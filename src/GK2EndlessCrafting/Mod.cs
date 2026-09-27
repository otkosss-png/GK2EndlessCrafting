using BepInEx.Configuration;
using GK2.Framework;

namespace GK2EndlessCrafting
{
    // Настройки мода в игровом меню Mods (GK2 Mod Framework).
    // Строки здесь — АНГЛИЙСКИЕ заглушки: они показываются, только если для текущего языка
    // нет файла локализации. Русские тексты лежат в ModLocalization (en/ru json).
    internal sealed class Mod : Gk2ModBase
    {
        internal const string DefaultLanguage = "en";
        internal const bool DefaultEnabled = true;
        internal const int DefaultPollMs = 250;
        internal const bool DefaultDebugLog = false;

        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.endlesscrafting",
            "GK2 Endless Crafting",
            "otkosss",
            "1.0.7",
            "Stations keep repeating the selected recipe until you switch it off.",
            false,
            false);

        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<int> PollMs;
        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> DebugLog;

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            Enabled = s.AddToggle("General", "Enabled", DefaultEnabled,
                "Enabled", "Show the endless crafting button in the station window.", 10);
            PollMs = s.AddIntSlider("General", "PollMs", DefaultPollMs, 100, 5000,
                "Station scan interval (ms)", "How often the mod re-checks the open station; 250 ms works for most setups.",
                50, 20);
            Language = s.AddDropdown("General", "Language", DefaultLanguage, new[] { "auto", "en", "ru" },
                "Language", "en, ru, auto (system language).", 10);
            DebugLog = s.AddToggle("General", "DebugLog", DefaultDebugLog,
                "Verbose log", "Write detailed diagnostics to the BepInEx log.", 5);
        }
    }
}
