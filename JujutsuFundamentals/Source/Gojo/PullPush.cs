using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEFAbility = VEF.Abilities.Ability_Blank;

namespace GojoPsycasts
{
    /// <summary>Small, quick versions of Blue and Red (her call 2026-11-04): learning Blue
    /// also gives Pull, learning Red also gives Push (HediffComp_Limitless hands them out).
    /// They are not tree nodes, so VPE does not charge them: the cost is paid here, with
    /// VPE's psyfocus cost factor, which the Six Eyes halve.</summary>
    public abstract class Ability_Shove : VEFAbility
    {
        protected abstract float Psyfocus { get; }
        protected abstract float Heat { get; }
        protected abstract void Move(Thing t);

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var tr = pawn.psychicEntropy;
            var map = pawn.Map;
            if (tr == null || map == null) return;
            float cost = Psyfocus * pawn.GetStatValue(Gojo.CostFactorStat);
            if (tr.CurrentPsyfocus < cost)
            {
                Messages.Message("Not enough psyfocus.", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var t = Pick(targets, map);
            if (t == null) return;
            tr.OffsetPsyfocusDirectly(-cost);
            tr.TryAddEntropy(Heat, pawn, scale: true, overLimit: false);
            Move(t);
        }

        /// <summary>The thing aimed at, or the first person or loose item in the cell.</summary>
        private Thing Pick(GlobalTargetInfo[] targets, Map map)
        {
            if (targets == null || targets.Length == 0) return null;
            var g = targets[0];
            if (g.HasThing && Movable(g.Thing)) return g.Thing;
            if (!g.Cell.InBounds(map)) return null;
            return g.Cell.GetThingList(map).FirstOrDefault(Movable);
        }

        private bool Movable(Thing t) =>
            t != null && t != pawn && t.Spawned && !t.Destroyed
            && (t is Pawn || t.def.category == ThingCategory.Item);

        /// <summary>Moves an item to the nearest free spot by <paramref name="dest"/>.</summary>
        protected static void Place(Thing t, IntVec3 dest)
        {
            var map = t.Map; var from = t.Position;
            Fx.Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, from.ToVector3Shifted(), map, 0.8f);
            t.DeSpawn();
            if (!GenPlace.TryPlaceThing(t, dest, map, ThingPlaceMode.Near)) GenSpawn.Spawn(t, from, map);
        }
    }

    /// <summary>Pull: drags a person or an item straight to Gojo's side.</summary>
    public class Ability_Pull : Ability_Shove
    {
        protected override float Psyfocus => 0.01f;
        protected override float Heat => 3f;

        protected override void Move(Thing t)
        {
            var map = pawn.Map;
            Fx.Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, t.DrawPos, map, 1.2f);
            if (t is Pawn p)
            {
                var dir = (pawn.Position - p.Position).ToVector3();
                Fx.Throw(p, Fx.Along(p.Position, dir, p.Position.DistanceTo(pawn.Position) - 1f, map));
                if (p.HostileTo(pawn)) Fx.NotifyAttacked(p, pawn);
            }
            else Place(t, pawn.Position);
        }
    }

    /// <summary>Push: throws a person or an item away from Gojo. Someone thrown into a
    /// wall before the full distance is hurt by it.</summary>
    public class Ability_Push : Ability_Shove
    {
        public const float Distance = 8f;
        public const float SlamDamage = 12f;

        protected override float Psyfocus => 0.02f;
        protected override float Heat => 6f;

        protected override void Move(Thing t)
        {
            var map = pawn.Map;
            float power = Gojo.Power(pawn);
            float far = Distance * Mathf.Sqrt(power);
            var dir = (t.Position - pawn.Position).ToVector3();
            if (dir.sqrMagnitude < 0.01f) dir = Rand.InsideUnitCircleVec3;
            var dest = Fx.Along(t.Position, dir, far, map);
            Fx.Fleck(GojoDefOf.Gojo_Fleck_RedSpark, t.DrawPos, map, 1.4f);
            if (t is Pawn p)
            {
                bool blocked = dest.DistanceTo(p.Position) < far - 1.5f;
                Fx.Throw(p, dest);
                if (p.HostileTo(pawn)) Fx.NotifyAttacked(p, pawn);
                if (blocked)
                    p.TakeDamage(new DamageInfo(DamageDefOf.Blunt, SlamDamage * power, 0.2f, -1f, pawn));
            }
            else Place(t, dest);
        }
    }
}
