using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class MaliceCloakDefOf
    {
        public static ThingDef JJK_MaliceCloak;
        public static PawnKindDef JJK_MaliceShade;
        static MaliceCloakDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(MaliceCloakDefOf));
    }

    /// <summary>
    /// The Malice Cloak (her design 2026-11-11; the name is a placeholder). A cloak of human leather filled with cursed,
    /// void-like energy. It doesn't hide its wearer. Thrown, it rises as a shade where it lands (her correction: "it can
    /// simulate a temporary targetable pawn/creature"): a hovering pawn in the thrower's faction that never attacks.
    /// Whoever sees it, except the thrower and the thrower's allies, has a chance to have their aggro grabbed (her
    /// clarification: "not pull them, it grabs their aggro"): for a while it becomes their attack target, ranged shooting
    /// it from where they stand, melee going for it as they would any target. A Six Eyes always. Pawns who can't fight
    /// aren't affected. Blows on the shade come off the cloak's hit points. Cast again, it comes back onto the thrower
    /// from anywhere. Numbers are proposals.
    /// </summary>
    public class Apparel_MaliceCloak : Apparel
    {
        public const float ThrowRange = 24.9f;

        /// <summary>Whose cloak it is: the last one to wear or throw it.</summary>
        public Pawn owner;

        public override void Notify_Equipped(Pawn pawn) { base.Notify_Equipped(pawn); owner = pawn; }

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (var g in base.GetWornGizmos()) yield return g;
            var w = Wearer;
            if (w == null || w.Faction != Faction.OfPlayer) yield break;
            yield return new Command_Target
            {
                defaultLabel = "Throw Cloak",
                defaultDesc = "Throw the Malice Cloak. Where it lands it rises as a shade that grabs the aggro of anyone who sees it, " +
                              "except the wearer and the wearer's allies: a chance for most, certain for anyone with the Six Eyes. " +
                              "Blows on the shade wear the cloak down. Cast again to bring it back.",
                icon = def.uiIcon,
                targetingParams = new TargetingParameters { canTargetLocations = true, canTargetPawns = false, canTargetBuildings = false },
                action = t =>
                {
                    if (!t.Cell.InHorDistOf(w.Position, ThrowRange)) { Messages.Message("Too far to throw.", MessageTypeDefOf.RejectInput, false); return; }
                    MaliceCloak.Throw(w, this, t.Cell);
                },
            };
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "jjkCloakOwner");
        }
    }

    /// <summary>One thrown cloak, risen as a shade.</summary>
    public class MaliceShadeRun : IExposable
    {
        public Pawn shade, owner;
        public Apparel_MaliceCloak cloak;     // held by MaliceCloak while the shade stands
        public Vector3 from;
        public int start, land, until;
        public List<int> seen = new List<int>();

        public void ExposeData()
        {
            Scribe_References.Look(ref shade, "shade");
            Scribe_References.Look(ref owner, "owner");
            Scribe_References.Look(ref cloak, "cloak");
            Scribe_Values.Look(ref from, "from");
            Scribe_Values.Look(ref start, "start");
            Scribe_Values.Look(ref land, "land");
            Scribe_Values.Look(ref until, "until");
            Scribe_Collections.Look(ref seen, "seen", LookMode.Value);
            if (seen == null) seen = new List<int>();
        }
    }

    /// <summary>Throwing, the shades, aggro and recalling.</summary>
    public class MaliceCloak : GameComponent, IThingHolder
    {
        public const float AggroRange = 20f;
        public const float AggroChance = 0.35f;       // anyone else, once a throw
        public const int CheckEvery = 30;
        public const float FlySpeed = 0.5f;           // cells a tick while thrown
        public const int ShadeTicks = 3600;           // proposal: a minute, then it falls to the ground (recallable)
        public static readonly IntRange AggroTicks = new IntRange(480, 720);   // 8–12 s
        private static readonly Color Void = new Color(0.6f, 0.3f, 0.95f);
        private static readonly Color Smoke = new Color(0.15f, 0.08f, 0.2f);

        public static MaliceCloak Instance;
        private ThingOwner<Thing> held;
        public List<MaliceShadeRun> runs = new List<MaliceShadeRun>();
        /// <summary>Pawns whose aggro a shade holds, and until when.</summary>
        private static readonly Dictionary<Pawn, int> grabbed = new Dictionary<Pawn, int>();
        private static readonly HashSet<Pawn> struck = new HashSet<Pawn>();

        public MaliceCloak(Game game) { Instance = this; held = new ThingOwner<Thing>(this); grabbed.Clear(); struck.Clear(); }

        public IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => held;
        public void GetChildHolders(List<IThingHolder> outChildren) { }

        public static Apparel_MaliceCloak Worn(Pawn p) => p?.apparel?.WornApparel?.OfType<Apparel_MaliceCloak>().FirstOrDefault();
        public static bool IsShade(Thing t) => t is Pawn p && MaliceCloakDefOf.JJK_MaliceShade != null && p.kindDef == MaliceCloakDefOf.JJK_MaliceShade;
        public static MaliceShadeRun RunOf(Pawn shade) => Instance?.runs.FirstOrDefault(r => r.shade == shade);

        /// <summary>This pawn's cloak while it isn't on them: risen as a shade, lying somewhere, or with someone else.</summary>
        public static Apparel_MaliceCloak Out(Pawn owner)
        {
            if (owner == null || MaliceCloakDefOf.JJK_MaliceCloak == null) return null;
            var r = Instance?.runs.FirstOrDefault(x => x.owner == owner && x.cloak != null && !x.cloak.Destroyed);
            if (r != null) return r.cloak;
            // in their own pack (handed back when the shade's map went away)
            var packed = owner.inventory?.innerContainer?.OfType<Apparel_MaliceCloak>().FirstOrDefault();
            if (packed != null) return packed;
            foreach (var map in Find.Maps)
                foreach (var t in map.listerThings.ThingsOfDef(MaliceCloakDefOf.JJK_MaliceCloak))
                    if (t is Apparel_MaliceCloak c && c.owner == owner) return c;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p == owner) continue;
                    var c = p.inventory?.innerContainer?.OfType<Apparel_MaliceCloak>().FirstOrDefault(x => x.owner == owner)
                            ?? p.apparel?.WornApparel?.OfType<Apparel_MaliceCloak>().FirstOrDefault(x => x.owner == owner);
                    if (c != null) return c;
                }
            return null;
        }

        /// <summary>Off the thrower, out to the spot, and up as a shade there.</summary>
        public static bool Throw(Pawn owner, Apparel_MaliceCloak cloak, IntVec3 cell)
        {
            var map = owner?.Map;
            var inst = Instance;
            if (map == null || cloak == null || inst == null || MaliceCloakDefOf.JJK_MaliceShade == null || !cell.InBounds(map)) return false;
            if (!cell.Standable(map) && !CellFinder.TryFindRandomCellNear(cell, map, 3, c => c.Standable(map), out cell)) return false;
            Pawn shade;
            NoMagicClass.making = true;
            try { shade = PawnGenerator.GeneratePawn(new PawnGenerationRequest(MaliceCloakDefOf.JJK_MaliceShade, owner.Faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)); }
            finally { NoMagicClass.making = false; }
            var from = owner.DrawPos;
            owner.apparel.Remove(cloak);
            inst.held.TryAdd(cloak);
            cloak.owner = owner;
            shade.Name = new NameSingle("Malice Cloak");
            GenSpawn.Spawn(shade, cell, map);
            int now = Find.TickManager.TicksGame;
            var r = new MaliceShadeRun
            {
                shade = shade, owner = owner, cloak = cloak, from = from, start = now,
                land = now + Mathf.Max(20, Mathf.RoundToInt((cell.ToVector3Shifted() - from).MagnitudeHorizontal() / FlySpeed)),
            };
            r.until = r.land + ShadeTicks;
            inst.runs.Add(r);
            FleckMaker.ThrowDustPuffThick(from, map, 1.2f, Smoke);
            return true;
        }

        /// <summary>Back onto its owner, from wherever it is.</summary>
        public static bool Recall(Pawn owner)
        {
            var inst = Instance;
            if (owner == null || owner.Dead || owner.apparel == null || !owner.Spawned) return false;
            var r = inst?.runs.FirstOrDefault(x => x.owner == owner);
            Apparel_MaliceCloak c;
            if (r != null)
            {
                c = r.cloak;
                End(r, toGround: false);
                if (c == null || c.Destroyed) return false;
                inst.held.Remove(c);
            }
            else
            {
                c = Out(owner);
                if (c == null) return false;
                if (c.Spawned) { FleckMaker.ThrowDustPuffThick(c.DrawPos, c.Map, 1.4f, Smoke); c.DeSpawn(); }
                else if (c.ParentHolder is Pawn_ApparelTracker at) at.Remove(c);
                else c.holdingOwner?.Remove(c);
            }
            owner.apparel.Wear(c, true);
            FleckMaker.ThrowDustPuffThick(owner.DrawPos, owner.Map, 1.4f, Smoke);
            return true;
        }

        /// <summary>The shade goes. toGround: the cloak drops where it stood (time up); otherwise it stays held for Recall.</summary>
        private static void End(MaliceShadeRun r, bool toGround)
        {
            var inst = Instance;
            inst?.runs.Remove(r);
            var s = r.shade;
            IntVec3 at = s != null && s.Spawned ? s.Position : IntVec3.Invalid;
            var map = s?.Map;
            if (s != null)
            {
                foreach (var k in grabbed.Keys.ToList()) if (k.mindState?.enemyTarget == s) k.mindState.enemyTarget = null;
                if (s.Spawned) FleckMaker.ThrowDustPuffThick(s.DrawPos, s.Map, 1.6f, Smoke);
                if (!s.Destroyed) s.Destroy(DestroyMode.Vanish);
                // left as a world pawn: combat logs still point at it; the game clears it once nothing does
            }
            if (toGround && r.cloak != null && !r.cloak.Destroyed)
            {
                if (map != null && at.IsValid)
                {
                    inst?.held.Remove(r.cloak);
                    GenSpawn.Spawn(r.cloak, at, map);
                }
                else if (r.owner != null && !r.owner.Dead && r.owner.inventory != null)
                {
                    // the shade's map is gone (the owner left it): the cloak goes back to the owner's pack, not lost
                    inst?.held.Remove(r.cloak);
                    r.owner.inventory.innerContainer.TryAdd(r.cloak);
                }
            }
        }

        public static bool Ally(Pawn owner, Pawn p)
        {
            var a = owner?.Faction; var b = p.Faction;
            if (a == null || b == null) return false;
            return a == b || b.RelationKindWith(a) == FactionRelationKind.Ally;
        }

        public static bool SixEyes(Pawn p) => GojoPsycasts.Gojo.HasSixEyes(p);

        /// <summary>A blow on a shade: it comes off the cloak's hit points. At 0 the cloak is torn apart and the shade
        /// goes with it (proposal: destroyed, not dropped).</summary>
        public static void Hit(Pawn shade, float amount)
        {
            var r = RunOf(shade);
            if (r?.cloak == null) { struck.Add(shade); return; }
            r.cloak.HitPoints -= Mathf.Max(1, Mathf.RoundToInt(amount));
            if (shade.Spawned) MoteMaker.ThrowText(shade.DrawPos, shade.Map, $"{r.cloak.HitPoints}/{r.cloak.MaxHitPoints}", Void, 1.5f);
            if (r.cloak.HitPoints <= 0) struck.Add(shade);
        }

        public override void GameComponentTick()
        {
            if (struck.Count > 0)
                foreach (var s in struck.ToList())
                {
                    struck.Remove(s);
                    var r = RunOf(s);
                    if (r == null) { if (!s.Destroyed) s.Destroy(DestroyMode.Vanish); continue; }
                    var c = r.cloak;
                    End(r, toGround: false);
                    if (c != null && !c.Destroyed) { held.Remove(c); c.Destroy(); }     // torn apart
                    if (r.owner?.Faction == Faction.OfPlayer) Messages.Message("The Malice Cloak was torn apart.", MessageTypeDefOf.NegativeEvent, false);
                }
            int now = Find.TickManager.TicksGame;
            if (now % CheckEvery != 7 || runs.Count == 0) return;
            foreach (var k in grabbed.Where(e => e.Value < now || e.Key == null || e.Key.Dead).Select(e => e.Key).ToList()) grabbed.Remove(k);
            foreach (var r in runs.ToList())
            {
                if (r.shade == null || r.shade.Destroyed || !r.shade.Spawned || r.cloak == null || r.cloak.Destroyed) { End(r, toGround: true); continue; }
                if (now >= r.until) { End(r, toGround: true); continue; }   // a minute up: it falls, and can be recalled from there
                if (now < r.land) continue;
                Aggro(r, r.shade.Map, now);
            }
        }

        private static void Aggro(MaliceShadeRun r, Map map, int now)
        {
            var s = r.shade;
            if (now % 120 < CheckEvery) FleckMaker.ThrowDustPuffThick(s.DrawPos, map, 0.9f, new Color(0.35f, 0.15f, 0.5f));
            foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (p == r.owner || p == s || IsShade(p) || p.Dead || p.Downed || !p.Awake() || p.InMentalState || grabbed.ContainsKey(p)) continue;
                if (Ally(r.owner, p) || r.seen.Contains(p.thingIDNumber)) continue;
                if (!p.Position.InHorDistOf(s.Position, AggroRange) || !GenSight.LineOfSight(p.Position, s.Position, map, true)) continue;
                if (p.health?.capacities != null && p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) < 0.2f) continue;
                r.seen.Add(p.thingIDNumber);
                if (p.WorkTagIsDisabled(WorkTags.Violent)) continue;      // can't fight: not affected
                if (!Rand.Chance(SixEyes(p) ? 1f : AggroChance)) continue;
                Grab(p, s, AggroTicks.RandomInRange);
            }
        }

        /// <summary>The shade becomes their attack target: ranged shoot it from where they stand, melee go for it.</summary>
        private static void Grab(Pawn p, Pawn shade, int ticks)
        {
            var verb = p.TryGetAttackVerb(shade, true);
            if (verb == null) return;
            var job = JobMaker.MakeJob(verb.IsMeleeAttack ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, shade);
            job.expiryInterval = ticks;
            job.checkOverrideOnExpire = false;
            if (p.mindState != null) p.mindState.enemyTarget = shade;
            p.jobs.StartJob(job, JobCondition.InterruptForced);
            grabbed[p] = Find.TickManager.TicksGame + ticks;
            MoteMaker.ThrowText(p.DrawPos, p.Map, "Eyes on the cloak", Void, 2.5f);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Deep.Look(ref held, "jjkCloaksHeld", this);
            // runs whose shade is already gone are kept: the tick after loading ends them and hands the cloak back
            // (dropping them here stranded the cloak in the holder)
            Scribe_Collections.Look(ref runs, "jjkCloakShades", LookMode.Deep);
            if (held == null) held = new ThingOwner<Thing>(this);
            if (runs == null) runs = new List<MaliceShadeRun>();
            if (Scribe.mode == LoadSaveMode.LoadingVars) grabbed.Clear();
        }
    }

    /// <summary>Blows on a shade come off the cloak's hit points; the shade itself never takes damage.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_MaliceShadeHit
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (!MaliceCloak.IsShade(__instance)) return true;
            absorbed = true;
            if (dinfo.Def != null && dinfo.Def.harmsHealth) MaliceCloak.Hit(__instance, dinfo.Amount);
            return false;
        }
    }

    /// <summary>The shade flies out along an arc from the thrower, then hovers, bobbing a little.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    public static class Patch_MaliceShadeDrawPos
    {
        public static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            if (MaliceCloak.Instance == null || MaliceCloak.Instance.runs.Count == 0 || !MaliceCloak.IsShade(__instance)) return;
            var r = MaliceCloak.RunOf(__instance);
            if (r == null) return;
            int now = Find.TickManager.TicksGame;
            if (now < r.land)
            {
                float f = Mathf.InverseLerp(r.start, r.land, now);
                var at = Vector3.Lerp(r.from, __result, f);
                __result = new Vector3(at.x, __result.y, at.z + Mathf.Sin(f * Mathf.PI) * 1.2f);
                return;
            }
            __result = new Vector3(__result.x, __result.y, __result.z + 0.25f + Mathf.Sin(now * 0.05f) * 0.08f);
        }
    }

    /// <summary>"Recall Cloak" on whoever threw it, while it's away from them.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_MaliceCloakRecall
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            var p = __instance;
            if (p?.Faction != Faction.OfPlayer || p.apparel == null || MaliceCloak.IsShade(p) || MaliceCloak.Worn(p) != null) yield break;
            var c = MaliceCloak.Out(p);
            if (c == null) yield break;
            yield return new Command_Action
            {
                defaultLabel = "Recall Cloak",
                defaultDesc = "The Malice Cloak comes back onto its wearer, from wherever it is.",
                icon = c.def.uiIcon,
                action = () => MaliceCloak.Recall(p),
            };
        }
    }
}
