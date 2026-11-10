using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// The cut travels. It used to resolve the whole line in a single frame, so there
    /// was nothing to watch — now the destruction chases the slash outward, a few
    /// cells per tick, until it leaves the map.
    /// </summary>
    public class WorldCut : GameComponent
    {
        private class Cut
        {
            public Map map;
            public Pawn caster;
            public IntVec3 origin;
            public Vector3 dir;
            public float angle;
            public float dist;
            public float damage;
            public int maxSpan;
            public float trailed;   // how far along the streak has been laid
            public int halfWidth;   // 1 = three cells across; 2 = five, with the output raised
        }

        private const int   HalfWidth      = 1;      // three cells across
        private const float CellsPerTick   = 2.6f;   // how fast the cut runs outward

        private static readonly List<Cut> active = new List<Cut>();

        public WorldCut(Game game)
        {
            // Both caches are session state, not save state: a cut in flight does not
            // survive a reload, and the stone lookup is keyed on map ids that mean
            // something different in a different save.
            active.Clear();
            stoneByMap.Clear();
        }

        public static void Begin(Pawn caster, Map map, IntVec3 origin, Vector3 dir, float damage)
        {
            if (map == null) return;
            active.Add(new Cut
            {
                map = map, caster = caster, origin = origin,
                dir = dir.normalized, angle = dir.AngleFlat(), dist = 1f, trailed = 0f,
                damage = damage, maxSpan = map.Size.x + map.Size.z,
                halfWidth = Scaling.Raised(caster) ? 2 : HalfWidth,
            });
        }

        public override void GameComponentTick()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                var c = active[i];
                if (c.map == null || !Find.Maps.Contains(c.map)) { active.RemoveAt(i); continue; }   // map closed

                float from = c.dist;
                c.dist += CellsPerTick;

                // Every cell whose centre falls inside the band, exactly.
                //
                // This used to walk points along the spine and step the perpendicular
                // in whole cells, which on a diagonal skipped cells outright — a pawn
                // standing dead in the line could take nothing at all. Testing the
                // cells in the swept bounding box against along/across distance has no
                // holes and no double-hits: the along range is [from, dist), so
                // consecutive ticks tile rather than overlap.
                var originV = c.origin.ToVector3Shifted();
                var perp    = new Vector3(-c.dir.z, 0f, c.dir.x);

                var a = originV + c.dir * from;
                var b = originV + c.dir * c.dist;
                int pad  = c.halfWidth + 2;
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x)) - pad;
                int maxX = Mathf.CeilToInt (Mathf.Max(a.x, b.x)) + pad;
                int minZ = Mathf.FloorToInt(Mathf.Min(a.z, b.z)) - pad;
                int maxZ = Mathf.CeilToInt (Mathf.Max(a.z, b.z)) + pad;

                for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var cell = new IntVec3(x, 0, z);
                    if (!cell.InBounds(c.map)) continue;
                    var v = cell.ToVector3Shifted() - originV;
                    float along = v.x * c.dir.x + v.z * c.dir.z;
                    if (along < from || along >= c.dist) continue;
                    float side = Mathf.Abs(v.x * perp.x + v.z * perp.z);
                    if (side > c.halfWidth + 0.5f) continue;
                    Carve(c, cell);
                    // no ground crack (her call 2026-11-07: on a diagonal it showed as three lines)
                }

                // ONE crescent slash travelling down the line, leaving a crack in the ground
                // (back 2026-11-06, her call: the 2026-10-31 single streak read as a red laser).
                var head = originV + c.dir * c.dist;
                bool offMap = !head.ToIntVec3().InBounds(c.map);
                if (!offMap) SlashUtility.WorldSlashHead(head, c.map, c.angle, (2 * c.halfWidth + 1) / 3f);   // grows with the cut

                if (offMap || c.dist > c.maxSpan) active.RemoveAt(i);
            }
        }

        /// <summary>
        /// The cut goes through the body, not into a limb.
        ///
        /// One DamageInfo lands on ONE body part and the injury it makes is capped at
        /// that part's remaining health, so 140 damage aimed at a leg destroyed the
        /// leg and threw the other 110 away — which is why a line of pawns was walking
        /// away from a slash that is supposed to bisect them. Aiming at the core part
        /// settles it: DamageWorker_AddInjury.ShouldReduceDamageToPreservePart is
        /// false for the core part, so nothing is held back, and
        /// Pawn_HealthTracker.ShouldBeDead kills on zero core-part efficiency.
        ///
        /// HasPreventsDeath still short-circuits that check, so Deathless and its
        /// relatives remain the rare way to live through this — which is the one
        /// survival the design ever wanted.
        /// </summary>
        private static void Bisect(Cut c, Pawn victim)
        {
            var core = victim.RaceProps?.body?.corePart;
            float health = core == null ? 0f : victim.health.hediffSet.GetPartHealth(core);

            // The guaranteed bisection belongs to the whole King. Below twenty fingers
            // the cut is only as deep as its damage (balancing, 2026-10-15).
            bool whole = Scaling.FingersEaten(c.caster) >= 20 || ModuloYuji.Is(c.caster);   // he wields all of it
            var dinfo = new DamageInfo(SukunaDefOf.Sukuna_WorldSlash,
                                       whole ? Mathf.Max(c.damage, health + 1f) : c.damage,
                                       Scaling.CompleteAP, -1f, c.caster);
            if (core != null) dinfo.SetHitPart(core);
            victim.TakeDamage(Patch_WorldSlashUnstoppable.MakeUnstoppable(dinfo, victim));
            SlashUtility.NotifyAttacked(victim, c.caster);
        }

        /// <summary>Everything in the line is cleared out of the way: people cut,
        /// corpses butchered, buildings, plants, stone chunks and filth simply gone.
        /// Nothing is left behind and the ground is not changed; a crack shows along
        /// the centre for a few seconds (her call 2026-10-31).</summary>
        private static void Carve(Cut c, IntVec3 cell)
        {
            var map = c.map;
            foreach (var thing in cell.GetThingList(map).ToList())
            {
                if (thing == c.caster || thing.Destroyed) continue;
                if (thing is Corpse corpse) { SlashUtility.ButcherCorpse(corpse, c.caster); continue; }
                if (thing is Pawn v) { Bisect(c, v); continue; }

                bool clear = thing.def.category == ThingCategory.Building
                          || thing.def.category == ThingCategory.Plant
                          || thing.def.category == ThingCategory.Filth
                          || IsStoneChunk(thing.def);
                if (clear && thing.def.destroyable)
                {
                    bool held = thing.def.holdsRoof;
                    thing.Destroy(DestroyMode.Vanish);   // Vanish: no rubble, no chunks, no leavings
                    // Clear the roof rather than let it collapse — the cut removes
                    // what was above it too, and the alert spam was unbearable.
                    if (held && cell.Roofed(map)) map.roofGrid.SetRoof(cell, null);
                    continue;
                }
                thing.TakeDamage(Patch_WorldSlashUnstoppable.MakeUnstoppable(new DamageInfo(
                    SukunaDefOf.Sukuna_WorldSlash, c.damage, Scaling.CompleteAP, -1f, c.caster), thing));
            }
        }

        private static ThingCategoryDef chunkCat;
        private static bool IsStoneChunk(ThingDef def)
        {
            if (chunkCat == null) chunkCat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("StoneChunks");
            return chunkCat != null && def.thingCategories != null && def.thingCategories.Contains(chunkCat);
        }

        private static FleckDef splitDef;
        private static void Split(Cut c, IntVec3 cell)
        {
            if (splitDef == null) splitDef = DefDatabase<FleckDef>.GetNamedSilentFail("Sukuna_Fleck_GroundSplit");
            if (splitDef == null) return;
            var data = FleckMaker.GetDataStatic(cell.ToVector3Shifted(), c.map, splitDef, 1f);
            data.rotation = c.angle;
            c.map.flecks.CreateFleck(data);
        }

        private static TerrainDef scarCache;
        private static TerrainDef ScarTerrain
        {
            get
            {
                if (scarCache == null)
                    scarCache = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                             ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Sand");
                return scarCache;
            }
        }

        /// <summary>
        /// The stone chunks native to this map's tile.
        ///
        /// World.NaturalRockTypesIn gives the rock ThingDefs the tile actually
        /// generates with; each one's building.mineableThing is the chunk it breaks
        /// into. The StoneChunks category filter is belt and braces — it guarantees
        /// that a modded rock type whose mineableThing is an ore or an ingot cannot
        /// put a metal chunk in the furrow.
        /// </summary>
        private static readonly Dictionary<int, List<ThingDef>> stoneByMap =
            new Dictionary<int, List<ThingDef>>();

        private static ThingDef LocalStone(Map map)
        {
            if (map == null) return null;
            if (!stoneByMap.TryGetValue(map.uniqueID, out var stones))
            {
                stones = new List<ThingDef>();
                var chunks = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("StoneChunks");
                foreach (var rock in Find.World.NaturalRockTypesIn(map.Tile))
                {
                    var chunk = rock?.building?.mineableThing;
                    if (chunk == null) continue;
                    if (chunks != null && (chunk.thingCategories == null
                                           || !chunk.thingCategories.Contains(chunks))) continue;
                    if (!stones.Contains(chunk)) stones.Add(chunk);
                }
                stoneByMap[map.uniqueID] = stones;
            }
            return stones.Count == 0 ? null : stones.RandomElement();
        }
    }
}
