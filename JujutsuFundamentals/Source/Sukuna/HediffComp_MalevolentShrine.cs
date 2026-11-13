using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    public class HediffCompProperties_MalevolentShrine : HediffCompProperties
    {
        public float damageAmount = 12f;
        public int   ticksBetween = 30;
        public float radius       = 19.9f;

        public HediffCompProperties_MalevolentShrine()
            => compClass = typeof(HediffComp_MalevolentShrine);
    }

    /// <summary>
    /// The domain is anchored where it was opened. VEF's damage aura follows the
    /// caster, which made the whole thing slide around the map behind them — a domain
    /// does not move. Everything here works off a fixed cell instead, and the shrine
    /// building marks it.
    /// </summary>
    public class HediffComp_MalevolentShrine : HediffComp
    {
        private IntVec3 anchor = IntVec3.Invalid;
        private Thing shrine;
        private List<Thing> bones = new List<Thing>();
        private int damageAcc;
        private int fleckAcc;
        private JujutsuFundamentals.DomainInfo info;

        public HediffCompProperties_MalevolentShrine Props =>
            (HediffCompProperties_MalevolentShrine)props;

        /// <summary>Grows with sensitivity but hard-capped — 400 cells is the ceiling
        /// no matter how many fingers the vessel has eaten.</summary>
        public static float MaxRadius => Mathf.Min(400f, GenRadial.MaxRadialPatternRadius - 1f);
        public float Radius => Mathf.Min(Props.radius * Scaling.Power(Pawn) * Scaling.AbilityBoost(Pawn), MaxRadius);

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var pawn = Pawn;
            if (pawn?.Map == null) return;

            // Always directly beneath the pawn who opened it.
            anchor = pawn.Position;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_ShrineBuilding");
            if (def == null) return;
            shrine = GenSpawn.Spawn(ThingMaker.MakeThing(def), anchor, pawn.Map);
            RaiseBones(pawn.Map);
            // the shared clash rules: Sukuna's own domain wins every clash of two (her call 2026-11-10)
            info = JujutsuFundamentals.Domains.Make(parent, anchor, Radius, "Malevolent Shrine", SukunaBehaviour.IsSukuna(pawn));
            JujutsuFundamentals.Domains.Open(info);
        }

        /// <summary>
        /// Skulls heaped around the shrine, never on it. The shrine is a 3x3 building
        /// anchored on its centre cell, so its own footprint is the Chebyshev-1 ring
        /// and everything inside that is excluded — piling skulls over the structure
        /// hid the thing they are supposed to be piled at the foot of.
        /// </summary>
        private const int   ShrineHalf    = 1;    // the real 3x3 footprint, in cells
        private const int   ShrineArtHalf = 2;    // what the 5x5 drawSize covers
        private const float SkullRadius   = 6.5f; // how far the scan reaches

        /// <summary>Chance of a skull per ring, counting outwards from the first ring
        /// clear of the shrine art. Front-loaded on purpose: the heap is banked
        /// against the structure and thins out fast, rather than being scattered
        /// evenly over the whole radius.</summary>
        private static readonly float[] RingChance = { 1.00f, 1.00f };   // packed solid, no scatter (her call 2026-10-31)

        private void RaiseBones(Map map)
        {
            var boneDef = DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_ShrineBone");
            if (boneDef == null) return;

            int cells = GenRadial.NumCellsInRadius(SkullRadius);
            for (int i = 0; i < cells; i++)
            {
                var offset = GenRadial.RadialPattern[i];

                // Packed solid from the centre out, under the shrine too (her call
                // 2026-10-31): the skulls draw one layer below it (DoorMoveable), so
                // the ones underneath show through wherever the shrine art is clear
                // and the heap runs right up against it. Square rings on Chebyshev
                // distance, the shape of the building.
                int ring = Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.z));
                if (ring > ShrineArtHalf + RingChance.Length) continue;

                var cell = anchor + offset;
                if (!cell.InBounds(map) || !cell.Standable(map)) continue;
                if (cell.GetFirstThing(map, boneDef) != null) continue;

                bones.Add(GenSpawn.Spawn(ThingMaker.MakeThing(boneDef), cell, map));
            }
        }

        /// <summary>A downed caster's domain collapses (her call 2026-11-12).</summary>
        public override bool CompShouldRemove => base.CompShouldRemove || (Pawn != null && Pawn.Downed);

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (info != null) { JujutsuFundamentals.Domains.Close(info); info = null; }
            // The shrine is not a permanent building. It goes when the domain does.
            //
            // DeSpawn, NOT Destroy: both defs are destroyable=false so that nothing in
            // the game can knock them down, and Thing.Destroy checks exactly that flag
            // first and returns after logging "Tried to destroy non-destroyable
            // thing". So the shrine and the whole heap of skulls were being left
            // standing on the map after the domain closed.
            Remove(shrine);
            shrine = null;
            if (bones != null) foreach (var b in bones) Remove(b);
            bones = new List<Thing>();
        }

        /// <summary>Dead pawns' hediffs stop ticking and are never removed, so clear up here too.</summary>
        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            CompPostPostRemoved();
        }

        private static void Remove(Thing t)
        {
            if (t == null || t.Destroyed) return;
            if (t.Spawned) t.DeSpawn(DestroyMode.Vanish);
        }

        /// <summary>A domain runs a minute at the outside, and ten seconds past the
        /// moment its caster steps off the shrine. Psychic sensitivity buys duration
        /// up to that ceiling and not one tick past it.</summary>
        private const int MaxTicks       = 3600;   // 60 seconds
        private const int AbandonedTicks = 600;    // 10 seconds

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (info == null && anchor.IsValid && Pawn?.Map != null)   // after loading a save
            { info = JujutsuFundamentals.Domains.Make(parent, anchor, Radius, "Malevolent Shrine", SukunaBehaviour.IsSukuna(Pawn)); JujutsuFundamentals.Domains.Rejoin(info); }
            var map = Pawn?.Map;
            if (map == null || !anchor.IsValid) return;

            EnforceDuration();
            SpraySlashes(map, delta);

            damageAcc += delta;
            if (damageAcc < Props.ticksBetween) return;
            damageAcc = 0;
            CutEverything(map);
        }

        /// <summary>
        /// The two ceilings on how long this can run. The minute is absolute; the ten
        /// seconds start the moment he steps off the shrine, and stepping back on
        /// does not give them back — he left.
        ///
        /// Both are clamps rather than subtractions, so whichever of them is already
        /// shorter simply wins: leaving with four seconds left ends it in four.
        /// Clamping every tick also makes this independent of when VEF writes the
        /// ability's own duration into the comp.
        /// </summary>
        private void EnforceDuration()
        {
            var disappears = parent.TryGetComp<HediffComp_Disappears>();
            if (disappears == null) return;

            if (disappears.ticksToDisappear > MaxTicks)
                disappears.ticksToDisappear = MaxTicks;

            var pawn = Pawn;
            bool onShrine = pawn != null && pawn.Spawned
                         && Mathf.Abs(pawn.Position.x - anchor.x) <= ShrineHalf
                         && Mathf.Abs(pawn.Position.z - anchor.z) <= ShrineHalf;
            if (onShrine) return;

            if (disappears.ticksToDisappear > AbandonedTicks)
                disappears.ticksToDisappear = AbandonedTicks;
        }

        /// <summary>Small and numerous: the radius should look packed with cuts, not
        /// dotted with a few big ones.</summary>
        private void SpraySlashes(Map map, int delta)
        {
            fleckAcc += delta;
            if (fleckAcc < 2) return;
            fleckAcc = 0;

            // Dense enough that the whole radius reads as filled with cuts.
            float r = Radius;
            int cells = GenRadial.NumCellsInRadius(r);
            int count = Mathf.Clamp(Mathf.RoundToInt(cells * 0.9f), 120, 1600);
            for (int i = 0; i < count; i++)
            {
                var cell = anchor + GenRadial.RadialPattern[Rand.Range(0, cells)];
                if (cell.InBounds(map)) SlashUtility.SmallSlashFleckAt(cell, map);
            }
        }

        private void CutEverything(Map map)
        {
            var caster = Pawn;
            float r = Radius;
            ShrineWear.Wave(caster, anchor, r, map);   // wears down Hollow Wicker Basket and Simple Domain (her call 2026-11-11)
            float dmg = Props.damageAmount * Scaling.Power(caster) * Scaling.AbilityBoost(caster);
            float ap  = Scaling.ArmorPen(caster, Scaling.DismantleBaseAP,
                                          Scaling.DismantleCapAP, Props.damageAmount);

            foreach (var cell in GenRadial.RadialCellsAround(anchor, r, true))
            {
                if (!cell.InBounds(map)) continue;
                foreach (var thing in cell.GetThingList(map).ToList())
                {
                    if (thing == caster || thing.Destroyed || thing == shrine) continue;
                    if (bones != null && bones.Contains(thing)) continue;

                    if (thing is Corpse corpse) { SlashUtility.ButcherCorpse(corpse, caster); continue; }

                    if (thing is Pawn victim)
                    {
                        // hostileOnly false by design — the binding vow cuts everyone.
                        SureHit.Active = true;   // a domain's sure hit: Gojo's Infinity doesn't stop it
                        try
                        {
                            victim.TakeDamage(new DamageInfo(
                                SukunaDefOf.Sukuna_Slash, dmg, ap, -1f, caster,
                                null, null, DamageInfo.SourceCategory.ThingOrUnknown, victim));
                        }
                        finally { SureHit.Active = false; }
                        SlashUtility.NotifyAttacked(victim, caster);
                        continue;
                    }

                    if (thing.def.category != ThingCategory.Building
                        && thing.def.category != ThingCategory.Item
                        && thing.def.category != ThingCategory.Plant) continue;

                    // Natural rock is mineable Building with hit points; it was being
                    // skipped both by the useHitPoints guard and by the roof check.
                    float mult = thing.def.category == ThingCategory.Plant ? 6f : 1.5f;
                    if (thing.def.building != null && thing.def.building.isNaturalRock) mult = 3f;

                    bool held = thing.def.holdsRoof;
                    thing.TakeDamage(new DamageInfo(
                        SukunaDefOf.Sukuna_Slash, dmg * mult, ap, -1f, caster));
                    if (held && thing.Destroyed && cell.Roofed(map))
                        map.roofGrid.SetRoof(cell, null);
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref anchor, "sukunaShrineAnchor", IntVec3.Invalid);
            Scribe_References.Look(ref shrine, "sukunaShrineThing");
            Scribe_Values.Look(ref damageAcc, "sukunaShrineDmgAcc", 0);
            Scribe_Collections.Look(ref bones, "sukunaShrineBones", LookMode.Reference);
            if (bones == null) bones = new List<Thing>();
        }
    }
}
