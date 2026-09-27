using BepInEx.Configuration;
using GK2.Framework;

namespace GK2EndlessCrafting
{
    // Настройки появляются в игровом меню Mods (GK2 Mod Framework).
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
            "1.0.3",
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
                "Кнопка «∞»", "Показывать кнопку бесконечного крафта в окне станции", 10);
            PollMs = s.AddIntSlider("General", "PollMs", DefaultPollMs, 100, 5000,
                "Проверка станций, мс", "Резервный опрос очереди (мс); 250 по умолчанию", 50, 20);
            Language = s.AddDropdown("General", "Language", DefaultLanguage, new[] { "auto", "en", "ru" },
                "Language / Язык", "en, ru, auto (system)", 10);
            DebugLog = s.AddToggle("General", "DebugLog", DefaultDebugLog,
                "Отладочный лог", "", 5);
        }
    }
}
