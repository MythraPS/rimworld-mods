using HarmonyLib;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// A caster never burns in their own fire (her report 2026-11-08: Yuji caught fire from his Fire
    /// Arrow's remnant fires after the 60 s immunity ran out). Fire Arrow's fires carry the caster as
    /// their instigator, and vanilla passes that on to every fire they spread to and everything they
    /// set alight, so: such a fire can't attach to them, and its damage doesn't touch them.
    /// </summary>
    [HarmonyPatch(typeof(FireUtility), nameof(FireUtility.TryAttachFire))]
    public static class Patch_OwnFireNoAttach
    {
        public static bool Prefix(Thing t, Thing instigator) => !(t is Pawn && instigator != null && t == instigator);
    }

    public static class OwnFire
    {
        public static bool Is(Pawn victim, DamageInfo dinfo) =>
            dinfo.Instigator == victim && (dinfo.Def == DamageDefOf.Flame || dinfo.Def == DamageDefOf.Burn
                                           || dinfo.Def == SukunaDefOf.Sukuna_Flame);
    }
}
