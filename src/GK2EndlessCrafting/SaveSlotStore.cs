using System;
using System.IO;
using System.Text;
using BepInEx;
using GK2EndlessCrafting.Core;

namespace GK2EndlessCrafting
{
    // Персистентность реестра «∞» по слоту сейва. Файл:
    //   BepInEx\config\GK2EndlessCrafting\<slot>.json  (UTF-8 без BOM)
    // Слот берётся из MainGame.Instance.SaveSlotData.slotName (см. отчёт по Task 5).
    // Битый/нечитаемый файл НЕ затираем: пустой реестр + warning; файл заменяется
    // только при следующем явном сохранении (переключение «∞»).
    internal static class SaveSlotStore
    {
        internal const string DefaultSlot = "default";

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        internal static string Dir => Path.Combine(Paths.ConfigPath, "GK2EndlessCrafting");

        internal static string FileFor(string slot)
            => Path.Combine(Dir, Sanitize(slot) + ".json");

        // Разрешаем буквы/цифры/_/-/. ; всё прочее — в '_'; пустое → "default".
        internal static string Sanitize(string slot)
        {
            if (string.IsNullOrEmpty(slot)) return DefaultSlot;
            var sb = new StringBuilder(slot.Length);
            foreach (var c in slot)
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.Length == 0 ? DefaultSlot : sb.ToString();
        }

        internal static void Save(EndlessRegistry registry, string slot)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var path = FileFor(slot);
                var text = EndlessStore.Serialize(registry);
                File.WriteAllText(path, text, Utf8NoBom);
                Plugin.Log?.LogInfo("endless: saved " + (registry?.Count ?? 0)
                    + " station(s) to " + path);
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning("endless: save failed: " + ex.Message);
            }
        }

        internal static void Load(EndlessRegistry registry, string slot)
        {
            var path = FileFor(slot);
            try
            {
                if (!File.Exists(path))
                {
                    EndlessStore.Load(registry, "");
                    Plugin.Log?.LogInfo("endless: no saved stations for slot '"
                        + Sanitize(slot) + "' (" + path + ")");
                    return;
                }

                var text = File.ReadAllText(path, Utf8NoBom);
                EndlessStore.Load(registry, text);
                Plugin.Log?.LogInfo("endless: loaded " + (registry?.Count ?? 0)
                    + " station(s) for slot '" + Sanitize(slot) + "' from " + path);
            }
            catch (Exception ex)
            {
                // Нечитаемый файл: пустой реестр + warning, файл оставляем как есть.
                EndlessStore.Load(registry, "");
                Plugin.Log?.LogWarning("endless: cannot read " + path + " (" + ex.Message
                    + "); starting empty, file left untouched");
            }
        }
    }
}
