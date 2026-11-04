using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Blue: on arrival it becomes a point that pulls everything nearby in.</summary>
    public class Projectile_Blue : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var map = Map; var cell = Position; var caster = launcher as Pawn;
            base.Impact(hitThing, blockedByShield);
            OrbTracker.For(map)?.AddBlue(cell, caster);
        }

        /// <summary>Which way it is flying, flat.</summary>
        public Vector3 Heading => ExactRotation * Vector3.forward;
    }

    /// <summary>
    /// Red, let go. It does not burst on arrival (her call 2026-10-28): it stays where
    /// it lands as a <see cref="HeldRed"/> until something pops it or a Blue reaches
    /// it and the two become Purple. Aimed at a person, it lands on them and pops at once.
    /// </summary>
    public class Projectile_Red : Projectile
    {
        public const float Fling = 5f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            var map = Map; var cell = Position; var caster = launcher as Pawn;
            base.Impact(hitThing, blockedByShield);
            if (map == null) return;
            var held = (HeldRed)ThingMaker.MakeThing(GojoDefOf.Gojo_RedOrbHeld);
            held.caster = caster;
            held.boost = Gojo.PsyfocusBoost(caster);
            GenSpawn.Spawn(held, cell, map);
        }

        /// <summary>boost = 1 + the psyfocus Gojo had when the Red landed (her call 2026-11-04):
        /// a full bar makes the burst twice as wide.</summary>
        public static void Burst(Pawn caster, IntVec3 centre, Map map, float boost = 1f)
        {
            if (map == null) return;
            float power = Gojo.Power(caster);
            float radius = GojoAbilityDefOf.Gojo_Red.radius * Mathf.Sqrt(power) * Mathf.Max(1f, boost);
            FleckMaker.Static(centre, map, FleckDefOf.ExplosionFlash, radius * 2f);
            Fx.RedBlast(centre.ToVector3Shifted(), map, radius);
            var hit = new HashSet<Thing>();   // a big building covers many cells but is hit once
            foreach (var c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                foreach (var thing in c.GetThingList(map).ToList())
                {
                    if (thing == caster || thing.Destroyed || !hit.Add(thing)) continue;
                    if (thing is Pawn victim)
                    {
                        victim.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, OrbTracker.RedDamage * power, 0.35f, -1f, caster));
                        Fx.NotifyAttacked(victim, caster);
                        if (victim.Spawned)
                        {
                            var away = (victim.Position - centre).ToVector3();
                            if (away.sqrMagnitude < 0.01f) away = Rand.InsideUnitCircleVec3;
                            Fx.Throw(victim, Fx.Along(victim.Position, away, Fling * power, map));
                        }
                    }
                    else if (thing.def.useHitPoints && (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant))
                        thing.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, OrbTracker.RedBuildingDamage * power, 0.35f, -1f, caster));
                }
            }
        }
    }

    /// <summary>
    /// A Red waiting where it landed. It pops (the normal Red burst) when anyone but
    /// Gojo touches it or a shot flies through it. A Blue of his that reaches it turns
    /// both into Purple (OrbTracker.Fuse).
    /// </summary>
    public class HeldRed : ThingWithComps
    {
        public Pawn caster;
        public float boost = 1f;
        public const float TouchRadius = 1.2f;

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned || !this.IsHashIntervalTick(5)) return;
            var map = Map;
            foreach (var c in GenRadial.RadialCellsAround(Position, TouchRadius, true))
            {
                if (!c.InBounds(map)) continue;
                foreach (var t in c.GetThingList(map))
                {
                    if (t is Pawn p && p != caster && !p.Dead) { Pop(); return; }
                    if (t is Projectile pr && !(pr is Projectile_Blue) && !(pr is Projectile_Red) && !(pr is Projectile_Purple)) { Pop(); return; }
                }
            }
        }

        public void Pop()
        {
            if (!Spawned) return;
            var map = Map; var at = Position; var by = caster; float b = boost;
            Destroy();
            Projectile_Red.Burst(by, at, map, b);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref caster, "gojoCaster");
            Scribe_Values.Look(ref boost, "gojoBoost", 1f);
        }
    }

    /// <summary>Purple. The erasing happens every tick of the flight (OrbTracker),
    /// not on impact; reaching the end of its range is all Impact means here.</summary>
    public class Projectile_Purple : Projectile
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            base.Impact(hitThing, blockedByShield);
        }
    }

    /// <summary>
    /// Where a Blue tore the ground up: a crater drawn under everything, as wide as
    /// the pull. It fades over its last day and is gone after <see cref="Lifetime"/>.
    /// </summary>
    public class BlueCrater : Thing
    {
        public float radius = 4f;
        private int born = -1;
        public const int Lifetime = 60000 * 3;
        private const int FadeTicks = 60000;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (born < 0) born = Find.TickManager.TicksGame;
        }

        public override void TickRare()
        {
            // no base call: Thing.TickRare isn't implemented in 1.6 and throws
            if (Find.TickManager.TicksGame - born > Lifetime) Destroy();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var mat = def.graphic?.MatSingle;
            if (mat == null) return;
            int left = Lifetime - (Find.TickManager.TicksGame - born);
            if (left < FadeTicks) mat = FadedMaterialPool.FadedVersionOf(mat, Mathf.Clamp01(left / (float)FadeTicks));
            var pos = drawLoc; pos.y = AltitudeLayer.FloorEmplacement.AltitudeFor() - 0.01f;
            float s = radius * 2f + 1f;
            Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.Euler(0f, (thingIDNumber * 37) % 360, 0f), new Vector3(s, 1f, s)), mat, 0);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref radius, "gojoCraterRadius", 4f);
            Scribe_Values.Look(ref born, "gojoCraterBorn", -1);
        }
    }

    /// <summary>
    /// Per map: Blue's pull fields, Purple's erasing as it flies, and fusion. Fusion
    /// happens wherever any Blue meets any Red (her call 2026-11-02: a stray one is
    /// enough): a flying Blue passing a Red, or a Red, flying or landed, inside a Blue.
    /// A Red waits where it lands until popped; a Blue pulls for four seconds and then
    /// lingers, spent, for <see cref="LingerTicks"/>, still able to catch a Red.
    /// </summary>
    public class OrbTracker : MapComponent
    {
        private class BlueField : IExposable
        {
            public IntVec3 cell; public Pawn caster; public int ticksLeft;
            public float radius;   // 0 in saves from before 2026-11-04: use the plain radius
            public bool Pulling => ticksLeft > LingerTicks;
            public void ExposeData()
            {
                Scribe_Values.Look(ref cell, "cell");
                Scribe_References.Look(ref caster, "caster");
                Scribe_Values.Look(ref ticksLeft, "ticksLeft");
                Scribe_Values.Look(ref radius, "radius", 0f);
            }
        }
        private List<BlueField> fields = new List<BlueField>();

        public const int BlueTicks = 240;
        /// <summary>How long a spent Blue stays after its pull: one in-game hour.</summary>
        public const int LingerTicks = 2500;
        public const float BlueDamage = 10f;
        public const float BlueBuildingDamage = 40f;
        public const int BluePulses = BlueTicks / 30;   // one pull every 30 ticks
        // Red hits once for twice what a whole Blue does (her call 2026-10-31).
        public const float RedDamage = 2f * BlueDamage * BluePulses;
        public const float RedBuildingDamage = 2f * BlueBuildingDamage * BluePulses;
        public const float PurpleRadius = 1.9f;
        public const float PurpleDamage = 120f;
        public const float FuseDistance = 1.6f;

        public OrbTracker(Map map) : base(map) { }

        public static OrbTracker For(Map map) => map?.GetComponent<OrbTracker>();

        public void AddBlue(IntVec3 cell, Pawn caster)
        {
            // wider with more psyfocus in the pool when it lands (her call 2026-11-04)
            float r = Radius(caster) * Gojo.PsyfocusBoost(caster);
            fields.Add(new BlueField { cell = cell, caster = caster, ticksLeft = BlueTicks + LingerTicks, radius = r });
            Crater(cell, r);
        }

        public override void MapComponentTick()
        {
            if (fields.Count > 0) TickFields();
            var purples = map.listerThings.ThingsOfDef(GojoDefOf.Gojo_PurpleOrb);
            for (int i = purples.Count - 1; i >= 0; i--)
                if (purples[i] is Projectile p && p.Spawned) Erase(p);
            if (Find.TickManager.TicksGame % 3 == 0) Fuse();
        }

        // ---------------------------------------------------------------- Blue

        private static float Radius(Pawn caster) => GojoAbilityDefOf.Gojo_Blue.radius * Mathf.Sqrt(Gojo.Power(caster));
        private static float Radius(BlueField f) => f.radius > 0f ? f.radius : Radius(f.caster);

        private void TickFields()
        {
            bool pull = Find.TickManager.TicksGame % 30 == 0;
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var f = fields[i];
                f.ticksLeft--;
                if (f.Pulling)
                {
                    if (Find.TickManager.TicksGame % 6 == 0)
                        Fx.Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, f.cell.ToVector3Shifted(), map, Rand.Range(1.4f, 2.2f), Rand.Range(0f, 360f));
                    if (pull) Pull(f);
                }
                else if (Find.TickManager.TicksGame % 20 == 0)   // spent: a small, slow swirl so it can still be found
                    Fx.Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, f.cell.ToVector3Shifted(), map, Rand.Range(0.6f, 0.9f), Rand.Range(0f, 360f));
                if (f.ticksLeft <= 0) fields.RemoveAt(i);
            }
        }

        /// <summary>
        /// Everything in reach is dragged toward the centre hard enough to be hurt by
        /// it (her call 2026-10-28): people thrown in and crushed, loose items dragged
        /// in and battered, buildings, walls and plants torn at, harder the closer they
        /// are. Gojo and people of his faction are left alone.
        /// </summary>
        private void Pull(BlueField f)
        {
            float power = Gojo.Power(f.caster);
            float radius = Radius(f);
            var things = GenRadial.RadialDistinctThingsAround(f.cell, map, radius, true).ToList();
            foreach (var thing in things)
            {
                if (thing.Destroyed || thing == f.caster || !thing.Spawned) continue;
                float near = 1f - Mathf.Clamp01(thing.Position.DistanceTo(f.cell) / (radius + 1f)) * 0.6f;
                if (thing is Pawn victim)
                {
                    if (f.caster != null && victim.Faction != null && victim.Faction == f.caster.Faction) continue;
                    if (victim.Position.DistanceTo(f.cell) > 1.5f)
                    {
                        var dir = (f.cell - victim.Position).ToVector3();
                        Fx.Throw(victim, Fx.Along(victim.Position, dir, victim.Position.DistanceTo(f.cell) - 1f, map));
                    }
                    victim.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, BlueDamage * power * near, 0.3f, -1f, f.caster));
                    Fx.NotifyAttacked(victim, f.caster);
                }
                else if (thing.def.category == ThingCategory.Item)
                {
                    if (thing.def.useHitPoints)
                        thing.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, BlueDamage * power * near, 0f, -1f, f.caster));
                    if (!thing.Destroyed && thing.Spawned && thing.Position.DistanceTo(f.cell) > 1.5f)
                    {
                        var t = thing; var from = t.Position; t.DeSpawn();
                        if (!GenPlace.TryPlaceThing(t, f.cell, map, ThingPlaceMode.Near)) GenSpawn.Spawn(t, from, map);
                    }
                }
                else if (thing.def.useHitPoints && thing.def != GojoDefOf.Gojo_DomainBarrier
                         && (thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Plant))
                    thing.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, BlueBuildingDamage * power * near, 0.3f, -1f, f.caster));
            }
        }

        /// <summary>The ground is pulled up too: built floors in the inner part are
        /// torn off, rubble is scattered, and a crater is left behind.</summary>
        private void Crater(IntVec3 centre, float radius)
        {
            if (!centre.InBounds(map)) return;
            foreach (var c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                float d = c.DistanceTo(centre);
                if (d <= radius * 0.6f && map.terrainGrid.CanRemoveTopLayerAt(c))
                    map.terrainGrid.RemoveTopLayer(c, false);
                if (Rand.Chance(0.55f - d / radius * 0.4f))
                    FilthMaker.TryMakeFilth(c, map, Rand.Chance(0.5f) ? ThingDefOf.Filth_RubbleRock : ThingDefOf.Filth_Dirt);
            }
            var crater = (BlueCrater)ThingMaker.MakeThing(GojoDefOf.Gojo_BlueCrater);
            crater.radius = radius;
            GenSpawn.Spawn(crater, centre, map);
        }

        // ---------------------------------------------------------------- Purple

        private void Erase(Projectile orb)
        {
            var caster = orb.Launcher as Pawn;
            var pos = orb.ExactPosition;
            Fx.Fleck(GojoDefOf.Gojo_Fleck_PurpleTrail, pos, map, 2.4f, Rand.Range(0f, 360f));
            EraseCircle(caster, pos.ToIntVec3(), PurpleRadius * Mathf.Sqrt(Gojo.Power(caster)), orb);
        }

        /// <summary>Everything in the circle is erased: people take Purple's damage
        /// (their bodies vanish if it kills them), everything else that can be
        /// destroyed is gone, roofs too. Gojo and his waiting Reds are spared.</summary>
        private void EraseCircle(Pawn caster, IntVec3 centre, float radius, Thing skip)
        {
            var orb = skip;
            float power = Gojo.Power(caster);
            foreach (var c in GenRadial.RadialCellsAround(centre, radius, true))
            {
                if (!c.InBounds(map)) continue;
                foreach (var thing in c.GetThingList(map).ToList())
                {
                    if (thing == orb || thing == caster || thing.Destroyed) continue;
                    // someone in mid-air (thrown by a Blue) is inside a flyer: drop them here first
                    if (thing is PawnFlyer flyer)
                    {
                        var flying = flyer.FlyingPawn;
                        if (flying != null && flying != caster)
                        {
                            flyer.GetDirectlyHeldThings().TryDrop(flying, c, map, ThingPlaceMode.Near, out _);
                            if (flying.Spawned) Vaporize(flying, caster);
                        }
                        if (!flyer.Destroyed) flyer.Destroy(DestroyMode.Vanish);
                        continue;
                    }
                    if (thing.def.category == ThingCategory.Mote || thing.def.category == ThingCategory.Projectile
                        || thing.def.category == ThingCategory.Ethereal) continue;
                    if (thing is Pawn victim)
                    {
                        Vaporize(victim, caster);
                        continue;
                    }
                    if (thing is HeldRed) continue;
                    if (thing.def.destroyable) thing.Destroy(DestroyMode.Vanish);
                }
                if (map.roofGrid.Roofed(c)) map.roofGrid.SetRoof(c, null);
            }
        }

        /// <summary>Purple erases people outright (her report 2026-11-04: some walked out of it).</summary>
        private static void Vaporize(Pawn victim, Pawn caster)
        {
            if (victim.Dead) { victim.Corpse?.Destroy(); return; }
            victim.Kill(new DamageInfo(GojoDefOf.Gojo_Erase, PurpleDamage, 2f, -1f, caster));
            victim.Corpse?.Destroy();
        }

        // ---------------------------------------------------------------- fusion

        private void Fuse()
        {
            var flyingReds = new List<Projectile>();
            foreach (var t in map.listerThings.ThingsOfDef(GojoDefOf.Gojo_RedOrb)) if (t is Projectile r) flyingReds.Add(r);
            foreach (var t in map.listerThings.ThingsOfDef(GojoDefOf.Gojo_RedOrbSlow)) if (t is Projectile r) flyingReds.Add(r);
            var heldReds = map.listerThings.ThingsOfDef(GojoDefOf.Gojo_RedOrbHeld);
            if (flyingReds.Count == 0 && heldReds.Count == 0) return;

            // A flying Blue passing any Red: Purple where the Red was.
            foreach (var def in new[] { GojoDefOf.Gojo_BlueOrb, GojoDefOf.Gojo_BlueOrbSlow })
            {
                var blues = map.listerThings.ThingsOfDef(def);
                for (int i = blues.Count - 1; i >= 0; i--)
                {
                    if (!(blues[i] is Projectile b) || b.Destroyed) continue;
                    Thing red = Nearest(b.ExactPosition, flyingReds, heldReds, out float dist);
                    if (red == null || dist > FuseDistance) continue;
                    var at = RedCell(red);
                    b.Destroy(); red.Destroy();
                    var bc = b.Launcher as Pawn;
                    MakePurple(bc, at, Radius(bc) * Gojo.PsyfocusBoost(bc));
                }
            }

            // Any Red, flying or landed, inside a Blue (pulling or spent): Purple at the Blue.
            for (int i = fields.Count - 1; i >= 0; i--)
            {
                var f = fields[i];
                Thing red = Nearest(f.cell.ToVector3Shifted(), flyingReds, heldReds, out float dist);
                if (red == null || dist > Radius(f)) continue;
                red.Destroy();
                fields.RemoveAt(i);
                MakePurple(f.caster, f.cell, Radius(f));
            }
        }

        private static IntVec3 RedCell(Thing red) => red is Projectile rp ? rp.ExactPosition.ToIntVec3() : red.Position;

        private static Thing Nearest(Vector3 from, List<Projectile> flyingReds, List<Thing> heldReds, out float dist)
        {
            Thing best = null; dist = float.MaxValue;
            foreach (var r in flyingReds)
            {
                if (r.Destroyed) continue;
                float d = (r.ExactPosition - from).MagnitudeHorizontal();
                if (d < dist) { dist = d; best = r; }
            }
            foreach (var h in heldReds)
            {
                if (h.Destroyed) continue;
                float d = (h.DrawPos - from).MagnitudeHorizontal();
                if (d < dist) { dist = d; best = h; }
            }
            return best;
        }

        /// <summary>Blue and Red meeting: Purple bursts where they meet instead of
        /// flying on (her call 2026-10-31), erasing a circle as wide as Blue's pull.
        /// It is not the Purple psycast, so it costs nothing and ignores its cooldown.</summary>
        /// <summary>As wide as the Blue that made it.</summary>
        private void MakePurple(Pawn caster, IntVec3 at, float radius)
        {
            if (!at.InBounds(map)) return;
            FleckMaker.Static(at, map, FleckDefOf.ExplosionFlash, radius * 2f);
            foreach (var c in GenRadial.RadialCellsAround(at, radius, true))
                if (c.InBounds(map) && Rand.Chance(0.35f))
                    Fx.Fleck(GojoDefOf.Gojo_Fleck_PurpleTrail, c.ToVector3Shifted(), map, Rand.Range(1.6f, 2.6f), Rand.Range(0f, 360f));
            EraseCircle(caster, at, radius, null);
            if (caster?.Faction == Faction.OfPlayer)
                Messages.Message("Blue and Red meet: Hollow Purple.", new TargetInfo(at, map), MessageTypeDefOf.NeutralEvent, false);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref fields, "gojoBlueFields", LookMode.Deep);
            if (fields == null) fields = new List<BlueField>();
            fields.RemoveAll(f => f == null || !f.cell.IsValid);
        }
    }
}
