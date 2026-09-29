namespace GK2EndlessCrafting.Core
{
    public enum Lang { En, Ru }

    public static class EndlessText
    {
        public static string Infinity(Lang lang) => "∞";

        // Подпись в строке геймпад-подсказок окна крафта (рядом с «+»/«-»). Словом, а не «∞»:
        // шрифт подсказок может не уметь U+221E.
        public static string GamepadTip(Lang lang)
            => lang == Lang.Ru ? "Бесконечно" : "Endless";

        public static string HintOn(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: включён" : "Endless crafting: on";

        public static string HintOff(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: выключен" : "Endless crafting: off";
    }
}
