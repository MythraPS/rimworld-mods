using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Dhruv Lakdawalla's shikigami, copied by Yuta (her design 2026-11-11, reworked the same day): two real
    /// shikigami, small bat-like creatures with a head like Cursed Spirit Rika's, fly a circle round the chosen spot.
    /// The paths they trace become lines that cut any enemy crossing them (canon: the paths become barrier lines).
    /// They are the caster's, can be shot or struck, and any hit at all destroys one (her call: 1 HP).
    /// The circle is as wide as Yuta's domain would be. If at least one finishes a full lap, the barrier is traced:
    /// for a while Yuta can open Authentic Mutual Love inside that circle for half the psyfocus and half the neural
    /// heat, on that circle (DhruvBarrier). Numbers are proposals.
    /// </summary>
    public class Ability_DhruvShikigami : Ability
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = targets.FirstOrDefault();
            if (pawn?.Map == null || !t.IsValid) return;
            DhruvTrails.Start(pawn, t.Cell, GetPowerForPawn());
        }
    }

    [DefOf]
    public static class DhruvDefOf
    {
        public static PawnKindDef JJK_DhruvBat;
        public static HediffDef JJK_DhruvBarrierTraced;
        static DhruvDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(DhruvDefOf));
    }

    /// <summary>Running shikigami. Not saved: a save mid-flight sends them away on load.
    /// Her design 2026-11-11 (second pass): they fly to the chosen spot, then spiral outward round it, cutting what
    /// they pass. Every full turn closes a circle: Yuta, standing inside it, can open his domain on that circle (half
    /// the cost, see DhruvBarrier). They keep spiralling out, so each turn makes a larger circle he can use again,
    /// until the circle is as wide as his domain could be; then they go. Numbers are proposals.</summary>
    public class DhruvTrails : GameComponent
    {
        public const float Speed = 0.25f;         // cells a tick (15 a second)
        public const float StartRadius = 4f;      // the first, tightest turn
        public const float Growth = 6f;           // how much wider each turn gets
        public const int TrailLife = 300;         // how long a traced cell keeps cutting (5 s)
        public const int HitGap = 30;             // one cut per victim per half second

        private class Run
        {
            public Pawn caster;
            public Map map;
            public Vector3 center, from;
            public IntVec3 centerCell;
            public float maxRadius, damage;
            public int flyTicks, age;
            public float theta, radius;           // spiral angle (radians) and current distance from the centre
            public int turns;                     // full turns closed so far
            public readonly List<Pawn> bats = new List<Pawn>();
            public Dictionary<IntVec3, int> trail = new Dictionary<IntVec3, int>();
            public Dictionary<int, int> lastHit = new Dictionary<int, int>();
        }

        private static readonly List<Run> runs = new List<Run>();
        /// <summary>Where each shikigami is drawn (exactly on its path, between cells).</summary>
        public static readonly Dictionary<Pawn, Vector3> drawAt = new Dictionary<Pawn, Vector3>();
        private static readonly HashSet<Pawn> struck = new HashSet<Pawn>();
        private static FleckDef fleck;

        public DhruvTrails(Game game) { runs.Clear(); drawAt.Clear(); struck.Clear(); }

        public static bool IsBat(Thing t) => t is Pawn p && DhruvDefOf.JJK_DhruvBat != null && p.kindDef == DhruvDefOf.JJK_DhruvBat;

        /// <summary>Any hit: it goes on the next tick (not in the middle of the damage).</summary>
        public static void Strike(Pawn bat) { if (bat != null) struck.Add(bat); }

        /// <summary>The widest circle: what the caster's domain would be (the domain's own numbers).</summary>
        public static float Radius(Pawn caster)
        {
            var props = YutaCopyDefOf.JJK_MutualLoveDomain?.CompProps<HediffCompProperties_MutualLove>();
            float r = props?.radius ?? 23.9f, max = props?.maxRadius ?? 55f;
            return Mathf.Min(r * Jujutsu.Sensitivity(caster) * Jujutsu.Boost(caster), max);
        }

        public static void Start(Pawn caster, IntVec3 cell, float damage)
        {
            var map = caster.Map;
            var r = new Run
            {
                caster = caster, map = map, center = cell.ToVector3Shifted(), centerCell = cell, from = caster.DrawPos,
                maxRadius = Mathf.Max(StartRadius + Growth, Radius(caster)), damage = damage, radius = StartRadius,
            };
            r.flyTicks = Mathf.Max(30, Mathf.RoundToInt((r.center - r.from).MagnitudeHorizontal() / Speed));
            for (int k = 0; k < 2; k++)
            {
                var bat = Make(caster, caster.Position, map);
                if (bat == null) { Log.Warning("[JJK Yuta] Dhruv's shikigami: couldn't make one (JJK_DhruvBat missing?)"); continue; }
                drawAt[bat] = caster.DrawPos;
                r.bats.Add(bat);
            }
            runs.Add(r);
            if (r.bats.Count > 0 && caster.Faction == Faction.OfPlayer)
                Messages.Message("Dhruv's shikigami fly out and start to spiral.", new LookTargets(r.bats), MessageTypeDefOf.NeutralEvent, false);
        }

        private static Pawn Make(Pawn caster, IntVec3 at, Map map)
        {
            if (DhruvDefOf.JJK_DhruvBat == null) return null;
            Pawn p;
            NoMagicClass.making = true;
            try { p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(DhruvDefOf.JJK_DhruvBat, caster.Faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)); }
            finally { NoMagicClass.making = false; }
            if (!at.Walkable(map)) at = caster.Position;
            GenSpawn.Spawn(p, at, map);
            Rika.Puff(p.DrawPos, map, 1.2f);
            return p;
        }

        /// <summary>Sends one away: no body, not a death, not kept as a world pawn (as Rika.Dismiss).</summary>
        public static void Vanish(Pawn bat)
        {
            if (bat == null) return;
            drawAt.Remove(bat);
            struck.Remove(bat);
            if (!bat.Destroyed && !bat.Dead)
            {
                if (bat.Spawned) Rika.Puff(bat.DrawPos, bat.Map, 1f);
                bat.Destroy(DestroyMode.Vanish);
            }
            // left as a world pawn: combat logs still point at it; the game clears it once nothing does
        }

        /// <summary>The two fly on opposite sides of the spiral.</summary>
        private static Vector3 OnSpiral(Run r, int k, float theta, float radius) =>
            r.center + new Vector3(Mathf.Cos(theta + k * Mathf.PI), 0f, Mathf.Sin(theta + k * Mathf.PI)) * radius;

        public override void LoadedGame()
        {
            base.LoadedGame();
            // runs aren't saved: any shikigami left over from a save goes
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned.Where(IsBat).ToList()) Vanish(p);
        }

        private static void Place(Run r, Pawn bat, Vector3 pos, Vector3 facing)
        {
            drawAt[bat] = pos;
            bat.Rotation = Rot4.FromAngleFlat(facing.AngleFlat());
            var c = pos.ToIntVec3();
            // they fly over walls; their cell (what is shot at) only moves onto open ground
            if (c.InBounds(r.map) && c != bat.Position && c.Walkable(r.map)) { bat.Position = c; bat.Notify_Teleported(false, false); }
        }

        public override void GameComponentTick()
        {
            if (struck.Count > 0) foreach (var b in struck.ToList()) Vanish(b);
            if (runs.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            fleck = fleck ?? DefDatabase<FleckDef>.GetNamedSilentFail("JJK_Fleck_AirStepBurst");
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                var r = runs[i];
                r.age++;
                // gone some other way (killed outright, map removed): still cleaned up like a vanish
                for (int k = r.bats.Count - 1; k >= 0; k--)
                {
                    var b = r.bats[k];
                    if (b != null && !b.Destroyed && !b.Dead && b.Spawned) continue;
                    Vanish(b);
                    r.bats.RemoveAt(k);
                }
                bool over = r.radius > r.maxRadius || r.caster == null || r.caster.Dead || r.map == null || r.map != r.caster.Map;
                if (over || r.bats.Count == 0)
                {
                    foreach (var b in r.bats) Vanish(b);
                    runs.RemoveAt(i);
                    continue;
                }
                if (r.age <= r.flyTicks)
                {
                    // flying out to the spot, then out to the first turn
                    float f = r.age / (float)r.flyTicks;
                    for (int k = 0; k < r.bats.Count; k++)
                        Place(r, r.bats[k], Vector3.Lerp(r.from, OnSpiral(r, k, 0f, StartRadius), f), OnSpiral(r, k, 0f, StartRadius) - r.from);
                    continue;
                }
                // the spiral: the same speed along the path, wider by Growth every turn
                float dTheta = Speed / r.radius;
                r.theta += dTheta;
                r.radius = StartRadius + Growth * r.theta / (2f * Mathf.PI);
                int turns = Mathf.FloorToInt(r.theta / (2f * Mathf.PI));
                if (turns > r.turns)
                {
                    r.turns = turns;
                    DhruvBarrier.Trace(r.caster, r.centerCell, r.radius);   // a closed circle he can use, larger each time
                }
                for (int k = 0; k < r.bats.Count; k++)
                {
                    var bat = r.bats[k];
                    var pos = OnSpiral(r, k, r.theta, r.radius);
                    Place(r, bat, pos, OnSpiral(r, k, r.theta + 0.1f, r.radius) - pos);
                    var c = pos.ToIntVec3();
                    if (!c.InBounds(r.map)) continue;
                    r.trail[c] = now + TrailLife;
                    if (fleck != null && r.age % 3 == 0)
                    {
                        var data = FleckMaker.GetDataStatic(pos, r.map, fleck, 0.45f);
                        data.instanceColor = Patch_WeaponGlow.EnergyColor(r.caster);
                        r.map.flecks.CreateFleck(data);
                    }
                }
                if (r.age % 10 != 0) continue;
                // anything crossing their path is cut: everyone who isn't the caster's ally (Domains.Hits), and plants
                foreach (var v in r.map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (v.Dead || IsBat(v) || !Domains.Hits(r.caster, v)) continue;
                    if (!r.trail.TryGetValue(v.Position, out var until) || until < now) continue;
                    if (r.lastHit.TryGetValue(v.thingIDNumber, out var last) && now - last < HitGap) continue;
                    if (Protection.Parried(v, r.caster)) continue;
                    r.lastHit[v.thingIDNumber] = now;
                    v.TakeDamage(new DamageInfo(DamageDefOf.Cut, r.damage * Jujutsu.Boost(r.caster), 0.3f, -1f, r.caster));
                }
                foreach (var c in r.trail.Where(e => e.Value - TrailLife > now - 10).Select(e => e.Key).ToList())
                {
                    var plant = c.GetPlant(r.map);
                    if (plant != null && !plant.Destroyed && plant.def.plant.IsTree == false) plant.TakeDamage(new DamageInfo(DamageDefOf.Cut, r.damage, 0f, -1f, r.caster));
                }
            }
        }
    }

    /// <summary>Any hit destroys a shikigami (her call: 1 HP, "one shot by anything").</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_DhruvBatOneHit
    {
        public static bool Prefix(Pawn __instance, ref bool absorbed)
        {
            if (!DhruvTrails.IsBat(__instance)) return true;
            absorbed = true;
            DhruvTrails.Strike(__instance);
            return false;
        }
    }

    /// <summary>Drawn exactly on the circle, not snapped to cells or held back by walls.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Patch_DhruvBatDrawPos
    {
        public static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            if (DhruvTrails.drawAt.Count == 0 || !DhruvTrails.drawAt.TryGetValue(__instance, out var at)) return;
            __result = new Vector3(at.x, __result.y, at.z);
        }
    }

    /// <summary>
    /// The traced barrier (her call 2026-11-11): one full lap of Dhruv's shikigami round a domain-sized circle.
    /// While it lasts (proposal: a minute) Yuta can open Authentic Mutual Love from inside it for half the psyfocus
    /// and half the neural heat, and the domain stands on the traced circle (at least as wide). Opening it uses it up.
    /// </summary>
    public static class DhruvBarrier
    {
        public const float CostFactor = 0.5f, StrainFactor = 0.5f;

        public static void Trace(Pawn caster, IntVec3 center, float radius)
        {
            if (caster?.health == null || DhruvDefOf.JJK_DhruvBarrierTraced == null) return;
            var old = caster.health.hediffSet.GetFirstHediffOfDef(DhruvDefOf.JJK_DhruvBarrierTraced);
            if (old != null) caster.health.RemoveHediff(old);
            var h = caster.health.AddHediff(DhruvDefOf.JJK_DhruvBarrierTraced);
            var comp = h?.TryGetComp<HediffComp_DhruvBarrier>();
            if (comp != null) { comp.center = center; comp.radius = radius; }
            if (caster.Spawned)
            {
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, "Barrier traced", Patch_WeaponGlow.EnergyColor(caster), 2.5f);
                if (caster.Faction == Faction.OfPlayer && Yuta.Is(caster))
                    Messages.Message($"Dhruv's shikigami closed a circle {radius * 2f:0} cells wide: {caster.LabelShort} can open his domain on it from inside, for half the cost.",
                                     caster, MessageTypeDefOf.PositiveEvent, false);
            }
        }

        public static HediffComp_DhruvBarrier Get(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(DhruvDefOf.JJK_DhruvBarrierTraced)?.TryGetComp<HediffComp_DhruvBarrier>();

        /// <summary>Does this domain-caster stand inside a traced circle of his own?</summary>
        public static bool Ready(Pawn p)
        {
            var b = Get(p);
            return b != null && p.Spawned && b.center.IsValid && p.Position.InHorDistOf(b.center, b.radius);
        }

        public static void Use(Pawn p)
        {
            var b = Get(p);
            if (b != null) p.health.RemoveHediff(b.parent);
        }
    }

    public class HediffCompProperties_DhruvBarrier : HediffCompProperties
    {
        public HediffCompProperties_DhruvBarrier() { compClass = typeof(HediffComp_DhruvBarrier); }
    }

    public class HediffComp_DhruvBarrier : HediffComp
    {
        public IntVec3 center = IntVec3.Invalid;
        public float radius;

        public override string CompLabelInBracketsExtra => Pawn != null && DhruvBarrier.Ready(Pawn) ? "inside" : null;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref center, "dhruvCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "dhruvRadius");
        }
    }
}
