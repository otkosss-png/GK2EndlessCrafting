using System;
using System.Collections.Generic;
using GK2EndlessCrafting.Core;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2EndlessCrafting
{
    // Геймпад в окне станции (очередь): мышью «∞» выключается кликом по значку строки, а на
    // геймпаде значок недостижим. Y (GameKey.Fold — в UICraftWindow он свободен) на строке
    // очереди с включённым режимом выключает его — то же, что клик по значку.
    internal static class QueueGameKeys
    {
        // GamepadNavigationController у LazyWindow<T> protected — читаем рефлексией.
        private static readonly System.Reflection.PropertyInfo _navProp =
            AccessTools.Property(typeof(LazyWindow<UIBaseCraftWindowData>), "GamepadNavigationController");

        internal static UICraftQueueElementWidget FocusedRow(UICraftWindow window)
        {
            var ctrl = window != null && _navProp != null
                ? _navProp.GetValue(window) as GamepadNavigationController : null;
            var focused = ctrl != null ? ctrl.FocusedItem : null;
            return focused != null ? focused.GetComponentInParent<UICraftQueueElementWidget>() : null;
        }

        internal static string EndlessStationOf(UICraftQueueElementWidget row)
        {
            var data = row != null ? row.Data : null;
            var el = data != null ? data.CraftQueueElement : null;
            if (el == null || !el.IsInfinite || data.WgoData == null) return null;
            return StationKey.Of(data.WgoData);
        }

        internal static bool TurnOffFocused(UICraftWindow window)
        {
            try
            {
                if (!LazyInput.IsGamepadActive) return false;
                var row = FocusedRow(window);
                var station = EndlessStationOf(row);
                if (string.IsNullOrEmpty(station)) return false;
                EndlessMode.TurnOff(station);
                QueueRowMarker.Sync(row);
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: queue key: " + ex.Message);
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(UICraftWindow), "GetGameKeyDelegates")]
    internal static class QueueGameKeysPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UICraftWindow __instance, Dictionary<GameKey, Func<bool>> __result)
        {
            try
            {
                if (__instance == null || __result == null || __result.ContainsKey(GameKey.Fold)) return;
                __result[GameKey.Fold] = () => QueueGameKeys.TurnOffFocused(__instance);
                Plugin.Log?.LogInfo("endless: controller shortcut Y (Fold) -> turn off endless on queue row");
            }
            catch (Exception ex) { Plugin.Log?.LogWarning("endless: queue keys: " + ex.Message); }
        }
    }

    // Подсказка «Y  Выкл. бесконечно»: UICraftWindow.PrintTips собирает список и сразу печатает
    // его через LazyButtonTipsStr.Print — поэтому во время PrintTips запоминаем строку в фокусе,
    // а в префиксе Print дописываем свою подсказку.
    [HarmonyPatch(typeof(UICraftWindow), "PrintTips", new[] { typeof(GamepadNavigationItem) })]
    internal static class QueueTipsContextPatch
    {
        [ThreadStatic] internal static UICraftQueueElementWidget Row;

        [HarmonyPrefix]
        private static void Prefix(UICraftWindow __instance, GamepadNavigationItem gamepadNavigationItem)
        {
            try
            {
                var row = gamepadNavigationItem != null
                    ? gamepadNavigationItem.GetComponentInParent<UICraftQueueElementWidget>() : null;
                Row = QueueGameKeys.EndlessStationOf(row) != null ? row : null;
            }
            catch { Row = null; }
        }

        [HarmonyPostfix]
        private static void Postfix() => Row = null;
    }

    [HarmonyPatch(typeof(LazyButtonTipsStr), "Print")]
    internal static class QueueTipsPrintPatch
    {
        [HarmonyPrefix]
        private static void Prefix(List<LazyGameKeyTip> tips)
        {
            try
            {
                if (QueueTipsContextPatch.Row == null || tips == null) return;
                tips.Add(new LazyGameKeyTip(GameKey.Fold, EndlessText.GamepadOffTip(Plugin.Lang), true));
            }
            catch { }
        }
    }
}
