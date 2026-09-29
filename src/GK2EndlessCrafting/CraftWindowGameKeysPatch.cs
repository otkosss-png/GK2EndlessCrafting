using System;
using System.Collections.Generic;
using GK2EndlessCrafting.Core;
using HarmonyLib;
using LazyBearTechnology;

namespace GK2EndlessCrafting
{
    // Поддержка геймпада. В окне крафта «+/−» на геймпаде — это крестовина (когда фокус на
    // ячейке результата), сами кнопки в навигацию не входят, поэтому и клон «∞» с геймпада
    // недостижим. Вешаем переключение «∞» на свободную клавишу окна через штатный механизм
    // игры LazyWindow<T>.GetGameKeyDelegates() (как «Создать»/«В очередь»): Y (GameKey.Fold —
    // у игры есть значок и привязка, в окне выбора рецепта он свободен), запасные — RT/LT.
    // НЕ GameKey.RightBumper: он не привязан к геймпаду (в подсказке пустой sprite), а LB/RB
    // (NextTab/PrevTab) в окне крафта занимает Craft Max (±10).
    // Подсказка добавляется в строку «+ / -» внизу окна.
    internal static class CraftWindowGameKeys
    {
        private static readonly GameKey[] Candidates = { GameKey.Fold, GameKey.RightTrigger, GameKey.LeftTrigger };

        // Какая клавиша досталась окну (по instanceId) — для подсказки.
        private static readonly Dictionary<int, GameKey> _keys = new Dictionary<int, GameKey>();

        internal static void Register(UIBaseCraftSelectionWindow window, Dictionary<GameKey, Func<bool>> dict)
        {
            try
            {
                if (window == null || dict == null) return;
                foreach (var key in Candidates)
                {
                    if (key == null || dict.ContainsKey(key)) continue;
                    dict[key] = () => EndlessButton.ToggleFromGamepad(window);
                    _keys[window.GetInstanceID()] = key;
                    Plugin.Log?.LogInfo("endless: controller shortcut " + key + " -> infinity toggle (" + window.GetType().Name + ")");
                    return;
                }
                Plugin.Log?.LogWarning("endless: no free controller key for the infinity toggle in " + window.GetType().Name);
            }
            catch (Exception ex) { Plugin.Log?.LogWarning("endless: game keys: " + ex.Message); }
        }

        internal static void AddTip(UIBaseCraftSelectionWindow window, List<LazyGameKeyTip> tips)
        {
            try
            {
                if (window == null || tips == null) return;
                if (!_keys.TryGetValue(window.GetInstanceID(), out var key)) return;
                if (!EndlessButton.IsAvailable(window)) return;
                tips.Add(new LazyGameKeyTip(key, EndlessText.GamepadTip(Plugin.Lang), true));
            }
            catch (Exception ex) { Plugin.Log?.LogWarning("endless: gamepad tip: " + ex.Message); }
        }
    }

    // GetGameKeyDelegates переопределён в каждом конкретном окне — патчим все три.
    [HarmonyPatch(typeof(UICraftSelectionWindow), "GetGameKeyDelegates")]
    internal static class CraftSelectionGameKeysPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UICraftSelectionWindow __instance, Dictionary<GameKey, Func<bool>> __result)
            => CraftWindowGameKeys.Register(__instance, __result);
    }

    [HarmonyPatch(typeof(UIFuelCraftWindow), "GetGameKeyDelegates")]
    internal static class FuelCraftGameKeysPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIFuelCraftWindow __instance, Dictionary<GameKey, Func<bool>> __result)
            => CraftWindowGameKeys.Register(__instance, __result);
    }

    [HarmonyPatch(typeof(UISingleCraftWindow), "GetGameKeyDelegates")]
    internal static class SingleCraftGameKeysPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UISingleCraftWindow __instance, Dictionary<GameKey, Func<bool>> __result)
            => CraftWindowGameKeys.Register(__instance, __result);
    }

    // Подсказка «RB  Бесконечно» рядом с «+ / -» (их игра печатает, когда фокус на результате).
    [HarmonyPatch(typeof(UIBaseCraftSelectionWindow), "AddCraftCountGamepadTips")]
    internal static class CraftCountTipsPatch
    {
        [HarmonyPostfix]
        private static void Postfix(UIBaseCraftSelectionWindow __instance, List<LazyGameKeyTip> tips)
            => CraftWindowGameKeys.AddTip(__instance, tips);
    }
}
