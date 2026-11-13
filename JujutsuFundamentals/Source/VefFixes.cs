using HarmonyLib;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// A breath or beam (VEF's ExpandableProjectile: Uraume's Ice Breath, for one) whose caster has left the map while
    /// it was still going throws an error every tick: VEF reads the caster's path follower, which is gone once they're
    /// off the map (test runner 2026-11-12, Uraume following Sukuna out). It ends with them instead.
    /// </summary>
    [HarmonyPatch(typeof(VEF.Weapons.ExpandableProjectile), "TickInterval")]
    public static class Patch_BreathEndsWithCaster
    {
        public static bool Prefix(VEF.Weapons.ExpandableProjectile __instance)
        {
            if (__instance.Launcher is Pawn p && !p.Dead && p.pather == null)
            {
                if (!__instance.Destroyed) __instance.Destroy();
                return false;
            }
            return true;
        }
    }
}
