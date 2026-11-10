using System.Collections.Generic;
using RimWorld;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;
using VanillaPsycastsExpanded;

namespace SukunaPsycasts
{
    public class HediffCompProperties_VesselPassives : HediffCompProperties
    {
        public HediffCompProperties_VesselPassives() => compClass = typeof(HediffComp_VesselPassives);
    }

    /// <summary>
    /// Keeps the two passive-node hediffs in sync with what the pawn has actually
    /// unlocked. Black Flash and Cursed Energy Reinforcement are tree nodes but not castable abilities, so
    /// there is no cast to hang them off — this comp adds and removes them instead.
    ///
    /// Driven by CompAbilities.HasAbility, which is public, so this survives save/
    /// load and VPE's respec (Hediff_PsycastAbilities.Reset) with no extra work.
    /// </summary>
    public class HediffComp_VesselPassives : HediffComp
    {
        private const int CheckInterval = 120;
        private int sinceCheck;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            // Every tick, because it is a graphic: the fire bow has to keep pointing
            // at whatever the shot is aimed at for the whole draw.
            WarmupMoteAim.Aim(Pawn);

            sinceCheck += delta;
            if (sinceCheck < CheckInterval) return;
            sinceCheck = 0;
            Sync();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo) => Sync();

        private void Sync()
        {
            var pawn = Pawn;
            if (pawn == null || pawn.Dead) return;
            var comp = pawn.GetComp<CompAbilities>();
            if (comp == null) return;

            Match(pawn, comp, SukunaAbilityDefOf.Sukuna_BlackFlash,
                  SukunaDefOf.Sukuna_BlackFlash);
            // Cursed Energy Reinforcement is always on once learned; its toggle only
            // raises the output (Hediff_CursedEnergyReinforcement.Boosted).
            Match(pawn, comp, SukunaAbilityDefOf.Sukuna_SlashDefensePseudoInfinity,
                  SukunaDefOf.Sukuna_PseudoInfinity);

            HoldOffStarvation(pawn);
        }

        /// <summary>
        /// A vessel starves and keeps going. Malnutrition's lethalSeverity is 1, and
        /// Hediff.CauseDeathNow is severity >= lethalSeverity, so holding the severity
        /// just under it leaves them permanently at "extreme malnutrition" — every
        /// penalty of starving, none of the dying. He is not finished with the body
        /// yet.
        /// </summary>
        private const float StarvationCeiling = 0.99f;

        private static void HoldOffStarvation(Pawn pawn)
        {
            var hunger = pawn.health?.hediffSet?.GetFirstHediffOfDef(HediffDefOf.Malnutrition);
            if (hunger != null && hunger.Severity > StarvationCeiling)
                hunger.Severity = StarvationCeiling;
        }

        // 2026-11-07 (her report): loose points (psylink levels, dev mode, quest rewards) are no longer
        // turned into stats here. Only the cases she set do that: a finger's own point, a new adult
        // Gojo, Yuji's arrival and Modulo Yuji.

        public static void Match(Pawn pawn, CompAbilities comp, VEFAbilityDef node, HediffDef passive)
        {
            if (node == null || passive == null) return;
            bool unlocked = comp.HasAbility(node);
            var present = pawn.health.hediffSet.GetFirstHediffOfDef(passive);
            if (unlocked && present == null) pawn.health.AddHediff(passive);
            else if (!unlocked && present != null) pawn.health.RemoveHediff(present);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref sinceCheck, "sukunaSinceCheck", 0);
        }
    }
}
