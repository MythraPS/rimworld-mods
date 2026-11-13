using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    /// <summary>
    /// The fire bow faces what the caster aims at (her reports 2026-11-06..08). VEF keeps a cast's targets
    /// on CompAbilities (currentlyCasting / currentlyCastingTargets) for the whole warmup, and the job's
    /// targetA holds the first one; the ability's verb target, read before, stays empty (cell 0,0), which
    /// is why the bow kept pointing at the map's bottom-left corner. The texture points north and
    /// exactRotation turns it clockwise, which is how AngleFlat measures.
    /// </summary>
    public static class FireBowAim
    {
        public static bool TryAim(Pawn p, VEFAbility ability, out Vector3 at)
        {
            at = default;
            var comp = p.GetComp<CompAbilities>();
            var targets = comp != null && comp.currentlyCasting == ability ? comp.currentlyCastingTargets : null;
            if (targets != null)
                foreach (var t in targets)
                    if (t.IsValid && t.IsMapTarget && t.Map == p.Map)
                    {
                        at = t.HasThing ? t.Thing.DrawPos : t.Cell.ToVector3Shifted();
                        return true;
                    }
            var job = p.CurJob;
            if (job != null && job.targetA.IsValid && job.targetA.Thing != p)
            {
                at = job.targetA.CenterVector3;
                return true;
            }
            return false;
        }

        public static void Aim(Pawn p, VEFAbility ability, Mote mote)
        {
            if (p == null || ability == null || mote == null || !TryAim(p, ability, out var at)) return;
            var dir = at - p.DrawPos;
            if (dir.sqrMagnitude < 0.01f) return;
            mote.exactRotation = dir.AngleFlat();
        }
    }

    /// <summary>
    /// Aims a warmup mote the moment VEF makes it, for any caster (the per-tick re-aim in WarmupMoteAim
    /// only runs for pawns with a Sukuna or Yuji condition). VEF assigns Ability.warmupMote after this
    /// returns, so the caster is found by its current cast and position.
    /// </summary>
    [HarmonyPatch(typeof(MoteMaker), nameof(MoteMaker.MakeStaticMote),
        new Type[] { typeof(Vector3), typeof(Map), typeof(ThingDef), typeof(float), typeof(bool), typeof(float) })]
    public static class Patch_AimWarmupMoteOnSpawn
    {
        public static void Postfix(Mote __result, Vector3 loc, Map map, ThingDef moteDef)
        {
            if (__result == null || map == null || moteDef == null) return;
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                var a = p.GetComp<CompAbilities>()?.currentlyCasting;
                if (a?.def?.warmupMote != moteDef) continue;
                if ((p.DrawPos - loc).MagnitudeHorizontalSquared() > 9f) continue;
                FireBowAim.Aim(p, a, __result);
                return;
            }
        }
    }
}
