using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    /// <summary>
    /// Piercing Blood: Scatter, Modulo Yuji's own (her call 2026-11-01). Reworked 2026-11-06 (her note): one
    /// straight shot at what he aims at, and it only scatters when there are more enemies in range - then one
    /// shot for each, up to eight. Poisons whoever it hits: three days unconscious.
    /// </summary>
    public class Ability_PiercingBloodScatter : VEFAbility
    {
        public const int Shots = 8;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var map = pawn.Map;
            if (map == null || targets == null || targets.Length == 0) return;
            float range = GetRangeForPawn();
            if (range <= 0f) range = 34.9f;
            var aimThing = targets[0].Thing as Pawn;
            IntVec3 aimCell = targets[0].Cell;

            var foes = map.mapPawns.AllPawnsSpawned
                .Where(p => p != pawn && !p.Dead && !p.Downed && p.HostileTo(pawn)
                            && p.Position.DistanceTo(pawn.Position) <= range)
                .OrderBy(p => p == aimThing ? -1f : p.Position.DistanceTo(aimCell))
                .Take(Shots).ToList();

            // the aimed shot: at the pawn he picked, or the enemy nearest the spot, or just the spot
            var main = aimThing != null && aimThing.HostileTo(pawn) ? aimThing : foes.FirstOrDefault(f => f.Position.DistanceTo(aimCell) <= 3f);
            var hits = new List<LocalTargetInfo>();
            hits.Add(main != null ? new LocalTargetInfo(main) : new LocalTargetInfo(aimCell));
            // scatter: one more branch for every other enemy in range
            foreach (var foe in foes)
                if (foe != main) hits.Add(foe);
            BloodBeams.Fire(pawn, hits);
        }
    }

    /// <summary>
    /// Piercing Blood as one continuous beam that splits (her call 2026-11-07): from Yuji it runs toward the
    /// middle of its targets, splits in two, each half splits again, until every branch ends on one enemy.
    /// Each segment appears when the one before it reaches its end; the hit lands when a branch arrives.
    /// </summary>
    public class BloodBeams : GameComponent
    {
        public const float CellsPerTick = 2f;
        public const float Width = 0.32f;
        private const float SplitAt = 0.45f;   // share of the way to the targets' middle where it splits

        private class Pending
        {
            public int tick;
            public Map map;
            public Pawn caster;
            public Vector3 from, to;
            // set only on a branch's hit. Must start Invalid: an unset LocalTargetInfo is cell (0,0,0), which the
            // game counts as valid, so every segment was taken for a hit and none was ever drawn (found 2026-11-09)
            public LocalTargetInfo hit = LocalTargetInfo.Invalid;
            public int arrive;            // a segment: the tick its tip reaches its end
        }

        /// <summary>A segment on screen: it grows from its start to its end, then holds and fades.</summary>
        private class Beam
        {
            public Map map;
            public Pawn caster;
            public Vector3 from, to;
            public int start, arrive;
            public float trailed;         // how far along the segment blood has been left (cells)
        }

        /// <summary>The blood stays on the ground where the beam passed (her call 2026-11-11), so Supernova can
        /// burst it later. Proposal: a pool on most cells along the path.</summary>
        public const float TrailChance = 0.7f;

        private static void Trail(Beam b, float upTo)
        {
            var dir = b.to - b.from; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.01f) return;
            dir /= len;
            upTo = Mathf.Min(upTo, len);
            var blood = b.caster?.RaceProps?.BloodDef ?? ThingDefOf.Filth_Blood;
            IntVec3 last = IntVec3.Invalid;
            for (float d = b.trailed; d < upTo; d += 0.5f)
            {
                var c = (b.from + dir * d).ToIntVec3();
                if (c == last) continue;
                last = c;
                if (c.InBounds(b.map) && c.Walkable(b.map) && Rand.Chance(TrailChance)) FilthMaker.TryMakeFilth(c, b.map, blood, 1);
            }
            b.trailed = upTo;
        }

        private static readonly List<Pending> queue = new List<Pending>();
        private static readonly List<Beam> live = new List<Beam>();
        private const int HoldTicks = 20, FadeTicks = 25;
        private const float HeadWidth = 0.5f, HeadLength = 1.4f;
        private static FleckDef bubbleDef;

        public BloodBeams(Game game) { queue.Clear(); live.Clear(); }

        public static void Fire(Pawn caster, List<LocalTargetInfo> targets)
        {
            if (caster?.Map == null || targets.Count == 0) return;
            Branch(caster, caster.DrawPos, targets, Find.TickManager.TicksGame);
        }

        private static Vector3 At(LocalTargetInfo t) => t.HasThing ? t.Thing.DrawPos : t.Cell.ToVector3Shifted();

        private static void Branch(Pawn caster, Vector3 from, List<LocalTargetInfo> targets, int tick)
        {
            from.y = 0f;
            if (targets.Count == 1)
            {
                var to = At(targets[0]); to.y = 0f;
                Add(caster, from, to, tick, targets[0]);
                return;
            }
            var mid = Vector3.zero;
            foreach (var t in targets) mid += At(t);
            mid /= targets.Count; mid.y = 0f;
            var split = from + (mid - from) * SplitAt;
            int next = Add(caster, from, split, tick, LocalTargetInfo.Invalid);
            // the two halves: sorted by which side of the beam they lie on
            var dir = (mid - from).normalized;
            var sorted = targets.OrderBy(t => { var v = At(t) - split; return dir.x * v.z - dir.z * v.x; }).ToList();
            int half = sorted.Count / 2;
            Branch(caster, split, sorted.Take(half).ToList(), next);
            Branch(caster, split, sorted.Skip(half).ToList(), next);
        }

        /// <summary>Queues one segment (and, at a branch's end, its hit when the beam arrives);
        /// returns the tick it reaches its end.</summary>
        private static int Add(Pawn caster, Vector3 from, Vector3 to, int tick, LocalTargetInfo hit)
        {
            float len = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            int end = tick + Mathf.Max(1, Mathf.CeilToInt(len / CellsPerTick));
            queue.Add(new Pending { tick = tick, arrive = end, map = caster.Map, caster = caster, from = from, to = to });
            if (hit.IsValid) queue.Add(new Pending { tick = end, map = caster.Map, caster = caster, hit = hit });
            return end;
        }

        public override void GameComponentTick()
        {
            if (queue.Count == 0 && live.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                var q = queue[i];
                if (q.tick > now) continue;
                queue.RemoveAt(i);
                if (q.map == null || q.caster == null || q.caster.Map != q.map) continue;
                if (q.hit.IsValid) Hit(q.caster, q.hit, q.map);
                else live.Add(new Beam { map = q.map, caster = q.caster, from = q.from, to = q.to, start = q.tick, arrive = q.arrive });
            }
            Boil(now);
        }

        /// <summary>Where a segment's tip is at this moment.</summary>
        private static Vector3 Tip(Beam b, int now)
        {
            float t = b.arrive > b.start ? Mathf.Clamp01((now - b.start) / (float)(b.arrive - b.start)) : 1f;
            return Vector3.Lerp(b.from, b.to, t);
        }

        /// <summary>The blood boils at the tip of every segment still travelling (her note 2026-11-06), and
        /// spent segments are dropped once they've faded.</summary>
        private static void Boil(int now)
        {
            if (bubbleDef == null) bubbleDef = DefDatabase<FleckDef>.GetNamedSilentFail("Sukuna_Fleck_BloodBubble");
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var b = live[i];
                if (now > b.arrive + HoldTicks + FadeTicks || b.map == null || !Find.Maps.Contains(b.map)) { live.RemoveAt(i); continue; }
                Trail(b, (Tip(b, now) - b.from).MagnitudeHorizontal());
                if (now >= b.arrive || bubbleDef == null) continue;
                var dir = b.to - b.from; dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) continue;
                dir.Normalize();
                var side = new Vector3(-dir.z, 0f, dir.x);
                var tip = Tip(b, now);
                int n = Rand.Chance(0.5f) ? 2 : 1;
                for (int k = 0; k < n; k++)
                {
                    var at = tip - dir * Rand.Range(0.2f, 1.5f) + side * Rand.Range(-0.12f, 0.12f);
                    if (!at.ShouldSpawnMotesAt(b.map)) continue;
                    var data = FleckMaker.GetDataStatic(at, b.map, bubbleDef, Rand.Range(0.12f, 0.26f));
                    data.velocityAngle = Rand.Range(0f, 360f);
                    data.velocitySpeed = Rand.Range(0.05f, 0.3f);
                    b.map.flecks.CreateFleck(data);
                }
            }
        }

        /// <summary>Drawn straight to the screen every frame, the way the game draws its own lines. Her report
        /// 2026-11-09: the stretched fleck used before never showed; its picture was a hairline at that width.
        /// Now a solid dark-red shaft from the segment's start to its tip, and the arrow head leading while it
        /// travels.</summary>
        public override void GameComponentUpdate()
        {
            if (live.Count == 0) return;
            var map = Find.CurrentMap;
            var shaftMat = BloodBeamMats.Shaft;
            var headMat = BloodBeamMats.Head;
            int now = Find.TickManager.TicksGame;
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (map == null || WorldRendererUtility.WorldRendered) return;
            foreach (var b in live)
            {
                if (b.map != map) continue;
                float alpha = now <= b.arrive + HoldTicks ? 1f : 1f - (now - b.arrive - HoldTicks) / (float)FadeTicks;
                if (alpha <= 0f) continue;
                var from = b.from; from.y = y;
                var tip = Tip(b, now); tip.y = y;
                if ((tip - from).MagnitudeHorizontal() > 0.05f)
                    GenDraw.DrawLineBetween(from, tip, alpha >= 1f ? shaftMat : FadedMaterialPool.FadedVersionOf(shaftMat, alpha), Width);
                if (now >= b.arrive) continue;
                var dir = b.to - b.from; dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();
                var pos = tip - dir * (HeadLength * 0.5f); pos.y = y + 0.01f;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.LookRotation(dir), new Vector3(HeadWidth, 1f, HeadLength)), headMat, 0);
            }
        }

        /// <summary>The blood's damage (from the old shot's def) and its poison: three days unconscious.
        /// Nothing happens if the hit was stopped (Gojo's Infinity, a shield).</summary>
        private static void Hit(Pawn caster, LocalTargetInfo target, Map map)
        {
            Pawn p = target.Thing as Pawn;
            if (p == null && target.Cell.InBounds(map))
                p = target.Cell.GetThingList(map).OfType<Pawn>().FirstOrDefault(x => x != caster && !x.Dead);
            if (p == null || p.Dead || !p.Spawned || p.Map != map) return;
            var proj = SukunaArcDefOf.Sukuna_BloodBullet?.projectile;
            float dmg = proj != null ? proj.GetDamageAmount(null) : 14f;
            float ap = proj != null ? proj.GetArmorPenetration(null) : 0.6f;
            var def = proj?.damageDef ?? DamageDefOf.Stab;
            var angle = (p.DrawPos - caster.DrawPos).AngleFlat();
            var result = p.TakeDamage(new DamageInfo(def, dmg, ap, angle, caster));
            SlashUtility.NotifyAttacked(p, caster);
            if (result == null || result.totalDamageDealt <= 0f || p.Dead || !p.RaceProps.IsFlesh) return;
            var poison = p.health.hediffSet.GetFirstHediffOfDef(SukunaArcDefOf.Sukuna_BloodPoison);
            if (poison != null) p.health.RemoveHediff(poison);   // a fresh three days
            p.health.AddHediff(SukunaArcDefOf.Sukuna_BloodPoison);
        }
    }

    /// <summary>Straight and fast after its target (2026-11-06: no more curving), boiling as it goes.</summary>
    public class Projectile_HomingBlood : Bullet
    {
        private static FleckDef bubbleDef;
        // kept so saves made before 2026-11-06 still load
        private float bend;

        protected override void TickInterval(int delta)
        {
            if (intendedTarget.Thing is Pawn t && t.Spawned && t.Map == Map) destination = t.DrawPos;
            base.TickInterval(delta);
            if (Spawned && Map != null) Boil();
        }

        /// <summary>A bubble or two along the shot's length each tick, swelling as they pop.</summary>
        private void Boil()
        {
            if (bubbleDef == null) bubbleDef = DefDatabase<FleckDef>.GetNamedSilentFail("Sukuna_Fleck_BloodBubble");
            if (bubbleDef == null) return;
            var dir = destination - origin; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            var side = new Vector3(-dir.z, 0f, dir.x);
            int n = Rand.Chance(0.5f) ? 2 : 1;
            for (int i = 0; i < n; i++)
            {
                var at = ExactPosition - dir * Rand.Range(0.2f, 1.5f) + side * Rand.Range(-0.12f, 0.12f);
                if (!at.ShouldSpawnMotesAt(Map)) continue;
                var data = FleckMaker.GetDataStatic(at, Map, bubbleDef, Rand.Range(0.12f, 0.26f));
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(0.05f, 0.3f);
                Map.flecks.CreateFleck(data);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // It found them: whatever it actually ran into, the target is who it hits.
            if (intendedTarget.Thing is Pawn t && t.Spawned && !t.Dead && t.Position.DistanceTo(Position) <= 2.5f)
                hitThing = t;
            base.Impact(hitThing, blockedByShield);
            if (hitThing is Pawn p && !p.Dead && p.RaceProps.IsFlesh)
            {
                var poison = p.health.hediffSet.GetFirstHediffOfDef(SukunaArcDefOf.Sukuna_BloodPoison);
                if (poison != null) p.health.RemoveHediff(poison);   // a fresh three days
                p.health.AddHediff(SukunaArcDefOf.Sukuna_BloodPoison);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref bend, "sukunaBloodBend", 0f);
        }
    }

    /// <summary>The beam's textures, loaded on the main thread at startup (the game warns about a GameComponent
    /// holding a Material field).</summary>
    [StaticConstructorOnStartup]
    public static class BloodBeamMats
    {
        public static readonly Material Shaft = MaterialPool.MatFrom("Things/Sukuna/BloodBeamShaft", ShaderDatabase.Transparent);
        public static readonly Material Head = MaterialPool.MatFrom("Things/Sukuna/BloodBeam", ShaderDatabase.Transparent);
    }
}
