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

    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "Hide")]
    internal static class CraftWindowHidePatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIBaseCraftSelectionWindow __instance)
        {
            EndlessButton.Clear();
        }
    }
}
