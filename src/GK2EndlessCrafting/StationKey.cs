using System.Reflection;
using HarmonyLib;

namespace GK2EndlessCrafting
{
    // Ключ станции (WgoData.UniqueId) и рецепта (CraftDef.id), а также обратный
    // резолв ключа станции в её CraftComponent. Все игровые обращения — в try/catch.
    internal static class StationKey
    {
        // Поле "data" объявлено в базовом LazyWidget<UIBaseCraftSelectionWindowData>;
        // AccessTools.Field ищет и в базовых типах.
        private static readonly FieldInfo DataField =
            AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "data");

        internal static UIBaseCraftSelectionWindowData DataOf(UIBaseCraftSelectionWindow w)
        {
            if (w == null || DataField == null) return null;
            try { return DataField.GetValue(w) as UIBaseCraftSelectionWindowData; }
            catch { return null; }
        }

        internal static string Of(UIBaseCraftSelectionWindow w)
        {
            try
            {
                var uid = DataOf(w)?.WgoData?.UniqueId;
                return uid?.ToString();
            }
            catch { return null; }
        }

        internal static string RecipeIdOf(UIBaseCraftSelectionWindow w)
        {
            try
            {
                var def = w != null ? w.CraftDef : null;
                if (def == null) def = DataOf(w)?.CraftDefinition;
                return def?.id;
            }
            catch { return null; }
        }

        internal static CraftComponent Resolve(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return null;
            try
            {
                var world = MainGame.Instance?.GameSave?.WorldData;
                if (world == null) return null;
                var wgo = world.GetWgoData(SGuid.Parse(stationId));
                return wgo?.CraftComponent;
            }
            catch { return null; }
        }
    }
}
