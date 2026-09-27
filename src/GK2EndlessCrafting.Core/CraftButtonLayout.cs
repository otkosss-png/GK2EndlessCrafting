namespace GK2EndlessCrafting.Core
{
    // Раскладка кнопки «∞» в окне крафта. Игра даёт кнопку «+» (plusCraftButton), и её же
    // клонируют другие моды (например Craft Max — ставит свою кнопку справа от «+»).
    // Поэтому если рядом уже есть кнопка чужого мода, встаём правее НЕЁ, а не «+».
    public static class CraftButtonLayout
    {
        public static float NextX(float plusX, float plusWidth, float? otherX, float otherWidth, float gap)
        {
            if (otherX.HasValue) return otherX.Value + otherWidth + gap;
            return plusX + plusWidth + gap;
        }
    }
}
