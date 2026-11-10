using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Vanilla fire dies without fuel and barely scratches anything non-flammable, so
    /// a Fire Arrow on bare dirt left nothing behind and a mech standing in the blast
    /// walked out of it. This keeps the area alight regardless of what is there to
    /// burn, holds the ground at furnace temperature, and burns what stands in it —
    /// steel and mechanoids included.
    /// </summary>
    public class FurnaceGround : GameComponent
    {
        private class Burn
        {
            public Map map;
            public Pawn caster;
            public List<IntVec3> cells = new List<IntVec3>();
            public int until;
            public float damage;
            public int lit;   // new fires this second
        }

        /// <summary>The blast's own burn (her report 2026-11-08: lag "when it blows"). Each cell burns when the
        /// explosion's wave reaches it, at the game's own blast pace, instead of every cell in one frame.
        /// Not saved: it lasts as long as the wave, a second or two.</summary>
        private class Wave
        {
            public Map map;
            public Pawn caster;
            public IntVec3 centre;
            public float damage;
            public int start, next;
            public List<IntVec3> cells = new List<IntVec3>();   // nearest first
        }

        private static readonly List<Wave> waves = new List<Wave>();
        private const float TicksPerCell = 1.5f;   // vanilla Explosion: 1.5 ticks per cell of distance

        /// <summary>Per second, per thing standing in the fire. Heat armour applies —
        /// see the AP below — so this is what gets through, not what is dealt.</summary>
        private const float BurnAP = 0.75f;

        private static readonly List<Burn> burns = new List<Burn>();
        private static readonly List<Thing> things = new List<Thing>();
        /// <summary>New fires per burn per second (2026-11-08, lag): the third-alight rule otherwise lit
        /// hundreds at once on a large blast.</summary>
        private const int MaxNewFires = 40;

        public FurnaceGround(Game game)
        {
            burns.Clear();
            waves.Clear();
            SlashUtility.ResetSession();
            ComboTracker.Reset();
        }

        public static void Ignite(Map map, IntVec3 centre, float radius, int ticks,
                                  Pawn caster, float damagePerSecond)
        {
            if (map == null) return;
            var b = new Burn
            {
                map = map, caster = caster, damage = damagePerSecond,
                until = Find.TickManager.TicksGame + ticks,
            };
            foreach (var c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                if (caster != null && c.DistanceTo(caster.Position) < 4f) continue;
                b.cells.Add(c);
            }
            burns.Add(b);
        }

        /// <summary>Burns everything in the blast, cell by cell as the wave arrives: heat, and burn damage to
        /// every pawn (half the blast's damage) and every object (six tenths).</summary>
        public static void Blast(Map map, IntVec3 centre, float radius, Pawn caster, float damage)
        {
            if (map == null) return;
            var w = new Wave { map = map, caster = caster, centre = centre, damage = damage, start = Find.TickManager.TicksGame };
            foreach (var c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                if (caster != null && c.DistanceTo(caster.Position) < 4f) continue;
                w.cells.Add(c);
            }
            waves.Add(w);
        }

        private static void TickWaves(int now)
        {
            for (int i = waves.Count - 1; i >= 0; i--)
            {
                var w = waves[i];
                if (w.map == null || !Find.Maps.Contains(w.map)) { waves.RemoveAt(i); continue; }
                float reach = (now - w.start) / TicksPerCell;
                while (w.next < w.cells.Count && (w.cells[w.next] - w.centre).LengthHorizontal <= reach)
                    BurnCell(w, w.cells[w.next++]);
                if (w.next >= w.cells.Count) waves.RemoveAt(i);
            }
        }

        private static void BurnCell(Wave w, IntVec3 cell)
        {
            GenTemperature.PushHeat(cell, w.map, 260f);
            things.Clear();
            things.AddRange(cell.GetThingList(w.map));
            foreach (var thing in things)
            {
                if (thing == w.caster || thing.Destroyed) continue;
                if (thing is Pawn victim)
                {
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Burn, w.damage * 0.5f, BurnAP, -1f, w.caster));
                    SlashUtility.NotifyAttacked(victim, w.caster);
                }
                else if (thing.def.useHitPoints)
                {
                    thing.TakeDamage(new DamageInfo(DamageDefOf.Burn, w.damage * 0.6f, BurnAP, -1f, w.caster));
                }
            }
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (waves.Count > 0) TickWaves(now);
            int slot = now % 60;   // each cell once a second, a sixtieth of them on each tick (2026-11-09)

            for (int i = burns.Count - 1; i >= 0; i--)
            {
                var b = burns[i];
                // a closed map's grids are freed: spawning there crashed the game (her crash 2026-11-09)
                if (b.map == null || now > b.until || !Find.Maps.Contains(b.map)) { burns.RemoveAt(i); continue; }

                if (slot == 0) b.lit = 0;   // new fires are counted per second
                for (int k = slot; k < b.cells.Count; k += 60)
                {
                    var c = b.cells[k];
                    if (!c.InBounds(b.map)) continue;

                    // Keep roughly a third of the area alight at any moment, and hold
                    // the whole of it at furnace temperature the entire time.
                    if (b.lit < MaxNewFires && Rand.Chance(0.34f) && c.GetFirstThing(b.map, ThingDefOf.Fire) == null)
                    {
                        b.lit++;
                        var fire = (Fire)ThingMaker.MakeThing(ThingDefOf.Fire);
                        fire.fireSize = Rand.Range(0.6f, 1.4f);
                        fire.instigator = b.caster;   // spreads and attaches carry this on (OwnFireImmunity)
                        GenSpawn.Spawn(fire, c, b.map, Rot4.North);
                    }
                    GenTemperature.PushHeat(c, b.map, 90f);

                    // Flame damage is worthless against anything that does not catch,
                    // which is exactly the list the user cares about — mechanoids,
                    // walls, ship chunks. Burn them directly instead.
                    things.Clear();
                    things.AddRange(c.GetThingList(b.map));
                    foreach (var thing in things)
                    {
                        if (thing.Destroyed || thing == b.caster) continue;
                        if (thing is Fire || thing is Filth || thing is Mote) continue;
                        if (thing is Pawn p)
                        {
                            if (SlashUtility.HasFurnaceImmunity(p)) continue;
                            p.TakeDamage(new DamageInfo(
                                DamageDefOf.Burn, b.damage, BurnAP, -1f, b.caster));
                            SlashUtility.NotifyAttacked(p, b.caster);
                        }
                        else if (thing.def.useHitPoints)
                        {
                            thing.TakeDamage(new DamageInfo(
                                DamageDefOf.Burn, b.damage, BurnAP, -1f, b.caster));
                        }
                    }
                }
            }
        }
    }
}
