using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;

namespace SukunaPsycasts
{
    public class CompProperties_CursedFinger : CompProperties
    {
        public float sensitivityPerFinger = 0.05f;
        public CompProperties_CursedFinger() => compClass = typeof(CompCursedFinger);
    }

    public class CompCursedFinger : ThingComp
    {
        public override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);
            Consume(ingester);
        }

        /// <summary>True while an NPC vessel is being made: no messages for the
        /// player about a raider's progress.</summary>
        public static bool Silent;

        public static void Consume(Pawn pawn) => Consume(pawn, rollTakeover: true);

        public static void Consume(Pawn pawn, bool rollTakeover)
        {
            if (pawn == null || pawn.Dead) return;

            // 1. Vessel severity == fingers eaten by THIS pawn. Per-pawn, not global.
            var vessel = pawn.health.hediffSet.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            if (vessel == null)
            {
                vessel = pawn.health.AddHediff(SukunaDefOf.Sukuna_Vessel);
                vessel.Severity = 1f;
                FrozenResistance.Record(pawn);
            }
            else vessel.Severity = Mathf.Min(vessel.Severity + 1f, 20f);
            SukunaArc.Imprint(pawn);   // Shrine stays learnable after the fingers are gone

            int n = Mathf.RoundToInt(vessel.Severity);

            // 2. A PSYLINK level. Without this the pawn has no psycaster bar at all,
            //    VPE never creates Hediff_PsycastAbilities, and the path cannot appear.
            pawn.ChangePsylinkLevel(1, false);

            // 3. One psycast level, and the path unlocked outright — swallowing a
            //    finger IS the unlock; it should not also cost a point.
            //
            //    VPE keeps its own hediff (VPE_PsycastAbilityImplant) separate from the
            //    vanilla psylink, and it is NOT guaranteed to exist the moment the
            //    psylink is granted. Previously this ran, found null, and silently did
            //    nothing — no path, no ability, no focus. Create it if missing.
            var psy = PsycastSetup.Ensure(pawn);
            if (psy != null)
            {
                if (!psy.unlockedPaths.Contains(SukunaDefOf.Sukuna_Path))
                    psy.unlockedPaths.Add(SukunaDefOf.Sukuna_Path);

                int pointsBefore = psy.points;      // anything above this is the finger's
                psy.ChangeLevel(1);
                FingerProgression.GrantMorbidFocus(psy);
                FingerProgression.SpendOnShrine(pawn, psy, pointsBefore);
            }

            if (psy == null)
                Log.Warning("[Ryomen Sukuna] " + pawn.LabelShort + " swallowed a finger but "
                          + "no psycast hediff could be created — no path, no ability granted.");

            // 4. Roll for transformation.
            if (!Silent)
                Messages.Message(
                    "{0} swallows the finger. ({1}/20)".Formatted(pawn.LabelShortCap, n),
                    pawn, MessageTypeDefOf.NeutralEvent, false);

            if (rollTakeover && Rand.Chance(Transformation.ChanceAt(n, pawn))) Transformation.Become(pawn);
        }
    }

    internal static class PsycastSetup
    {
        public static Hediff_PsycastAbilities Ensure(Pawn pawn)
    {
        var psy = pawn.health.hediffSet.hediffs.Find(h => h is Hediff_PsycastAbilities)
                    as Hediff_PsycastAbilities;
        if (psy != null) return psy;

        var def = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
        if (def == null)
        {
            Log.Warning("[Ryomen Sukuna] VPE_PsycastAbilityImplant not found — "
                      + "cannot grant Shrine progression.");
            return null;
        }

        psy = pawn.health.AddHediff(def) as Hediff_PsycastAbilities;
        if (psy == null) return null;

        var link = pawn.health.hediffSet.hediffs.Find(h => h is Hediff_Psylink) as Hediff_Psylink;
        if (link != null)
        {
            try { psy.InitializeFromPsylink(link); }
            catch (System.Exception e)
            {
                Log.Warning("[Ryomen Sukuna] InitializeFromPsylink failed: " + e.Message);
            }
        }
        return psy;
        }
    }

    /// <summary>
    /// Resistance reads the pawn's INNATE psychic sensitivity, frozen at the moment
    /// they ate their first finger, so the sensitivity the fingers themselves grant
    /// can never feed back into resisting them.
    /// </summary>
    public class FrozenResistance : GameComponent
    {
        private static Dictionary<int, float> frozen = new Dictionary<int, float>();

        public FrozenResistance(Game game) { frozen = new Dictionary<int, float>(); }

        public static void Record(Pawn p)
        {
            if (p != null && !frozen.ContainsKey(p.thingIDNumber))
                frozen[p.thingIDNumber] = Scaling.Sensitivity(p);
        }

        public static float For(Pawn p)
        {
            if (p == null) return 1f;
            return frozen.TryGetValue(p.thingIDNumber, out float v) ? v : Scaling.Sensitivity(p);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref frozen, "sukunaFrozenResistance",
                                    LookMode.Value, LookMode.Value);
            if (frozen == null) frozen = new Dictionary<int, float>();
        }
    }
}
