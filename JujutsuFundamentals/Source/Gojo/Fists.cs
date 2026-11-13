using HarmonyLib;
using RimWorld;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Walls and other buildings (her call 2026-11-04: fists were too slow on walls).
    /// The Blue Fist bonus in Patch_PreApplyDamage only sees pawns, so a building took a plain
    /// punch. Gojo's unarmed melee hits on buildings now do 4x, more with psychic sensitivity.</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_FistsOnBuildings
    {
        public const float BuildingFactor = 4f;

        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (!(__instance is Building) || !(dinfo.Instigator is Pawn attacker)) return;
            if (dinfo.Def == null || dinfo.Def.isRanged || dinfo.Def.isExplosive) return;
            if (dinfo.Def == GojoDefOf.Gojo_Force || dinfo.Def == GojoDefOf.Gojo_Erase) return;
            bool unarmed = dinfo.Weapon == null || dinfo.Weapon == ThingDefOf.Human || dinfo.Weapon.race != null;
            if (!unarmed || attacker.health?.hediffSet?.HasHediff(GojoDefOf.Gojo_BlueFist) != true) return;
            dinfo.SetAmount(dinfo.Amount * BuildingFactor * UnityEngine.Mathf.Max(1f, Gojo.Power(attacker)));
        }
    }
}
