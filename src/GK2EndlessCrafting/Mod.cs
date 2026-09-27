using BepInEx.Configuration;
using GK2.Framework;

namespace GK2EndlessCrafting
{
    // Настройки появляются в игровом меню Mods (GK2 Mod Framework).
    internal sealed class Mod : Gk2ModBase
    {
        internal const string DefaultLanguage = "en";
        internal const bool DefaultEnabled = true;
        internal const int DefaultPollSeconds = 1;
        internal const bool DefaultDebugLog = false;

        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.endlesscrafting",
            "GK2 Endless Crafting",
            "otkosss",
            "1.0.0",
            "Stations keep repeating the selected recipe until you switch it off.",
            false,
            false);

        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<int> PollSeconds;
        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> DebugLog;

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            Enabled = s.AddToggle("General", "Enabled", DefaultEnabled,
                "Кнопка «∞»", "Показывать кнопку бесконечного крафта в окне станции", 10);
            PollSeconds = s.AddIntSlider("General", "PollSeconds", DefaultPollSeconds, 1, 10,
                "Проверка станций, с", "Как часто проверять очередь", 1, 20);
            Language = s.AddDropdown("General", "Language", DefaultLanguage, new[] { "auto", "en", "ru" },
                "Language / Язык", "en, ru, auto (system)", 10);
            DebugLog = s.AddToggle("General", "DebugLog", DefaultDebugLog,
                "Отладочный лог", "", 5);
        }
    }
}
