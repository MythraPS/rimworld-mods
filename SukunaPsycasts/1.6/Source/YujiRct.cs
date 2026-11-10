using Verse;

namespace SukunaPsycasts
{
    /// <summary>Yuji's reverse cursed technique (her call 2026-11-01: at least strong enough, like Gojo's, to
    /// stand inside Malevolent Shrine). Malevolent Shrine cuts for 12 x the King's power every half second,
    /// 24 x power a second. Yuji takes a quarter of that (6 x power a second) and heals eight times as fast
    /// as anyone else: 0.48 x psyfocus x his own power a tick, 28.8 a second at full psyfocus and power 1.</summary>
    public static class YujiRct
    {
        public const float SlashTaken = 0.25f;
        public const float Heal = 8f;

        public static float HealFactor(Pawn p) => Yuji.Is(p) ? Heal : 1f;
    }
}
