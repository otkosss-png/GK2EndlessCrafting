using BepInEx.Configuration;
using GK2.Framework;

namespace GK2EndlessCrafting
{
    // Настройки мода БЕЗ типов GK2 Framework: остальной код читает их всегда, даже когда
    // Framework не установлен (он в Workshop не лежит, жёсткая зависимость молча отключала
    // весь мод — ни кнопки, ни настроек).
    internal sealed class ModSettings
    {
        internal const string DefaultLanguage = "en";
        internal const bool DefaultEnabled = true;
        internal const int DefaultPollMs = 250;
        internal const bool DefaultDebugLog = false;

        internal ConfigEntry<bool> Enabled;
        internal ConfigEntry<int> PollMs;
        internal ConfigEntry<string> Language;
        internal ConfigEntry<bool> DebugLog;
    }

    // Мост к игровому меню Mods (GK2 Mod Framework). Класс трогается ТОЛЬКО из
    // Plugin.RegisterWithFramework, когда Framework найден: иначе загрузка типа упала бы.
    // Строки здесь — АНГЛИЙСКИЕ заглушки; русские тексты лежат в ModLocalization.
    internal sealed class FrameworkMod : Gk2ModBase
    {
        private readonly Gk2ModMetadata _metadata = new Gk2ModMetadata(
            "otkosss.gk2.endlesscrafting",
            "GK2 Endless Crafting",
            "otkosss",
            "1.0.11",
            "Stations keep repeating the selected recipe until you switch it off.",
            false,
            false);

        public override Gk2ModMetadata Metadata => _metadata;

        public override void OnRegister(Gk2ModContext context)
        {
            var s = context.Settings;
            var m = Plugin.Mod;
            m.Enabled = s.AddToggle("General", "Enabled", ModSettings.DefaultEnabled,
                "Enabled", "Show the endless crafting button in the station window.", 10);
            m.PollMs = s.AddIntSlider("General", "PollMs", ModSettings.DefaultPollMs, 100, 5000,
                "Station scan interval (ms)", "How often the mod re-checks the open station; 250 ms works for most setups.",
                50, 20);
            m.Language = s.AddDropdown("General", "Language", ModSettings.DefaultLanguage, new[] { "auto", "en", "ru" },
                "Language", "en, ru, auto (system language).", 10);
            m.DebugLog = s.AddToggle("General", "DebugLog", ModSettings.DefaultDebugLog,
                "Verbose log", "Write detailed diagnostics to the BepInEx log.", 5);
        }
    }
}
