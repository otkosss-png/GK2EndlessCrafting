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
                if (prt != null && rt != null)
                {
                    rt.anchorMin = prt.anchorMin;
                    rt.anchorMax = prt.anchorMax;
                    rt.pivot = prt.pivot;
                    rt.sizeDelta = prt.sizeDelta;
                    rt.anchoredPosition = prt.anchoredPosition + new Vector2(prt.sizeDelta.x + 6f, 0f);
                }

                // Отключаем цветовые переходы: иначе DoStateTransition сбрасывает подсветку.
                var selectable = btn as Selectable;
                if (selectable != null) selectable.transition = Selectable.Transition.None;

                var label = BuildLabel(btn);
                btn.onClick.AddListener(() => Toggle(window));

                _buttons[key] = btn;
                _labels[key] = label;
                Sync(window);
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
                var img = btn.targetGraphic as Image;
                if (img != null) img.color = on ? GameStyle.Accent : Color.white;
                if (_labels.TryGetValue(key, out var label) && label != null)
                    label.color = on ? Color.white : GameStyle.Text;
            }
            catch (Exception ex)
            {
                Trace("sync: " + ex.Message);
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

        private static TextMeshProUGUI BuildLabel(LazyButton btn)
        {
            var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = EndlessText.Infinity(Plugin.Lang);
            }
            else
            {
                // У «+»/«−» может не быть текстового узла (иконка) — заменяем графику
                // на игровой спрайт кнопки и рисуем «∞» поверх.
                UiFactory.ApplyButtonSprite(btn);
                label = UiFactory.Label("Label", btn.transform, EndlessText.Infinity(Plugin.Lang), 30,
                    TextAlignmentOptions.Center, Color.white);
                var rt = label.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            label.raycastTarget = false;
            return label;
        }
    }
}
