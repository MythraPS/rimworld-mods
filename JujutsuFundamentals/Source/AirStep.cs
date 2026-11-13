using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Air Step (her call 2026-11-10: a Cursed Energy Fundamentals node at level 2, after Cursed Energy
    /// Reinforcement). Moved here from the Sukuna mod: cursed energy pushed through the soles, a long leap
    /// to a chosen spot, over walls and water but never over a mountain or into a building. More psychic
    /// sensitivity: farther (range x sensitivity) and sooner again (cooldown / sensitivity).
    /// Colonists only jump when ordered; other pawns use it on their own.
    /// </summary>
    public class Ability_AirStep : Ability
    {
        public const float MinSensitivity = 0.25f;

        public override int GetCooldownForPawn() =>
            Mathf.RoundToInt(base.GetCooldownForPawn() / Mathf.Max(MinSensitivity, Jujutsu.Sensitivity(pawn)));

        private bool Mine => pawn != null && pawn.IsColonistPlayerControlled;
        public override bool CanAutoCast => !Mine && base.CanAutoCast;
        public override bool AutoCast => !Mine && base.AutoCast;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length == 0 || pawn.Map == null) return;
            var cell = targets[0].Cell;
            var map = pawn.Map;
            if (!cell.InBounds(map) || cell == pawn.Position || Problem(pawn, cell) != null) return;
            Burst(pawn);
            var flyer = PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer, pawn, cell, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, pawn.Position, map);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            var map = pawn.Map;
            string why = map == null || !target.Cell.InBounds(map) ? "Nowhere to land there." : Problem(pawn, target.Cell);
            if (why != null)
            {
                if (showMessages) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        private static FleckDef burst;

        /// <summary>A small blast of cursed energy at the feet as they push off (her ask 2026-11-11), in the pawn's
        /// energy colour.</summary>
        public static void Burst(Pawn p)
        {
            if (p?.Map == null) return;
            burst = burst ?? DefDatabase<FleckDef>.GetNamedSilentFail("JJK_Fleck_AirStepBurst");
            if (burst == null) return;
            var data = FleckMaker.GetDataStatic(p.DrawPos + new Vector3(0f, 0f, -0.35f), p.Map, burst, 1f);
            data.instanceColor = Patch_WeaponGlow.EnergyColor(p);
            p.Map.flecks.CreateFleck(data);
        }

        /// <summary>Over buildings but never into one, and never over a mountain. Null when the jump is fine.</summary>
        public static string Problem(Pawn pawn, IntVec3 cell)
        {
            var map = pawn.Map;
            if (!cell.Standable(map) || cell.GetEdifice(map) != null) return "Nowhere to land there.";
            foreach (var c in GenSight.PointsOnLineOfSight(pawn.Position, cell))
            {
                if (!c.InBounds(map)) continue;
                var roof = c.GetRoof(map);
                var e = c.GetEdifice(map);
                if ((roof != null && roof.isThickRoof) || (e != null && e.def.building != null && e.def.building.isNaturalRock))
                    return "A mountain is in the way.";
            }
            return null;
        }
    }
}
