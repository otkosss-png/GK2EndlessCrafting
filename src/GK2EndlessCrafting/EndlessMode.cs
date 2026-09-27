namespace GK2EndlessCrafting
{
    // Единая точка включения/выключения режима «∞» для станции. Через неё проходят
    // ОБА пути: кнопка в окне крафта и маркер на строке очереди. Это гарантирует,
    // что реестр (а значит персистентность и ensure-проход раннера) всегда согласован:
    // иначе раннер в течение одного опроса вернул бы IsInfinite=true обратно.
    internal static class EndlessMode
    {
        internal static void TurnOn(string stationId, string recipeId, CraftParamsData capturedParams)
        {
            if (string.IsNullOrEmpty(stationId) || string.IsNullOrEmpty(recipeId)) return;
            Plugin.Registry.Set(stationId, recipeId);
            // Снятые params — ровно те, что игра отдаёт в свой add-to-queue.
            StationKey.CaptureParams(stationId, capturedParams);
            EndlessRunner.ApplyNow(stationId);
            Plugin.SaveRegistry();
        }

        internal static void TurnOff(string stationId)
        {
            if (string.IsNullOrEmpty(stationId)) return;
            Plugin.Registry.Clear(stationId);
            EndlessRunner.ClearNative(stationId);
            Plugin.SaveRegistry();
        }
    }
}
