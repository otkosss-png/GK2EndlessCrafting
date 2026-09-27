namespace GK2EndlessCrafting.Core
{
    public enum Lang { En, Ru }

    public static class EndlessText
    {
        public static string Infinity(Lang lang) => "∞";

        public static string HintOn(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: включён" : "Endless crafting: on";

        public static string HintOff(Lang lang)
            => lang == Lang.Ru ? "Бесконечный крафт: выключен" : "Endless crafting: off";
    }
}
