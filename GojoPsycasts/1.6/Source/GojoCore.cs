using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace GojoPsycasts
{
    [StaticConstructorOnStartup]
    public static class GojoStartup
    {
        static GojoStartup()
        {
            new Harmony("mythraps.satorugojo").PatchAll(Assembly.GetExecutingAssembly());
            Log.Message("[Satoru Gojo] Harmony patches applied.");
        }
    }

    [DefOf]
    public static class GojoDefOf
    {
        public static PsycasterPathDef Gojo_Path;
        public static TraitDef Gojo_SixEyes;
        public static HediffDef Gojo_Limitless;
        public static HediffDef Gojo_Infinity;
        public static HediffDef Gojo_BlueFist;
        public static HediffDef Gojo_BlackFlash;
        public static HediffDef Gojo_ReverseCursedTechnique;
        public static HediffDef Gojo_Speed;
        public static HediffDef Gojo_InfiniteVoid;
        public static HediffDef Gojo_Reinforcement;
        public static DamageDef Gojo_Force;
        public static DamageDef Gojo_Erase;
        public static ThingDef Gojo_BlueOrb, Gojo_BlueOrbSlow, Gojo_RedOrb, Gojo_RedOrbSlow, Gojo_PurpleOrb, Gojo_RedOrbHeld, Gojo_BlueCrater;
        public static ThingDef Gojo_DomainBarrier;
        public static ThingDef Gojo_VoidOverlay;
        public static FleckDef Gojo_Fleck_BlackSpark, Gojo_Fleck_RedBeam, Gojo_Fleck_PurpleTrail, Gojo_Fleck_BlueSwirl,
            Gojo_Fleck_RedBurst, Gojo_Fleck_RedSpark;
        public static HairDef Gojo_Hair;
        public static TattooDef Gojo_Blindfold;
        public static TattooDef Gojo_Eyes;
        public static TraitDef Gojo_Selfish;
        public static ThingDef Gojo_BlindfoldApparel;
        public static BackstoryDef Gojo_Childhood, Gojo_Adulthood;
        public static PawnKindDef Gojo_Satoru;
        public static IncidentDef Gojo_Arrives;
        static GojoDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GojoDefOf));
    }

    [DefOf]
    public static class GojoAbilityDefOf
    {
        public static VEFAbilityDef Gojo_Infinity, Gojo_Blue, Gojo_BlueFist, Gojo_BlackFlash, Gojo_Speed,
            Gojo_ReverseCursedTechnique, Gojo_Red, Gojo_Teleport, Gojo_Purple, Gojo_InfiniteVoid, Gojo_Reinforcement,
            Gojo_Pull, Gojo_Push, Gojo_Fly;
        static GojoAbilityDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(GojoAbilityDefOf));
    }

    public static class Gojo
    {
        /// <summary>Pawns on any map plus pawns travelling in caravans: everyone a sustained toggle should charge.</summary>
        public static IEnumerable<Pawn> ActivePawns()
        {
            foreach (var map in Find.Maps)
            {
                var l = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < l.Count; i++) yield return l[i];
            }
            foreach (var c in Find.WorldObjects.Caravans)
            {
                var l = c.PawnsListForReading;
                for (int i = 0; i < l.Count; i++) yield return l[i];
            }
        }

        private static StatDef costFactor;
        /// <summary>VPE's psyfocus cost factor, looked up once.</summary>
        public static StatDef CostFactorStat => costFactor ?? (costFactor = DefDatabase<StatDef>.GetNamed("VPE_PsyfocusCostFactor"));

        public static bool HasSixEyes(Pawn p) =>
            p?.story?.traits?.HasTrait(GojoDefOf.Gojo_SixEyes) == true;

        public static bool Knows(Pawn p, VEFAbilityDef def) =>
            p?.GetComp<CompAbilities>()?.HasAbility(def) == true;

        /// <summary>1 + current psyfocus: x1 on an empty bar, x2 on a full one (Blue and Red size, 2026-11-04).</summary>
        public static float PsyfocusBoost(Pawn p) =>
            1f + Mathf.Clamp01(p?.psychicEntropy?.CurrentPsyfocus ?? 0f);

        /// <summary>Everything scales on psychic sensitivity, which the Six Eyes raise.</summary>
        public static float Power(Pawn p) =>
            p == null ? 1f : Mathf.Max(0.1f, p.GetStatValue(StatDefOf.PsychicSensitivity));

        public static Hediff_PsycastAbilities Psycasts(Pawn pawn, bool create)
        {
            var psy = pawn.health.hediffSet.hediffs.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            if (psy != null || !create) return psy;
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
            if (def == null) return null;
            psy = pawn.health.AddHediff(def) as Hediff_PsycastAbilities;
            var link = pawn.health.hediffSet.hediffs.Find(h => h is Hediff_Psylink) as Hediff_Psylink;
            if (psy != null && link != null)
            {
                try { psy.InitializeFromPsylink(link); }
                catch (System.Exception e) { Log.Warning("[Satoru Gojo] InitializeFromPsylink failed: " + e.Message); }
            }
            return psy;
        }

        /// <summary>The Six Eyes come with the path open, Infinity learned and the
        /// Gojo hediff on. Safe to call repeatedly.</summary>
        public static void Awaken(Pawn pawn)
        {
            if (pawn?.health == null || !HasSixEyes(pawn)) return;
            if (!pawn.health.hediffSet.HasHediff(GojoDefOf.Gojo_Limitless))
                pawn.health.AddHediff(GojoDefOf.Gojo_Limitless);
            if (pawn.GetPsylinkLevel() < 1) pawn.ChangePsylinkLevel(1, false);
            var psy = Psycasts(pawn, true);
            if (psy == null) return;
            if (!psy.unlockedPaths.Contains(GojoDefOf.Gojo_Path)) psy.unlockedPaths.Add(GojoDefOf.Gojo_Path);
            var comp = pawn.GetComp<CompAbilities>();
            if (comp != null && !comp.HasAbility(GojoAbilityDefOf.Gojo_Infinity))
                comp.GiveAbility(GojoAbilityDefOf.Gojo_Infinity);
        }
    }

    /// <summary>
    /// Only the Six Eyes open Limitless. PsycasterPathDef's lock fields cover genes,
    /// memes, foci, mechanitors and backstories but nothing else, so the gate is a
    /// postfix on the public CanPawnUnlock.
    /// </summary>
    [HarmonyPatch(typeof(PsycasterPathDef), nameof(PsycasterPathDef.CanPawnUnlock))]
    public static class Patch_CanPawnUnlock
    {
        public static void Postfix(PsycasterPathDef __instance, Pawn pawn, ref bool __result)
        {
            if (__instance == GojoDefOf.Gojo_Path) __result = Gojo.HasSixEyes(pawn);
        }
    }

    /// <summary>
    /// Anyone holding the Six Eyes trait gets the path and the hediff, including a
    /// pawn the player gave the trait in an editor. Checked every few seconds rather
    /// than hooked, so it also catches saves and editors.
    ///
    /// Also (2026-10-28): Satoru himself is brought up to full strength once, which
    /// covers a Gojo already in a save; and a child of a Six Eyes parent is born with
    /// the Six Eyes, so Limitless at its basic level (Awaken gives psycast level 1).
    /// A child is looked at once, so removing the trait later in an editor sticks.
    /// </summary>
    public class SixEyesKeeper : GameComponent
    {
        private HashSet<int> done = new HashSet<int>();

        public SixEyesKeeper(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned.ToList())
                {
                    if (!p.RaceProps.Humanlike) continue;
                    if (!done.Contains(p.thingIDNumber))
                    {
                        done.Add(p.thingIDNumber);
                        if (p.kindDef == GojoDefOf.Gojo_Satoru) Satoru.FullStrength(p);
                        else if (!Gojo.HasSixEyes(p) && HasSixEyesParent(p))
                            p.story.traits.GainTrait(new Trait(GojoDefOf.Gojo_SixEyes, 0, true));
                    }
                    if (p.kindDef == GojoDefOf.Gojo_Satoru && (!Gojo.Knows(p, GojoAbilityDefOf.Gojo_Reinforcement)
                                                               || !p.story.traits.HasTrait(GojoDefOf.Gojo_Selfish)))
                        Satoru.FullStrength(p);   // Satoru from an older save: give him what was added since
                    if (Gojo.HasSixEyes(p) && !p.health.hediffSet.HasHediff(GojoDefOf.Gojo_Limitless))
                        Gojo.Awaken(p);
                }
        }

        private static bool HasSixEyesParent(Pawn p) =>
            p.relations?.DirectRelations?.Any(r => r.def == PawnRelationDefOf.Parent && Gojo.HasSixEyes(r.otherPawn)) == true;

        public override void ExposeData()
        {
            base.ExposeData();
            var l = done.ToList();
            Scribe_Collections.Look(ref l, "gojoKeeperDone", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars) done = l == null ? new HashSet<int>() : new HashSet<int>(l);
        }
    }

    public static class Fx
    {
        /// <summary>Black Flash: jagged black sparks with a red edge thrown out of the impact.</summary>
        public static void BlackSparks(Thing at)
        {
            if (at?.Map == null) return;
            for (int i = 0; i < 9; i++)
            {
                var data = FleckMaker.GetDataStatic(at.DrawPos, at.Map, GojoDefOf.Gojo_Fleck_BlackSpark, Rand.Range(0.8f, 1.9f));
                data.rotation = Rand.Range(0f, 360f);
                data.velocityAngle = Rand.Range(0f, 360f);
                data.velocitySpeed = Rand.Range(1.5f, 4.5f);
                at.Map.flecks.CreateFleck(data);
            }
        }

        public static void Fleck(FleckDef def, Vector3 at, Map map, float scale, float rotation = 0f)
        {
            if (map == null) return;
            var data = FleckMaker.GetDataStatic(at, map, def, scale);
            data.rotation = rotation;
            map.flecks.CreateFleck(data);
        }

        public static void Skip(IntVec3 cell, Map map)
        {
            if (map == null) return;
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipFlashEntry, 1.4f);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipInnerExit, 1f);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
        }

        /// <summary>Gojo's teleport: Blue pulls him to the destination and Red pushes him
        /// off the start, so both ends flash blue and red (her call 2026-10-31).</summary>
        public static void TeleportFlash(IntVec3 cell, Map map)
        {
            if (map == null) return;
            var at = cell.ToVector3Shifted();
            Tinted(FleckDefOf.PsycastSkipFlashEntry, at, map, 1.4f, new Color(0.25f, 0.55f, 1f));
            Tinted(FleckDefOf.PsycastSkipInnerExit, at, map, 1f, new Color(0.3f, 0.6f, 1f));
            Tinted(FleckDefOf.PsycastSkipOuterRingExit, at, map, 1f, new Color(1f, 0.2f, 0.2f));
            Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, at, map, 1.6f, Rand.Range(0f, 360f));
            Fleck(GojoDefOf.Gojo_Fleck_RedBeam, at, map, 1.4f, Rand.Range(0f, 360f));
        }

        private static void Tinted(FleckDef def, Vector3 at, Map map, float scale, Color color)
        {
            var data = FleckMaker.GetDataStatic(at, map, def, scale);
            data.instanceColor = color;
            map.flecks.CreateFleck(data);
        }

        /// <summary>Throw a pawn to <paramref name="dest"/> and stun them where they land.</summary>
        public static void Throw(Pawn victim, IntVec3 dest)
        {
            if (victim == null || !victim.Spawned || dest == victim.Position) return;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("PawnFlyer_Stun");
            if (def == null) return;
            var map = victim.Map; var from = victim.Position;
            var flyer = PawnFlyer.MakeFlyer(def, victim, dest, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, from, map);
        }

        /// <summary>The farthest standable cell from <paramref name="start"/> along
        /// <paramref name="dir"/>, up to <paramref name="cells"/> away, stopping at walls.</summary>
        public static IntVec3 Along(IntVec3 start, Vector3 dir, float cells, Map map)
        {
            var best = start;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return start;
            dir.Normalize();
            for (float d = 1f; d <= cells; d += 1f)
            {
                var c = (start.ToVector3Shifted() + dir * d).ToIntVec3();
                if (!c.InBounds(map) || !c.Walkable(map)) break;
                best = c;
            }
            return best;
        }

        /// <summary>Red's burst (her call 2026-11-04: a plain outward blast, not the beam picture):
        /// a red ring that grows to the burst's edge, and red sparks flying outward.</summary>
        public static void RedBlast(Vector3 at, Map map, float radius)
        {
            if (map == null) return;
            var ring = GojoDefOf.Gojo_Fleck_RedBurst;
            const float start = 1f;
            var data = FleckMaker.GetDataStatic(at, map, ring, start);
            // the ring grows growthRate per second; hold it solid until it reaches the edge
            data.solidTimeOverride = Mathf.Max(0f, (radius * 2f - start) / ring.growthRate - ring.fadeInTime);
            map.flecks.CreateFleck(data);
            int n = Mathf.Clamp(Mathf.RoundToInt(radius * 4f), 12, 60);
            for (int i = 0; i < n; i++)
            {
                var s = FleckMaker.GetDataStatic(at, map, GojoDefOf.Gojo_Fleck_RedSpark, Rand.Range(0.5f, 1.1f));
                s.velocityAngle = i * 360f / n + Rand.Range(-6f, 6f);
                s.velocitySpeed = radius * Rand.Range(2.4f, 3.2f);
                map.flecks.CreateFleck(s);
            }
        }

        public static void NotifyAttacked(Pawn victim, Pawn by)
        {
            if (victim == null || by == null || victim.Dead) return;
            victim.mindState?.Notify_DamageTaken(new DamageInfo(GojoDefOf.Gojo_Force, 0f, 0f, -1f, by));
        }
    }
}
