using System.Linq;
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

            // Heavenly Restriction (her calls 2026-11-10): the first finger anyone with it eats (Yuji, Toji, Maki)
            // opens a loophole that bypasses it. The restriction stays, and so does the superhuman body.
            bool firstWilling = false;   // Yuji: his first eaten finger (the sealed one is already counted)
            if (JujutsuFundamentals.ZeroCursedEnergy.NoPsycasts(pawn))   // full, or Maki's incomplete one
            {
                JujutsuFundamentals.ZeroCursedEnergy.Bypass(pawn);
                firstWilling = Yuji.Is(pawn);
                if (!Silent) Messages.Message($"The finger opens a loophole in {pawn.LabelShort}'s Heavenly Restriction: it stays, but is bypassed, and cursed energy floods in.", pawn, MessageTypeDefOf.PositiveEvent, false);
            }

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

            // Yuji, with Sukuna still inside him and the "after Sukuna leaves" setting (default, her call 2026-11-12):
            // the finger feeds Sukuna, not him. Its level waits until Sukuna leaves (YujiStart.GrantBanked).
            if (Yuji.Is(pawn) && !firstWilling && YujiStart.ShrineAfterSukunaLeaves && YujiStart.SukunaInside(pawn))
            {
                var arcB = SukunaArc.Instance;
                if (arcB != null) arcB.bankedYujiLevels++;
                if (!Silent)
                    Messages.Message("{0} swallows the finger. ({1}/20) Its power goes to Sukuna for now; it will be Yuji's once Sukuna is out of him."
                                         .Formatted(pawn.LabelShortCap, n), pawn, MessageTypeDefOf.NeutralEvent, false);
                return;
            }

            // 2. A PSYLINK level. Without this the pawn has no psycaster bar at all,
            //    VPE never creates Hediff_PsycastAbilities, and the path cannot appear.
            pawn.ChangePsylinkLevel(1, false);
            // no psylink took (no brain, or something else blocks it): the finger is in them, but gives nothing.
            // VPE's level-up would throw without one (test runner 2026-11-12).
            if (!pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Psylink)) return;

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
                if (Yuji.Is(pawn)) FingerProgression.YujiFinger(pawn, psy, pointsBefore, firstWilling);
                else FingerProgression.SpendOnShrine(pawn, psy, pointsBefore);
                if (Yuji.Is(pawn) && firstWilling)
                {
                    CompDeathPainting.GrantSaved(pawn);
                    if (pawn.style != null && !ModuloYuji.Is(pawn))   // the marks under his eyes appear now
                    {
                        pawn.style.FaceTattoo = SukunaQuestDefOf.Sukuna_YujiMarks;
                        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                        if (pawn.Spawned) PortraitsCache.SetDirty(pawn);
                    }
                }
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
        /// <summary>The game's own psylink, which VPE's ChangeLevel needs. False (with the reason logged once) if it
        /// can't be had, so callers skip the level change instead of throwing.</summary>
        public static bool EnsurePsylink(Pawn pawn)
        {
            if (pawn?.health == null) return false;
            if (pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Psylink)) return true;
            pawn.ChangePsylinkLevel(1, false);
            if (pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Psylink)) return true;
            Log.WarningOnce($"[JJK] {pawn.LabelShort} can't be given a psylink (brain {pawn.health.hediffSet.GetBrain() != null}, dead {pawn.Dead}, "
                            + $"no-psycast restriction {JujutsuFundamentals.ZeroCursedEnergy.NoPsycasts(pawn)}): their psycast level is left as it is.", pawn.thingIDNumber ^ 0x51a7);
            return false;
        }

        public static Hediff_PsycastAbilities Ensure(Pawn pawn)
    {
        // VPE's ChangeLevel needs the game's own psylink there, and throws without one (test runner 2026-11-12:
        // Yuji ascending after his finger levels had no psylink). Give one first if it's missing.
        if (!pawn.health.hediffSet.hediffs.Any(h => h is Hediff_Psylink)) pawn.ChangePsylinkLevel(1, false);
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
