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
            catch { }
        }

        internal static void Clear()
        {
            // Hide() лишь деактивирует окно, поэтому созданные дочерние объекты надо
            // реально уничтожить — иначе повторное открытие их накопит.
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

                if (Plugin.Registry.IsOn(id)) Plugin.Registry.Clear(id);
                else Plugin.Registry.Set(id, recipe);
                Sync(window);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: toggle: " + ex.Message);
            }
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
