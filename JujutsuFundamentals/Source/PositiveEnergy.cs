using RimWorld;
using Verse;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Positive Energy (her call 2026-11-14): a toggle every pawn with Reverse Cursed Technique gets. While it's on, their
    /// attacks carry reverse cursed energy and do x2 damage to anomalies (Anomaly's entities and mutants, the cursed
    /// spirits included), with every kind of attack. It replaces the free, always-on x2 that RCT used to give.
    /// It costs nothing while idle: each hit that lands on an anomaly costs a little psyfocus (her call), and with no
    /// psyfocus to pay the hit lands without the bonus. Canon: positive energy is fatal to most curses (ch.175, Yuta
    /// and Kurourushi).
    /// </summary>
    [StaticConstructorOnStartup]
    public static class PositiveEnergy
    {
        public const string Key = "positiveenergy";
        public const float Factor = 2f;          // her call 2026-11-14
        /// <summary>Psyfocus per hit on an anomaly, before VPE's cost factor (the Six Eyes halve it). Proposal.</summary>
        public const float CostPerHit = 0.01f;

        private static VEFAbilityDef ability; private static HediffDef hediff; private static bool looked;
        public static VEFAbilityDef AbilityDef { get { Look(); return ability; } }
        public static HediffDef HediffDef { get { Look(); return hediff; } }
        private static void Look()
        {
            if (looked) return;
            looked = true;
            ability = DefDatabase<VEFAbilityDef>.GetNamedSilentFail("JJK_PositiveEnergy");
            hediff = DefDatabase<HediffDef>.GetNamedSilentFail("JJK_PositiveEnergy");
        }

        static PositiveEnergy()
        {
            // no drain while up: the cost is per hit (it still lapses at zero psyfocus, like the other toggles)
            Toggles.Add(new ToggleSpec { key = Key, title = "Positive energy", drain = 0f,
                hediff = () => HediffDef, ability = () => AbilityDef });
        }

        /// <summary>The toggle is on and the pawn still knows Reverse Cursed Technique.</summary>
        public static bool Active(Pawn attacker) =>
            ModsConfig.AnomalyActive && attacker != null && Toggles.IsOn(attacker, Key)
            && Jujutsu.Knows(attacker, JJKAbilityDefOf.JJK_ReverseCursedTechnique);

        /// <summary>Takes the per-hit psyfocus. False if there isn't enough: that hit gets no bonus.</summary>
        public static bool TryPay(Pawn attacker)
        {
            var tr = attacker?.psychicEntropy;
            if (tr == null) return false;
            float cost = CostPerHit * Toggles.CostFactorOf(attacker);
            if (tr.CurrentPsyfocus < cost) return false;
            tr.OffsetPsyfocusDirectly(-cost);
            return true;
        }

        /// <summary>Everyone who knows Reverse Cursed Technique has the toggle; anyone who no longer does loses it
        /// (and it switches off).</summary>
        public static void Sync(Pawn pawn, VEF.Abilities.CompAbilities comp)
        {
            var def = AbilityDef;
            if (def == null || JJKAbilityDefOf.JJK_ReverseCursedTechnique == null) return;
            bool rct = comp.HasAbility(JJKAbilityDefOf.JJK_ReverseCursedTechnique);
            bool has = comp.HasAbility(def);
            if (rct && !has) comp.GiveAbility(def);
            else if (!rct && has)
            {
                Toggles.Set(pawn, Key, false, quiet: true);
                comp.LearnedAbilities.RemoveAll(a => a.def == def);
            }
        }
    }
}
