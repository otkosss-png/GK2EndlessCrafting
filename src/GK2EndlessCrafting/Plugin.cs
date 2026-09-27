using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2EndlessCrafting.Core;

namespace GK2EndlessCrafting
{
    [BepInDependency("ru.superman4eg.gk2.framework")]
    [BepInPlugin(Guid, "GK2 Endless Crafting", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "otkosss.gk2.endlesscrafting";
        public static ManualLogSource Log;
        internal static Mod Mod;
        internal static Lang Lang = Lang.En;
        internal static EndlessRegistry Registry = new EndlessRegistry();

        private void Awake()
        {
            Log = Logger;

            Mod = new Mod();
            try
            {
                GK2.Framework.FrameworkApi.RegisterMod(Mod, Config);
            }
            catch (Exception ex)
            {
                Logger.LogWarning("GK2 Framework register failed, using local config: " + ex.Message);
            }
            EnsureSettings();

            Lang = ResolveLanguage(Mod.Language.Value);

            try { new HarmonyLib.Harmony(Guid).PatchAll(typeof(Plugin).Assembly); }
            catch (Exception ex) { Logger.LogWarning("harmony patch failed: " + ex.Message); }

            EndlessRunner.Start();

            Logger.LogInfo("GK2 Endless Crafting " + Version + " loaded.");
        }

        private void EnsureSettings()
        {
            if (Mod.DebugLog != null) return;
            Mod.Enabled = Config.Bind("General", "Enabled", Mod.DefaultEnabled, "Show the endless crafting button");
            Mod.PollMs = Config.Bind("General", "PollMs", Mod.DefaultPollMs, "Fallback station poll interval (ms)");
            Mod.Language = Config.Bind("General", "Language", Mod.DefaultLanguage, "auto | en | ru");
            Mod.DebugLog = Config.Bind("General", "DebugLog", Mod.DefaultDebugLog, "Verbose debug log");
        }

        internal static Lang ResolveLanguage(string value)
        {
            if (string.Equals(value, "en", StringComparison.OrdinalIgnoreCase)) return Lang.En;
            if (string.Equals(value, "ru", StringComparison.OrdinalIgnoreCase)) return Lang.Ru;
            var two = System.Globalization.CultureInfo.CurrentUICulture?.TwoLetterISOLanguageName;
            return string.Equals(two, "ru", StringComparison.OrdinalIgnoreCase) ? Lang.Ru : Lang.En;
        }

        internal static string Version => typeof(Plugin).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    }
}
