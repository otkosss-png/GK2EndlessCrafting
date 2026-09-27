using System;
using System.Collections.Generic;
using GK2EndlessCrafting.Core;
using UnityEngine;
using UnityEngine.UI;

namespace GK2EndlessCrafting
{
    // Видимый маркер «∞» на строке очереди крафта. Игра свою кнопку infCraftButton
    // никогда не показывает (HideSelection прячет её в каждом Redraw) и не рисует
    // индикатор бесконечности, поэтому рисуем собственный кликабельный «∞». Клик
    // снимает бесконечность через общую точку EndlessMode.TurnOff.
    internal static class QueueRowMarker
    {
        private const string MarkerName = "EndlessRowMarker";
        private static readonly Dictionary<int, Button> _markers = new Dictionary<int, Button>();

        // Вызывается из postfix'ов UICraftQueueElementWidget.Redraw/HideSelection.
        internal static void Sync(UICraftQueueElementWidget widget)
        {
            if (widget == null) return;
            try
            {
                var data = widget.Data;
                var el = data != null ? data.CraftQueueElement : null;
                bool on = el != null && el.IsInfinite;

                var btn = EnsureMarker(widget);
                if (btn == null) return;

                btn.onClick.RemoveAllListeners();
                if (on)
                {
                    var stationId = StationKey.Of(data.WgoData);
                    btn.onClick.AddListener(() =>
                    {
                        EndlessMode.TurnOff(stationId);
                        Sync(widget); // сразу убрать маркер, не дожидаясь следующего Redraw
                    });
                }
                btn.gameObject.SetActive(on);
            }
            catch (Exception ex)
            {
                if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                    Plugin.Log?.LogInfo("endless: row marker sync: " + ex.Message);
            }
        }

        private static Button EnsureMarker(UICraftQueueElementWidget widget)
        {
            int key = widget.GetInstanceID();
            if (_markers.TryGetValue(key, out var cached) && cached != null
                && cached.transform.parent == widget.transform)
            {
                cached.transform.SetAsLastSibling();
                return cached;
            }

            var parent = widget.transform as RectTransform;
            if (parent == null) return null;

            // Виджеты берутся из пула — сначала переиспользуем уже созданный маркер.
            var existing = parent.Find(MarkerName) as RectTransform;
            var btn = existing != null ? existing.GetComponent<Button>() : null;
            if (btn == null)
                btn = UiFactory.TextButton(MarkerName, parent, EndlessText.Infinity(Plugin.Lang), 22);
            if (btn == null) return null;

            var rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(26f, 26f);
            rt.anchoredPosition = new Vector2(-4f, -4f);

            // Яркий акцентный фон — «∞» должен читаться на тёмной строке.
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = GameStyle.Accent;

            _markers[key] = btn;
            return btn;
        }
    }
}
