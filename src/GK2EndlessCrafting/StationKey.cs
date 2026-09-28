using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace GK2EndlessCrafting
{
    // Ключ станции (WgoData.UniqueId) и рецепта (CraftDef.id), обратный резолв ключа
    // станции в её WgoData/CraftComponent, плюс CraftParamsData для постановки крафта
    // (без него AddToQueue падает — см. report, fix round 2). Обращения к игре — в try/catch.
    internal static class StationKey
    {
        // Поле "data" объявлено в базовом LazyWidget<UIBaseCraftSelectionWindowData>;
        // AccessTools.Field ищет и в базовых типах.
        private static readonly FieldInfo DataField =
            AccessTools.Field(typeof(UIBaseCraftSelectionWindow), "data");

        // CraftParamsData, снятый с игрового окна в момент включения «∞» (data.ParamsData) —
        // ровно тот, что игра передаёт в свой add-to-queue. Ключ — stationId.
        private static readonly Dictionary<string, CraftParamsData> _params =
            new Dictionary<string, CraftParamsData>();

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

        internal static string Of(WgoData wgo)
        {
            try { return wgo?.UniqueId?.ToString(); }
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

        internal static WgoData ResolveWgo(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return null;
            try
            {
                var world = MainGame.Instance?.GameSave?.WorldData;
                if (world == null) return null;
                return world.GetWgoData(SGuid.Parse(stationId));
            }
            catch { return null; }
        }

        // Станция, где «∞» не предлагаем (см. Core.StationRules).
        internal static bool IsExcluded(WgoData wgo)
        {
            try { return wgo != null && GK2EndlessCrafting.Core.StationRules.IsExcluded(wgo.id); }
            catch { return false; }
        }

        internal static CraftComponent Resolve(string stationId)
            => ResolveWgo(stationId)?.CraftComponent;

        internal static void CaptureParams(string stationId, CraftParamsData p)
        {
            if (string.IsNullOrEmpty(stationId) || p == null) return;
            try { _params[stationId] = p; }
            catch { }
        }

        // Копия снятых params (чтобы элементы не делили один изменяемый объект).
        internal static CraftParamsData ParamsFor(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return null;
            if (!_params.TryGetValue(stationId, out var p) || p == null) return null;
            try { return new CraftParamsData(p); }
            catch { return null; }
        }

        // Фолбэк: строим params так же, как это делает UIBaseCraftSelectionWindowData.
        // UpdateCraftParamsDataAndCanStartStatus() при отсутствии params:
        //   new CraftParamsData(craftDef.id, wgoData, CraftParamsType.Common, -1)
        //   RecalculateParams(needItems, worker)
        internal static CraftParamsData BuildParams(WgoData wgo, CraftDefBase def)
        {
            if (wgo == null || def == null) return null;
            try
            {
                var p = new CraftParamsData(def.id, wgo, CraftParamsData.CraftParamsType.Common, -1);
                p.RecalculateParams(def.needItems, wgo.Worker);
                return p;
            }
            catch { return null; }
        }
    }
}
