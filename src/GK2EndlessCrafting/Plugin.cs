using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GK2EndlessCrafting.Core;

namespace GK2EndlessCrafting
{
    // Framework — мягкая зависимость: в Workshop его нет, а без него мод раньше не грузился
    // вовсе. Есть Framework — настройки в меню Mods; нет — кнопка работает, настройки в cfg.
    [BepInDependency(FrameworkGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, "GK2 Endless Crafting", "1.0.11")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "otkosss.gk2.endlesscrafting";
        internal const string FrameworkGuid = "ru.superman4eg.gk2.framework";
        public static ManualLogSource Log;
        internal static ModSettings Mod;
        internal static Lang Lang = Lang.En;
        internal static EndlessRegistry Registry = new EndlessRegistry();

        // Слот текущего сейва (SaveSlotData.slotName). null/пусто — MainGame ещё не создан.
        internal static string SlotName;

        private void Awake()
        {
            Log = Logger;

            Mod = new ModSettings();
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(FrameworkGuid))
            {
                try { RegisterWithFramework(); }
                catch (Exception ex)
                {
                    Logger.LogWarning("GK2 Framework register failed, using local config: " + ex.Message);
                }
            }
            else
            {
                Logger.LogInfo("GK2 Mod Framework not installed: using BepInEx\\config settings (no in-game Mods menu).");
            }
            EnsureSettings();

            // Локализация настроек (en/ru) для GK2 Framework: пишем файлы сами — так они
            // есть и при установке через Workshop-загрузчик.
            ModLocalization.EnsureFiles();

            Lang = ResolveLanguage(Mod.Language.Value);

            // Патчи по классам: один несошедшийся патч (например, геймпад-подсказка) не
            // отключает остальные, как было бы с PatchAll.
            try
            {
                var harmony = new HarmonyLib.Harmony(Guid);
                foreach (var type in LoadableTypes())
                {
                    try { harmony.CreateClassProcessor(type).Patch(); }
                    catch (Exception ex) { Logger.LogWarning("harmony patch " + type.Name + " failed: " + ex.Message); }
                }
            }
            catch (Exception ex) { Logger.LogWarning("harmony patch failed: " + ex.Message); }

            // Если MainGame уже существует — читаем сохранённые станции сразу.
            // Обычно на Awake его ещё нет: слот подхватит EndlessRunner при загрузке сейва.
            LoadForCurrentSlot();

            EndlessRunner.Start();

            Logger.LogInfo("GK2 Endless Crafting " + Version + " loaded.");
        }

        // Без Framework тип FrameworkMod не загрузится и GetTypes() бросит
        // ReflectionTypeLoadException — берём только успешно загруженные типы.
        private static System.Collections.Generic.IEnumerable<Type> LoadableTypes()
        {
            Type[] types;
            try { types = typeof(Plugin).Assembly.GetTypes(); }
            catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
            foreach (var t in types)
                if (t != null && t.Name != "FrameworkMod") yield return t; // не typeof: тот грузит тип
        }

        // Отдельный метод: типы Framework (FrameworkMod/FrameworkApi) подгружаются только
        // при его JIT, а он вызывается лишь когда Framework найден.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void RegisterWithFramework()
        {
            GK2.Framework.FrameworkApi.RegisterMod(new FrameworkMod(), Config);
        }

        // Текущий слот сейва. Единственный источник правды о «загруженном» слоте:
        // EndlessRunner перечитывает файл, когда это значение меняется.
        internal static string ResolveSlot()
        {
            try { return MainGame.Instance?.SaveSlotData?.slotName; }
            catch { return null; }
        }

        internal static void LoadForCurrentSlot()
        {
            var slot = ResolveSlot();
            if (string.IsNullOrEmpty(slot)) return;
            SlotName = slot;
            SaveSlotStore.Load(Registry, slot);
        }

        // Вызывается после переключения «∞»: слот берём свежий, если он уже виден.
        internal static void SaveRegistry()
        {
            var slot = ResolveSlot();
            if (!string.IsNullOrEmpty(slot)) SlotName = slot;
            SaveSlotStore.Save(Registry, SlotName);
        }

        private void EnsureSettings()
        {
            if (Mod.DebugLog != null) return;
            Mod.Enabled = Config.Bind("General", "Enabled", ModSettings.DefaultEnabled, "Show the endless crafting button");
            Mod.PollMs = Config.Bind("General", "PollMs", ModSettings.DefaultPollMs, "Fallback station poll interval (ms)");
            Mod.Language = Config.Bind("General", "Language", ModSettings.DefaultLanguage, "auto | en | ru");
            Mod.DebugLog = Config.Bind("General", "DebugLog", ModSettings.DefaultDebugLog, "Verbose debug log");
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
