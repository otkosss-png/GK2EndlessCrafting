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
        // Родной цвет глифа «+» (для выкл-состояния), чтобы Sync мог вернуть его.
        private static readonly Dictionary<int, Color> _labelColors = new Dictionary<int, Color>();
        private const char InfinityChar = '\u221E';
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

                // Считываем стиль РОДНОГО глифа «+» (TMP) ДО удаления детей — чтобы наш
                // «∞» выглядел так же (шрифт/материал/размер/цвет).
                var srcGlyph = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                var srcFont = srcGlyph != null ? srcGlyph.font : null;
                var srcMat = srcGlyph != null ? srcGlyph.fontSharedMaterial : null;
                var srcSize = srcGlyph != null ? srcGlyph.fontSize : 0f;
                var srcColor = srcGlyph != null ? srcGlyph.color : Color.white;

                // Убираем ВСЕ дочерние визуалы клона (родной «+» — TMP и/или Image),
                // иначе на кнопке окажутся два глифа. Фон (targetGraphic) сохраняем.
                int removed = RemoveChildVisuals(btn);

                // Отключаем цветовые переходы: иначе DoStateTransition сбрасывает подсветку.
                var selectable = btn as Selectable;
                if (selectable != null) selectable.transition = Selectable.Transition.None;

                var label = BuildLabel(btn, prt, srcFont, srcMat, srcSize, srcColor);
                btn.onClick.AddListener(() => Toggle(window));

                _buttons[key] = btn;
                _labels[key] = label;
                _labelColors[key] = label.color;
                Sync(window);

                // Одноразовая диагностика внешнего вида кнопки (на каждую созданную кнопку).
                LogButtonCreated(srcSprite, cloneImg, label, removed);
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
                // Тинтуем ТОЛЬКО фон кнопки. Подпись: родной цвет глифа (как у «+») в
                // выкл-состоянии и белый на акцентном фоне (вкл) — чтобы оставалась видна.
                var img = btn.targetGraphic as Image;
                if (img != null) img.color = on ? GameStyle.Accent : Color.white;
                if (_labels.TryGetValue(key, out var label) && label != null)
                    label.color = on ? Color.white
                        : (_labelColors.TryGetValue(key, out var c) ? c : Color.white);
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
            _labelColors.Clear();
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

        // Убирает все дочерние визуалы клона (родной глиф «+» — TMP и/или Image).
        // Фон (targetGraphic и его ветку) сохраняем. Возвращает число удалённых.
        private static int RemoveChildVisuals(LazyButton btn)
        {
            var parent = btn != null ? btn.transform : null;
            if (parent == null) return 0;
            var keep = btn.targetGraphic != null ? btn.targetGraphic.transform : null;
            var children = new List<Transform>(parent.childCount);
            for (int i = 0; i < parent.childCount; i++) children.Add(parent.GetChild(i));
            int removed = 0;
            foreach (var child in children)
            {
                if (child == null) continue;
                // Пропускаем ветку фона (сам targetGraphic или его предок).
                if (keep != null && (child == keep || keep.IsChildOf(child))) continue;
                UnityEngine.Object.Destroy(child.gameObject);
                removed++;
            }
            return removed;
        }

        // Шрифт, который реально умеет рисовать «∞». Лучший — родной шрифт глифа «+»
        // (тогда «∞» выглядит как соседний «+»); иначе GameStyle (им же рисуется
        // маркер строки очереди, где «∞» отображается корректно); иначе — любой
        // TMP-шрифт сцены с этим глифом.
        private static TMP_FontAsset PickInfinityFont(TMP_FontAsset source)
        {
            if (CanRenderInfinity(source)) return source;
            var gs = GameStyle.Font;
            if (CanRenderInfinity(gs)) return gs;
            try
            {
                foreach (var t in Resources.FindObjectsOfTypeAll<TMP_Text>())
                    if (t != null && t.font != null && CanRenderInfinity(t.font)) return t.font;
            }
            catch { }
            try { Plugin.Log?.LogWarning("endless: no TMP font with U+221E found; using GameStyle font"); }
            catch { }
            return gs;
        }

        private static bool CanRenderInfinity(TMP_FontAsset f)
        {
            if (f == null) return false;
            try { return f.HasCharacter((int)InfinityChar); }
            catch { return true; } // при сомнении не блокируем
        }

        private static TextMeshProUGUI BuildLabel(LazyButton btn, RectTransform srcRect,
            TMP_FontAsset srcFont, Material srcMat, float srcSize, Color srcColor)
        {
            float h = srcRect != null ? Mathf.Abs(srcRect.rect.height) : 0f;
            int autoSize = h > 1f ? Mathf.Clamp(Mathf.RoundToInt(h * 0.8f), 14, 40) : 24;
            int size = srcSize > 1f ? Mathf.RoundToInt(srcSize) : autoSize;

            var label = UiFactory.Label("Label", btn.transform, EndlessText.Infinity(Plugin.Lang), size,
                TextAlignmentOptions.Center, srcColor);

            // Шрифт+материал пары: родной глиф «+» → GameStyle → любой шрифт сцены с ∞.
            var font = PickInfinityFont(srcFont);
            if (font != null)
            {
                label.font = font; // TMP сам подставит материал этого шрифта
                if (font == srcFont && srcMat != null) label.fontSharedMaterial = srcMat;
                else if (font == GameStyle.Font && GameStyle.FontMaterial != null)
                    label.fontSharedMaterial = GameStyle.FontMaterial;
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
        // спрайт был у источника, какой применили, сколько дочерних визуалов убрали,
        // и итоговый текст/шрифт/геометрия подписи. Чтобы регрессия вида «два глифа»,
        // «чужая иконка» или «блоб вместо ∞» ловилась из лога.
        private static void LogButtonCreated(Sprite srcSprite, Image cloneImg, TextMeshProUGUI label, int removed)
        {
            try
            {
                var lrt = label != null ? label.rectTransform : null;
                var fontName = label != null && label.font != null ? label.font.name : "(none)";
                Plugin.Log?.LogInfo("endless: button created srcSprite=" + SpriteName(srcSprite)
                    + " usedSprite=" + SpriteName(cloneImg != null ? cloneImg.sprite : null)
                    + " removedChildren=" + removed
                    + " label(text='" + (label != null ? label.text : "") + "'"
                    + " font=" + fontName
                    + " size=" + (label != null ? label.fontSize : 0f)
                    + " anchorMin=" + (lrt != null ? lrt.anchorMin.ToString() : "-")
                    + " anchorMax=" + (lrt != null ? lrt.anchorMax.ToString() : "-")
                    + " sizeDelta=" + (lrt != null ? lrt.sizeDelta.ToString() : "-")
                    + " anchoredPos=" + (lrt != null ? lrt.anchoredPosition.ToString() : "-")
                    + " color=" + (label != null ? label.color.ToString() : "-") + ")");
            }
            catch { }
        }

        private static string SpriteName(Sprite s)
            => s == null || string.IsNullOrEmpty(s.name) ? "(none)" : s.name;
    }
}
