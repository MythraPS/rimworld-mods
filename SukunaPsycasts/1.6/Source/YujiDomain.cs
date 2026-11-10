using HarmonyLib;
using RimWorld;
using VEF.Abilities;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Yuji's own domain (her call 2026-11-06): the moment he learns Malevolent Shrine in his
    /// psycast tree, it becomes Benevolent Shrine instead. The tree then counts the Malevolent node as
    /// learned (Benevolent fills its slot), so he can't spend a point on it again. The lair and the hunt
    /// for Sukuna still open only after he masters Shrine and Hemosage.</summary>
    public static class YujiDomain
    {
        /// <summary>Yuji himself, not while Sukuna wears him.</summary>
        public static bool Owns(Pawn p) => p != null && Yuji.Is(p) && !SukunaBehaviour.IsSukuna(p);

        public static void Awaken(Pawn p)
        {
            bool had = SukunaArc.HasBenevolent(p);
            SukunaArc.GrantBenevolent(p);
            if (had || !p.Spawned) return;
            Find.LetterStack.ReceiveLetter("Benevolent Shrine",
                "Yuji reached for Malevolent Shrine and found something else: a domain of his own. "
                + "Domain Expansion: Benevolent Shrine replaces Malevolent Shrine. It cuts no matter at all, only the souls of "
                + "those hostile to him and his friends, and it can cut the King of Curses loose from a body he stole. "
                + "So can Yuji's fists, his slashes and his Black Flashes.",
                LetterDefOf.PositiveEvent, p);
        }
    }

    [HarmonyPatch(typeof(CompAbilities), nameof(CompAbilities.GiveAbility))]
    public static class Patch_YujiLearnsDomain
    {
        public static bool Prefix(CompAbilities __instance, VEF.Abilities.AbilityDef __0)
        {
            if (__0 != SukunaAbilityDefOf.Sukuna_MalevolentShrine || !(__instance.parent is Pawn p) || !YujiDomain.Owns(p)) return true;
            YujiDomain.Awaken(p);
            return false;
        }
    }

    [HarmonyPatch(typeof(CompAbilities), nameof(CompAbilities.HasAbility))]
    public static class Patch_YujiDomainCountsAsLearned
    {
        public static void Postfix(CompAbilities __instance, VEF.Abilities.AbilityDef __0, ref bool __result)
        {
            if (__result || __0 != SukunaAbilityDefOf.Sukuna_MalevolentShrine) return;
            if (__instance.parent is Pawn p && YujiDomain.Owns(p) && SukunaArc.HasBenevolent(p)) __result = true;
        }
    }
}
