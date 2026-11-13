using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKZeroDefOf
    {
        public static TraitDef JJK_HeavenlyRestriction;
        public static HediffDef JJK_ZeroCursedEnergyBody;
        static JJKZeroDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKZeroDefOf));
    }

    /// <summary>
    /// Heavenly Restriction: Zero Cursed Energy (her calls 2026-11-10), Toji's and awakened Maki's. Absolutely no
    /// psychic sensitivity and no psycasts: a psylink (or VPE's psycaster hediff) can never be added, and any already
    /// there goes. A superhuman body (the trait's stats plus the JJK_ZeroCursedEnergyBody hediff for capacities). Canon:
    /// a domain's sure hit can't lock onto them, so they are immune to sure hits.
    /// </summary>
    public static class ZeroCursedEnergy
    {
        /// <summary>Zero cursed energy right now: the restriction, not bypassed (degree 0).</summary>
        public static bool Is(Pawn p) => p?.story?.traits != null && JJKZeroDefOf.JJK_HeavenlyRestriction != null
                                         && p.story.traits.GetTrait(JJKZeroDefOf.JJK_HeavenlyRestriction)?.Degree == 0;

        /// <summary>Has the restriction at all, bypassed or not.</summary>
        public static bool Restricted(Pawn p) => p?.story?.traits != null && JJKZeroDefOf.JJK_HeavenlyRestriction != null
                                                 && p.story.traits.HasTrait(JJKZeroDefOf.JJK_HeavenlyRestriction);

        /// <summary>Never a psycaster: zero cursed energy, or Maki's incomplete restriction before her awakening (her call
        /// 2026-11-12: nothing, and she can't get any).</summary>
        public static bool NoPsycasts(Pawn p) => Is(p) || PartialClosed(p);

        /// <summary>Maki's incomplete restriction, not bypassed (degree 0).</summary>
        public static bool PartialClosed(Pawn p) => p?.story?.traits != null && MakiForms.Partial != null
                                                    && p.story.traits.GetTrait(MakiForms.Partial)?.Degree == 0;

        private static bool IsPsycastHediff(Hediff h) =>
            h is Hediff_Psylink || h is VanillaPsycastsExpanded.Hediff_PsycastAbilities;

        /// <summary>Who else has the superhuman body without the restriction (her call 2026-11-10: Yuji after his first
        /// finger, and Modulo Yuji). Registered by the Sukuna code.</summary>
        public static readonly List<Func<Pawn, bool>> SuperhumanBody = new List<Func<Pawn, bool>>();

        public static bool HasBody(Pawn p) => Restricted(p) || SuperhumanBody.Any(f => f(p));

        /// <summary>Keep the body hediff on, and (with the restriction) any psylink off. Called from PassiveSync.</summary>
        public static void Sync(Pawn p)
        {
            if (p?.health == null) return;
            var hs = p.health.hediffSet;
            var bodyDef = JJKZeroDefOf.JJK_ZeroCursedEnergyBody;
            if (bodyDef != null)
            {
                var body = hs.GetFirstHediffOfDef(bodyDef);
                bool should = HasBody(p);
                if (should && body == null) p.health.AddHediff(bodyDef);
                else if (!should && body != null) p.health.RemoveHediff(body);   // e.g. a four-armed Sukuna exorcised from a normal host
            }
            // Maki before her awakening is psychically dull (her call 2026-11-12); older saves get it here
            var sens = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
            if (sens != null && !Is(p) && NoPsycasts(p) && p.story?.traits != null && !p.story.traits.HasTrait(sens))
                p.story.traits.GainTrait(new Trait(sens, -1, true));
            if (!NoPsycasts(p)) return;
            for (int i = hs.hediffs.Count - 1; i >= 0; i--)
                if (IsPsycastHediff(hs.hediffs[i])) p.health.RemoveHediff(hs.hediffs[i]);
        }

        /// <summary>Bypass the restriction through a loophole (Yuji's first eaten finger, her call 2026-11-10). It stays,
        /// shown as bypassed: psychic sensitivity, psycasts and sure hits come back; the superhuman body stays.</summary>
        public static void Bypass(Pawn p)
        {
            var t = p?.story?.traits?.GetTrait(JJKZeroDefOf.JJK_HeavenlyRestriction);
            if (t != null && t.Degree == 0)
            {
                p.story.traits.RemoveTrait(t);
                p.story.traits.GainTrait(new Trait(JJKZeroDefOf.JJK_HeavenlyRestriction, 1, true));
            }
            // Maki's incomplete one too (her call 2026-11-12): the dullness it gave her goes with it
            var part = MakiForms.Partial != null ? p?.story?.traits?.GetTrait(MakiForms.Partial) : null;
            if (part != null && part.Degree == 0)
            {
                p.story.traits.RemoveTrait(part);
                p.story.traits.GainTrait(new Trait(MakiForms.Partial, 1, true));
                var sens = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
                var dull = sens != null ? p.story.traits.GetTrait(sens) : null;
                if (dull != null && dull.Degree < 0) p.story.traits.RemoveTrait(dull);
            }
            Sync(p);
        }

        [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
            new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
        public static class Patch_NoPsylink
        {
            [HarmonyPriority(Priority.First)]
            public static bool Prefix(Hediff hediff) => !(hediff != null && IsPsycastHediff(hediff) && NoPsycasts(hediff.pawn));
        }
    }
}
