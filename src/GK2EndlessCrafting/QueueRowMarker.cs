using System;
using System.Collections.Generic;
using System.Reflection;
using GK2EndlessCrafting.Core;
using HarmonyLib;
using LazyBearTechnology;
using UnityEngine;
using UnityEngine.UI;

namespace GK2EndlessCrafting
{
    // Компактный значок «∞» в правом верхнем углу строки очереди. Глиф — РОДНАЯ
    // иконка игры (ui_btn_icon_infinity из скрытой infCraftButton строки), поэтому
    // вид нативный, а размер/место наши (родная кнопка в строке слишком крупная —
    // красная плашка на всю ячейку). Клик снимает режим через EndlessMode.TurnOff.
    internal static class QueueRowMarker
    {
        private const string MarkerName = "EndlessRowMarker";

        private const float MarkerSize = 26f;
        private const float MarkerOffsetX = -4f;
        private const float MarkerOffsetY = -4f;
        private const int MarkerFontSize = 22;

        private static readonly FieldInfo InfField =
            AccessTools.Field(typeof(UICraftQueueElementWidget), "infCraftButton");

        private static readonly Dictionary<int, UICraftQueueElementWidget> _widgets =
            new Dictionary<int, UICraftQueueElementWidget>();
        private static readonly Dictionary<int, Button> _markers = new Dictionary<int, Button>();
        private static readonly Dictionary<int, Image> _glyphs = new Dictionary<int, Image>();
        private static readonly Dictionary<int, string> _state = new Dictionary<int, string>();

        private static Sprite _infSprite;
        private static bool _infSpriteSearched;
        private static float _infSpriteNextTry;

        private static bool ModOn
            => Plugin.Mod != null && (Plugin.Mod.Enabled == null || Plugin.Mod.Enabled.Value);

        internal static void Sync(UICraftQueueElementWidget widget)
        {
            if (widget == null) return;
            try
            {
                var data = widget.Data;
                var el = data != null ? data.CraftQueueElement : null;
                bool on = ModOn && el != null && el.IsInfinite;
                var stationId = on && data.WgoData != null ? StationKey.Of(data.WgoData) : null;
                string sig = on ? ("1:" + stationId) : "0";

                int key = widget.GetInstanceID();
                _widgets[key] = widget;

                if (!on)
                {
                    _state[key] = sig;
                    if (_markers.TryGetValue(key, out var hidden) && hidden != null && hidden.gameObject.activeSelf)
                        hidden.gameObject.SetActive(false);
                    return;
                }

                var btn = EnsureMarker(widget, key);
                if (btn == null) return;

                if (_state.TryGetValue(key, out var prev) && prev == sig && btn.gameObject.activeSelf)
                {
                    btn.transform.SetAsLastSibling();
                    return;
                }

                _state[key] = sig;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    EndlessMode.TurnOff(stationId);
                    Sync(widget);
                });
                btn.gameObject.SetActive(true);
                btn.transform.SetAsLastSibling();
            }
            catch (Exception ex)
            {
                Trace("row marker sync: " + ex.Message);
            }
        }

        internal static void SyncAll()
        {
            if (_widgets.Count == 0) return;
            foreach (var key in new List<int>(_widgets.Keys))
            {
                var w = _widgets[key];
                if (w == null)
                {
                    _widgets.Remove(key);
                    _markers.Remove(key);
                    _glyphs.Remove(key);
                    _state.Remove(key);
                    continue;
                }
                Sync(w);
            }
        }

        // Родная иконка ∞ игры: ищем её в скрытой infCraftButton самой строки
        // (дочерний Image с именем «...icon...», спрайт ui_btn_icon_infinity).
        private static Sprite ResolveInfinitySprite(UICraftQueueElementWidget widget)
        {
            if (_infSprite != null) return _infSprite;
            if (_infSpriteSearched && Time.unscaledTime < _infSpriteNextTry) return null;
            _infSpriteNextTry = Time.unscaledTime + 1f;

            try
            {
                var inf = (widget != null && InfField != null ? InfField.GetValue(widget) : null) as LazyButton;
                if (inf == null) return null;
                var imgs = inf.GetComponentsInChildren<Image>(true);
                for (int i = 0; i < imgs.Length; i++)
                {
                    var im = imgs[i];
                    if (im == null || im.sprite == null) continue;
                    if (im.gameObject.name.IndexOf("icon", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    _infSprite = im.sprite;
                    _infSpriteSearched = true;
                    return _infSprite;
                }
            }
            catch { }
            return null;
        }

        private static Button EnsureMarker(UICraftQueueElementWidget widget, int key)
        {
            if (_markers.TryGetValue(key, out var cached) && cached != null
                && cached.transform.parent == widget.transform)
            {
                EnsureGlyph(cached, widget, key);
                return cached;
            }

            var parent = widget.transform as RectTransform;
            if (parent == null) return null;

            var existing = parent.Find(MarkerName) as RectTransform;
            var btn = existing != null ? existing.GetComponent<Button>() : null;
            if (btn == null)
                btn = UiFactory.TextButton(MarkerName, parent, EndlessText.Infinity(Plugin.Lang), MarkerFontSize);
            if (btn == null) return null;

            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(MarkerSize, MarkerSize);
            rt.anchoredPosition = new Vector2(MarkerOffsetX, MarkerOffsetY);

            var selectable = btn as Selectable;
            if (selectable != null) selectable.transition = Selectable.Transition.None;

            var img = btn.targetGraphic as Image;
            if (img != null) img.color = GameStyle.Accent;

            _markers[key] = btn;
            EnsureGlyph(btn, widget, key);
            return btn;
        }

        // Глиф: родная иконка ∞ (если нашли) вместо TMP-подписи.
        private static void EnsureGlyph(Button btn, UICraftQueueElementWidget widget, int key)
        {
            var sprite = ResolveInfinitySprite(widget);
            if (sprite == null) return; // остаётся TMP-подпись

            Image icon;
            if (!_glyphs.TryGetValue(key, out icon) || icon == null || icon.transform.parent != btn.transform)
            {
                var rt = UiFactory.Rect("Glyph", btn.transform);
                icon = rt.gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                _glyphs[key] = icon;
            }
            if (icon.sprite != sprite) icon.sprite = sprite;
            icon.color = Color.white;

            var irt = (RectTransform)icon.transform;
            irt.anchorMin = new Vector2(0.5f, 0.5f);
            irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = Vector2.zero;
            irt.sizeDelta = new Vector2(MarkerSize * 0.72f, MarkerSize * 0.72f);
            if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);

            // Прячем TMP-подпись, чтобы не было двух глифов.
            var label = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (label != null && label.gameObject.activeSelf) label.gameObject.SetActive(false);
        }

        private static void Trace(string msg)
        {
            if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                Plugin.Log?.LogInfo("endless: " + msg);
        }
    }
}
