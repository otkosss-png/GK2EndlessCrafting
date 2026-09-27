using HarmonyLib;

namespace GK2EndlessCrafting
{
    // Open вызывается один раз на открытие окна выбора крафта и не переопределяется
    // производными окнами; data уже установлена. Лучшая точка для вставки кнопки.
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "Open")]
    internal static class CraftWindowOpenPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIBaseCraftSelectionWindow __instance)
        {
            EndlessButton.Ensure(__instance);
        }
    }

    // Базовый Hide: срабатывает для UICraftSelectionWindow (он Hide не переопределяет).
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "Hide")]
    internal static class CraftWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIBaseCraftSelectionWindow __instance)
        {
            EndlessButton.Clear();
        }
    }

    // UIFuelCraftWindow и UISingleCraftWindow объявляют собственный Hide (Cecil:
    // каждый вызывает base UIBaseCraftSelectionWindow.Hide(), но патчим и сам
    // переопределённый метод — очистка гарантирована независимо от вызовов base).
    // EndlessButton.Clear() идемпотентен, двойной прогон безопасен.
    [HarmonyPatch(typeof(UIFuelCraftWindow), "Hide")]
    internal static class FuelCraftWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIFuelCraftWindow __instance)
        {
            EndlessButton.Clear();
        }
    }

    [HarmonyPatch(typeof(UISingleCraftWindow), "Hide")]
    internal static class SingleCraftWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UISingleCraftWindow __instance)
        {
            EndlessButton.Clear();
        }
    }
}
