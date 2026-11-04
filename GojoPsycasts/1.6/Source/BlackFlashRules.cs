using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    /// <summary>The Black Flash mod options (her ask 2026-11-04): a chance multiplier, and
    /// whether damage that is not a melee attack (abilities) can trigger it. Gunshots never do.</summary>
    public static class BlackFlashRules
    {
        /// <summary>True while a melee attack is landing (set around Verb_MeleeAttack.TryCastShot,
        /// which applies its damage in the same call).</summary>
        public static int meleeDepth;
        public static bool InMelee => meleeDepth > 0;

        public static float Multiplier => IconSettingsMod.Settings?.blackFlashChance ?? 1f;

        public static bool Allowed(DamageInfo dinfo)
        {
            if (Multiplier <= 0f) return false;
            if (dinfo.Weapon != null && dinfo.Weapon.IsRangedWeapon && !InMelee) return false;
            if (InMelee) return true;
            return IconSettingsMod.Settings?.blackFlashAbilities ?? true;
        }

        public static float Scale(float chance) => Mathf.Clamp01(chance * Multiplier);
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_BlackFlashMeleeContext
    {
        public static void Prefix() => BlackFlashRules.meleeDepth++;
        public static void Finalizer() => BlackFlashRules.meleeDepth = Mathf.Max(0, BlackFlashRules.meleeDepth - 1);
    }
}
