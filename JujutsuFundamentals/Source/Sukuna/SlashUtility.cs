using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    public static class SlashUtility
    {
        /// <summary>Any slash landing on a corpse reduces it to meat. Shrine, Cleave,
        /// Dismantle and World Cutting Slash all route through here.</summary>
        public static void ButcherCorpse(Corpse corpse, Pawn caster)
        {
            if (corpse == null || corpse.Destroyed || corpse.Map == null) return;
            var inner = corpse.InnerPawn;
            var map = corpse.Map;
            var at = corpse.Position;

            ThingDef meatDef = inner?.RaceProps?.meatDef;
            int count = inner == null ? 0 : Mathf.Max(1, GenMath.RoundRandom(
                            inner.GetStatValue(StatDefOf.MeatAmount) * 0.6f));

            corpse.Destroy(DestroyMode.KillFinalize);
            if (meatDef == null || count <= 0) return;

            var meat = ThingMaker.MakeThing(meatDef);
            meat.stackCount = count;
            GenPlace.TryPlaceThing(meat, at, map, ThingPlaceMode.Near);
            FleckMaker.Static(at, map, FleckDefOf.ExplosionFlash, 1.2f);
        }

        /// <summary>Translucent slash arc, drawn at a random angle so repeated cuts
        /// do not look stamped.</summary>
        public static void SlashFleck(Thing at)
        {
            if (at?.Map == null) return;
            SlashFleckAt(at.Position, at.Map);
        }

        public static void SlashFleckAt(IntVec3 cell, Map map)
            => Throw(cell.ToVector3Shifted(), map, "Sukuna_Fleck_Slash", Rand.Range(1.1f, 1.7f));

        /// <summary>A cut that keeps the angle it travelled at. Dismantle's slash must
        /// line up with its flight — only the Shrine's spray is randomised.</summary>
        public static void SlashFleckAngled(Vector3 loc, Map map, float angle, float scale = 1.4f)
            => Throw(loc, map, "Sukuna_Fleck_Slash", scale, angle);

        public static void WorldSlashFleckAngled(Vector3 loc, Map map, float angle, float scale)
            => Throw(loc, map, "Sukuna_Fleck_WorldSlash", scale, angle);

        /// <summary>The single blade riding the leading edge of a World Cutting Slash.
        /// Short-lived on purpose — one cut moving, not a trail of them.</summary>
        public static void WorldSlashHead(Vector3 loc, Map map, float angle, float scale = 1f)
            => Throw(loc, map, "Sukuna_Fleck_WorldSlashHead", scale, angle);

        /// <summary>Small, for the Shrine — packed and numerous rather than a few
        /// large arcs.</summary>
        public static void SmallSlashFleckAt(IntVec3 cell, Map map)
            => Throw(cell.ToVector3Shifted(), map, "Sukuna_Fleck_Slash", Rand.Range(0.45f, 0.8f));

        /// <summary>
        /// Damage applied outside a normal verb does not by itself make a pawn react.
        /// This is what makes victims register that they were attacked, and by whom.
        /// </summary>
        public static void NotifyAttacked(Pawn victim, Pawn attacker)
        {
            if (victim == null || attacker == null || victim == attacker) return;
            try
            {
                victim.mindState?.Notify_DamageTaken(new DamageInfo(
                    SukunaDefOf.Sukuna_Slash, 0f, 0f, -1f, attacker));
                if (victim.mindState != null && victim.mindState.enemyTarget == null
                    && victim.Faction != attacker.Faction)
                    victim.mindState.enemyTarget = attacker;
            }
            catch { /* reaction is a nicety; never let it break the hit */ }
        }

        /// <summary>The big one, for World Cutting Slash.</summary>
        public static void WorldSlashFleckAt(IntVec3 cell, Map map)
            => Throw(cell.ToVector3Shifted(), map, "Sukuna_Fleck_WorldSlash", Rand.Range(1.6f, 2.4f));

        /// <summary>
        /// Every slash gets its own angle. FleckMaker.Static has no rotation
        /// parameter, so the data has to be built by hand — without this they all
        /// render at the same angle and the effect looks stamped.
        /// </summary>
        private static void Throw(Vector3 loc, Map map, string defName, float scale,
                                  float? angle = null)
        {
            if (map == null) return;
            var def = DefDatabase<FleckDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            var data = FleckMaker.GetDataStatic(loc, map, def, scale);
            data.rotation = angle ?? Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        /// <summary>Black sparks spreading out of a Black Flash impact.</summary>
        public static void BlackSparks(Thing at)
        {
            if (at?.Map == null) return;
            var def = DefDatabase<FleckDef>.GetNamedSilentFail("Sukuna_Fleck_BlackSpark");
            if (def == null) return;
            // Black lightning edged in red, thrown outward from the impact.
            for (int i = 0; i < 9; i++)
            {
                var data = FleckMaker.GetDataStatic(at.DrawPos, at.Map, def, Rand.Range(0.8f, 1.9f));
                data.rotation = Rand.Range(0f, 360f);
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(1.5f, 4.5f);
                at.Map.flecks.CreateFleck(data);
            }
        }

        /// <summary>True while the pawn is inside their own Furnace blast window.
        /// Fire Arrow's flames are their own; they should not burn to death in them.</summary>
        private static readonly Dictionary<int, int> furnaceUntil = new Dictionary<int, int>();
        public static void ResetSession() => furnaceUntil.Clear();

        public static void GrantFurnaceImmunity(Pawn p, int ticks)
        {
            if (p != null) furnaceUntil[p.thingIDNumber] = Find.TickManager.TicksGame + ticks;
        }

        public static bool HasFurnaceImmunity(Pawn p) =>
            p != null && furnaceUntil.TryGetValue(p.thingIDNumber, out int until)
                      && Find.TickManager.TicksGame < until;
    }
}
