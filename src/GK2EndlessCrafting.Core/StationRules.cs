using System;

namespace GK2EndlessCrafting.Core
{
    // Станции, где режим «∞» не предлагаем. Поленницы (firewood_shed_1/2) — не обычный
    // крафт: с бесконечной очередью ломается их отображение дров (жалоба игрока, 28.09).
    public static class StationRules
    {
        private static readonly string[] ExcludedPrefixes = { "firewood_shed" };

        // id — WgoData.id станции (например "firewood_shed_1").
        public static bool IsExcluded(string stationWgoId)
        {
            if (string.IsNullOrEmpty(stationWgoId)) return false;
            foreach (var prefix in ExcludedPrefixes)
                if (stationWgoId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
