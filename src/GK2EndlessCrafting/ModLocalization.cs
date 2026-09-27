using System;
using System.IO;
using System.Reflection;
using BepInEx;

namespace GK2EndlessCrafting
{
    // GK2 Framework берёт названия/описания настроек из
    //   BepInEx\plugins\GK2.Framework\Localization\<modId>\<lang>.json
    // (ключи settings.<Section>.<Key>.name / .description; если ключа нет — берётся
    // английская заглушка из кода). Мод приезжает и через Workshop-загрузчик (папка
    // _Workshop), поэтому файлы пишем сами из своего каталога — так локализация есть при
    // любом способе установки.
    internal static class ModLocalization
    {
        internal const string ModId = "otkosss.gk2.endlesscrafting";

        private const string EnJson = @"{
  ""mod.name"": ""GK2 Endless Crafting"",
  ""mod.description"": ""Stations keep repeating the selected recipe until you switch it off."",
  ""settings.General.Enabled.name"": ""Enabled"",
  ""settings.General.Enabled.description"": ""Show the endless crafting button in the station window."",
  ""settings.General.PollMs.name"": ""Station scan interval (ms)"",
  ""settings.General.PollMs.description"": ""How often the mod re-checks the open station; 250 ms works for most setups."",
  ""settings.General.Language.name"": ""Language"",
  ""settings.General.Language.description"": ""en, ru, auto (system language)."",
  ""settings.General.DebugLog.name"": ""Verbose log"",
  ""settings.General.DebugLog.description"": ""Write detailed diagnostics to the BepInEx log.""
}";

        private const string RuJson = @"{
  ""mod.name"": ""GK2 Бесконечный крафт"",
  ""mod.description"": ""Станции повторяют выбранный рецепт, пока вы это не выключите."",
  ""settings.General.Enabled.name"": ""Включено"",
  ""settings.General.Enabled.description"": ""Показывать кнопку бесконечного крафта в окне станции."",
  ""settings.General.PollMs.name"": ""Интервал сканирования станции, мс"",
  ""settings.General.PollMs.description"": ""Как часто мод проверяет открытую станцию; 250 мс подходит почти всем."",
  ""settings.General.Language.name"": ""Язык"",
  ""settings.General.Language.description"": ""en, ru, auto (язык системы)."",
  ""settings.General.DebugLog.name"": ""Подробный лог"",
  ""settings.General.DebugLog.description"": ""Писать подробную диагностику в лог BepInEx.""
}";

        internal static void EnsureFiles()
        {
            try
            {
                var selfDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(selfDir)) return;

                var source = Path.Combine(selfDir, "Localization");
                var mine = Path.Combine(source, ModId);

                Directory.CreateDirectory(mine);
                WriteIfChanged(Path.Combine(mine, "en.json"), EnJson);
                WriteIfChanged(Path.Combine(mine, "ru.json"), RuJson);

                var target = Path.Combine(BepInEx.Paths.PluginPath, "GK2.Framework", "Localization", ModId);
                Directory.CreateDirectory(target);
                WriteIfChanged(Path.Combine(target, "en.json"), EnJson);
                WriteIfChanged(Path.Combine(target, "ru.json"), RuJson);

                ReloadFramework();
            }
            catch (Exception ex) { Plugin.Log.LogWarning("localization: " + ex.Message); }
        }

        private static void WriteIfChanged(string path, string content)
        {
            try
            {
                if (File.Exists(path) && File.ReadAllText(path) == content) return;
                File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
            }
            catch (Exception ex) { Plugin.Log.LogWarning("localization write " + Path.GetFileName(path) + ": " + ex.Message); }
        }

        // Просим фреймворк перечитать переводы (если такой API есть в установленной версии).
        private static void ReloadFramework()
        {
            try
            {
                var t = Type.GetType("GK2.Framework.FrameworkLocalization, GK2.Framework");
                var m = t?.GetMethod("Reload", new[] { typeof(string) });
                if (m != null) { m.Invoke(null, new object[] { ModId }); Plugin.Log.LogInfo("localization: reloaded " + ModId); }
            }
            catch (Exception ex) { Plugin.Log.LogWarning("localization reload: " + ex.Message); }
        }
    }
}
