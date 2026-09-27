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

        // Единые тюнингуемые константы положения/размера маркера (в пикселях;
        // якорь — правый верхний угол строки).
        private const float MarkerSize = 26f;
        private const float MarkerOffsetX = -4f;
        private const float MarkerOffsetY = -4f;
        private const int MarkerFontSize = 22;

        // ВСЕ увиденные строки очереди (не только строки с маркером) — чтобы по
        // изменению флага (см. патч set_IsInfinite) пересинхронизировать их разом.
        private static readonly Dictionary<int, UICraftQueueElementWidget> _widgets =
            new Dictionary<int, UICraftQueueElementWidget>();
        private static readonly Dictionary<int, Button> _markers = new Dictionary<int, Button>();
        // Подпись последнего состояния строки — чтобы Sync был дешёвым и идемпотентным
        // (Sync вызывается дважды за Redraw: самим postfix'ом и вложенным HideSelection).
        private static readonly Dictionary<int, string> _state = new Dictionary<int, string>();

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
                var stationId = on ? StationKey.Of(data.WgoData) : null;
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

                // Ничего не изменилось и маркер на месте — только поднять наверх (дёшево).
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
                    Sync(widget); // сразу убрать маркер, не дожидаясь следующего Redraw
                });
                btn.gameObject.SetActive(true);
                btn.transform.SetAsLastSibling();
            }
            catch (Exception ex)
            {
                if (Plugin.Mod != null && Plugin.Mod.DebugLog != null && Plugin.Mod.DebugLog.Value)
                    Plugin.Log?.LogInfo("endless: row marker sync: " + ex.Message);
            }
        }

        // Пересинхронизировать все известные строки. Вызывается из postfix'а
        // CraftElementBase.set_IsInfinite (реально срабатывает при смене флага)
        // и из EndlessMode.TurnOn/TurnOff.
        internal static void SyncAll()
        {
            if (_widgets.Count == 0) return;
            foreach (var key in new List<int>(_widgets.Keys))
            {
                var w = _widgets[key];
                if (w == null)
                {
                    // Виджет уничтожен пулом — чистим словари.
                    _widgets.Remove(key);
                    _markers.Remove(key);
                    _state.Remove(key);
                    continue;
                }
                Sync(w);
            }
        }

        private static Button EnsureMarker(UICraftQueueElementWidget widget, int key)
        {
            if (_markers.TryGetValue(key, out var cached) && cached != null
                && cached.transform.parent == widget.transform)
                return cached;

            var parent = widget.transform as RectTransform;
            if (parent == null) return null;

            // Виджеты берутся из пула — сначала переиспользуем уже созданный маркер.
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

            // Без цветовых переходов Button — иначе ховер-тинт/вспышка (как у кнопки окна).
            var selectable = btn as Selectable;
            if (selectable != null) selectable.transition = Selectable.Transition.None;

            // Яркий акцентный фон — «∞» должен читаться на тёмной строке.
            var img = btn.targetGraphic as Image;
            if (img != null) img.color = GameStyle.Accent;

            _markers[key] = btn;
            return btn;
        }
    }
}
