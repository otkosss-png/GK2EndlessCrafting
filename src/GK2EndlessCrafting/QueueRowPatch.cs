using HarmonyLib;

namespace GK2EndlessCrafting
{
    // Redraw каждый раз прячет игровую infCraftButton (через HideSelection) и не
    // рисует индикатор бесконечности — после него синхронизируем наш маркер «∞».
    [HarmonyPatch(typeof(UICraftQueueElementWidget), "Redraw")]
    internal static class QueueRowRedrawPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UICraftQueueElementWidget __instance)
        {
            QueueRowMarker.Sync(__instance);
        }
    }

    // HideSelection прячет игровые кнопки строки (в т.ч. при уходе курсора) —
    // синхронизируем маркер и здесь, чтобы он не пропадал при hover-out.
    [HarmonyPatch(typeof(UICraftQueueElementWidget), "HideSelection")]
    internal static class QueueRowHideSelectionPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UICraftQueueElementWidget __instance)
        {
            QueueRowMarker.Sync(__instance);
        }
    }

    // Ключевой хук: ни Redraw, ни HideSelection не вызываются при смене IsInfinite
    // (сеттер дёргает OnCountChanged, который у виджета ведёт только в UpdateCount).
    // Поэтому маркер пересинхронизируем прямо на сеттере флага.
    [HarmonyPatch(typeof(CraftElementBase), "set_IsInfinite")]
    internal static class CraftElementIsInfinitePatch
    {
        [HarmonyPostfix]
        private static void Postfix(CraftElementBase __instance)
        {
            QueueRowMarker.SyncAll();
        }
    }
}
