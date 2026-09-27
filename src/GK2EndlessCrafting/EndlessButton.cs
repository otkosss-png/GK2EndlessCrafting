using System;
using System.Collections.Generic;
using System.Reflection;
using GK2EndlessCrafting.Core;
using HarmonyLib;
using LazyBearTechnology;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GK2EndlessCrafting
{
    // Кнопка «∞» в окне выбора крафта. Клонируется с игровой plusCraftButton,
    // поэтому остаётся LazyButton (сохраняется sprite и gamepad-навигация).
    internal static class EndlessButton
    {
        private static readonly Dictionary<int, LazyButton> _buttons = new Dictionary<int, LazyButton>();
        private static readonly Dictionary<int, TextMeshProUGUI> _labels = new Dictionary<int, TextMeshProUGUI>();
        // Открытые окна крафта — чтобы пересинхронизировать кнопку «∞» при смене
        // режима из другого места (маркер строки очереди), а не только при открытии.
        private static readonly Dictionary<int, UIBaseCraftSelectionWindow> _windows =
            new Dictionary<int, UIBaseCraftSelectionWindow>();

        private static readonly FieldInfo _plusField =
            AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "plusCraftButton");

        // LazyUIEvent-поля копируются Instantiate вместе с компонентом; их тоже надо
        // обнулить, иначе клик по «∞» дёрнет «+». onClick (Button) чистим отдельно.
        private static readonly FieldInfo[] _lazyEventFields =
        {
            AccessTools.Field(typeof(LazyButton), "onDown"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableDown"),
            AccessTools.Field(typeof(LazyButton), "onUp"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableUp"),
            AccessTools.Field(typeof(LazyButton), "onEnter"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableEnter"),
            AccessTools.Field(typeof(LazyButton), "onExit"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableExit"),
            AccessTools.Field(typeof(LazyButton), "onNotInteractableClick"),
        };

        private static bool ModOn
            => Plugin.Mod != null && (Plugin.Mod.Enabled == null || Plugin.Mod.Enabled.Value);

        internal static void Ensure(UIBaseCraftSelectionWindow window)
        {
            if (window == null) return;
            try
            {
                int key = window.GetInstanceID();
                _windows[key] = window;
                if (_buttons.TryGetValue(key, out var existing) && existing != null)
                {
                    existing.gameObject.SetActive(ModOn);
                    if (ModOn) Sync(window);
                    return;
                }

                if (!ModOn) return;

                var plus = _plusField != null ? _plusField.GetValue(window) as LazyButton : null;
                if (plus == null) return; // у окна нет выбора рецепта — кнопки нет
                var parent = plus.transform.parent;
                if (parent == null) return;

                var go = UnityEngine.Object.Instantiate(plus.gameObject, parent);
                go.name = "EndlessCraftBtn";
                var btn = go.GetComponent<LazyButton>();
                if (btn == null) { UnityEngine.Object.Destroy(go); return; }

                // У клона могли скопироваться сериализованные слушатели «+».
                btn.onClick.RemoveAllListeners();
                ClearLazyEvents(btn);

                var prt = plus.transform as RectTransform;
                var rt = btn.transform as RectTransform;
                var srcImg = plus.targetGraphic as Image;
                var srcSprite = srcImg != null ? srcImg.sprite : null;
                if (prt != null && rt != null)
                {
                    rt.anchorMin = prt.anchorMin;
                    rt.anchorMax = prt.anchorMax;
                    rt.pivot = prt.pivot;
                    rt.sizeDelta = prt.sizeDelta;
                    rt.anchoredPosition = prt.anchoredPosition + new Vector2(prt.sizeDelta.x + 6f, 0f);
                }

                // Клон сохраняет СВОЙ спрайт — это гарантированно вид соседней игровой
                // кнопки «+» (правильный размер/скругления/цвета). Глобальный GameStyle
                // применяем ТОЛЬКО если у источника спрайта нет (иначе нестабильный
                // хевристический поиск подменял кнопку чужой иконкой).
                var cloneImg = btn.targetGraphic as Image;
                if (cloneImg != null && cloneImg.sprite == null)
                {
                    var fallback = GameStyle.ButtonSprite;
                    if (fallback != null)
                    {
                        cloneImg.sprite = fallback;
                        cloneImg.type = fallback.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                    }
                }

                // Отключаем цветовые переходы: иначе DoStateTransition сбрасывает подсветку.
                var selectable = btn as Selectable;
                if (selectable != null) selectable.transition = Selectable.Transition.None;

                var label = BuildLabel(btn, prt);
                btn.onClick.AddListener(() => Toggle(window));

                _buttons[key] = btn;
                _labels[key] = label;
                Sync(window);

                // Одноразовая диагностика внешнего вида кнопки (на каждую созданную кнопку).
                LogButtonCreated(srcSprite, cloneImg, label);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: ensure button: " + ex.Message);
            }
        }

        internal static void Sync(UIBaseCraftSelectionWindow window)
        {
            if (window == null) return;
            try
            {
                int key = window.GetInstanceID();
                if (!_buttons.TryGetValue(key, out var btn) || btn == null) return;

                bool on = Plugin.Registry != null && Plugin.Registry.IsOn(StationKey.Of(window));
                // Тинтуем ТОЛЬКО фон кнопки; подпись «∞» всегда белая — читается и на
                // акцентном фоне (вкл), и на родном спрайте кнопки (выкл).
                var img = btn.targetGraphic as Image;
                if (img != null) img.color = on ? GameStyle.Accent : Color.white;
                if (_labels.TryGetValue(key, out var label) && label != null)
                    label.color = Color.white;
            }
            catch (Exception ex)
            {
                Trace("sync: " + ex.Message);
            }
        }

        // Пересинхронизировать кнопки «∞» во всех открытых окнах крафта. Вызывается
        // из EndlessMode.TurnOn/TurnOff, чтобы подсветка не отставала при смене
        // режима из строки очереди.
        internal static void SyncAll()
        {
            if (_windows.Count == 0) return;
            foreach (var key in new List<int>(_windows.Keys))
            {
                var w = _windows[key];
                if (w == null) { _windows.Remove(key); continue; }
                Sync(w);
            }
        }

        // Вызывается postfix'ами Hide: базового UIBaseCraftSelectionWindow и конкретных
        // UIFuelCraftWindow/UISingleCraftWindow. После Hide окно лишь деактивируется,
        // поэтому созданные объекты надо реально уничтожить (не только очистить
        // словари) — иначе повторное открытие накопит клоны. Метод идемпотентен,
        // повторный вызов (базовый + производный postfix) безопасен.
        internal static void Clear()
        {
            foreach (var kv in _buttons)
                if (kv.Value != null) UnityEngine.Object.Destroy(kv.Value.gameObject);
            _buttons.Clear();
            _labels.Clear();
            _windows.Clear();
        }

        private static void Toggle(UIBaseCraftSelectionWindow window)
        {
            try
            {
                var id = StationKey.Of(window);
                var recipe = StationKey.RecipeIdOf(window);
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(recipe)) return;

                // Обе ветки идут через EndlessMode — общую точку с маркером строки
                // очереди (реестр + нативный флаг + сохранение всегда согласованы).
                if (Plugin.Registry.IsOn(id)) EndlessMode.TurnOff(id);
                else EndlessMode.TurnOn(id, recipe, StationKey.DataOf(window)?.ParamsData);
                Sync(window);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: toggle: " + ex.Message);
            }
        }

        private static void ClearLazyEvents(LazyButton btn)
        {
            if (btn == null) return;
            foreach (var f in _lazyEventFields)
            {
                if (f == null) continue;
                try
                {
                    var evt = f.GetValue(btn) as UnityEngine.Events.UnityEvent;
                    if (evt != null) evt.RemoveAllListeners();
                }
                catch { }
            }
        }

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }

        private static TextMeshProUGUI BuildLabel(LazyButton btn, RectTransform srcRect)
        {
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label == null)
            {
                // У «+»/«−» нет текстового узла — рисуем «∞» поверх родного спрайта
                // кнопки (спрайт НЕ подменяем). Размер шрифта — по высоте кнопки.
                float h = srcRect != null ? Mathf.Abs(srcRect.rect.height) : 0f;
                int size = h > 1f ? Mathf.Clamp(Mathf.RoundToInt(h * 0.8f), 14, 40) : 24;
                label = UiFactory.Label("Label", btn.transform, EndlessText.Infinity(Plugin.Lang), size,
                    TextAlignmentOptions.Center, Color.white);
            }
            else
            {
                label.text = EndlessText.Infinity(Plugin.Lang);
            }

            // Подпись на весь rect кнопки, по центру, без смещений и клиппинга —
            // так же, как родной глиф «+»/«−».
            var rt = label.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(1f, 1f);
            rt.offsetMax = new Vector2(-1f, -1f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            label.alignment = TextAlignmentOptions.Center;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        // Одноразовый диагностический лог (Info) на каждую созданную кнопку: какой
        // спрайт был у источника, какой применили, и геометрия/шрифт подписи. Чтобы
        // регрессия вида «кнопка = чужая иконка» ловилась из лога.
        private static void LogButtonCreated(Sprite srcSprite, Image cloneImg, TextMeshProUGUI label)
        {
            try
            {
                var lrt = label != null ? label.rectTransform : null;
                Plugin.Log?.LogInfo("endless: button created srcSprite=" + SpriteName(srcSprite)
                    + " usedSprite=" + SpriteName(cloneImg != null ? cloneImg.sprite : null)
                    + " label(anchorMin=" + (lrt != null ? lrt.anchorMin.ToString() : "-")
                    + " anchorMax=" + (lrt != null ? lrt.anchorMax.ToString() : "-")
                    + " sizeDelta=" + (lrt != null ? lrt.sizeDelta.ToString() : "-")
                    + " anchoredPos=" + (lrt != null ? lrt.anchoredPosition.ToString() : "-")
                    + " fontSize=" + (label != null ? label.fontSize : 0f)
                    + " color=" + (label != null ? label.color.ToString() : "-") + ")");
            }
            catch { }
        }

        private static string SpriteName(Sprite s)
            => s == null || string.IsNullOrEmpty(s.name) ? "(none)" : s.name;
    }
}
