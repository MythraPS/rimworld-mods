using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    /// <summary>
    /// A finger is not a free point to spend wherever you like. It grants a psycaster
    /// level, and that level is immediately committed: to the next Shrine slot that can
    /// legally open (every finger, no alternating), and once all nine are learned, to psycaster stats.
    /// </summary>
    public static class FingerProgression
    {
        /// <summary>
        /// <paramref name="pointsBefore"/> is the pawn's spendable-point count from
        /// BEFORE the finger granted a level. Anything above that came from the finger
        /// and is committed here — a finger is never a free point to spend on some
        /// other path.
        /// </summary>
        public static void SpendOnShrine(Pawn pawn, Hediff_PsycastAbilities psy, int pointsBefore)
        {
            if (pawn == null || psy == null) return;
            var comp = pawn.GetComp<CompAbilities>();
            if (comp == null) return;

            // No alternating (her call 2026-11-10): every finger opens the next Shrine technique. Once all nine are
            // known, every finger goes into stats.
            var next = NextUnlockable(pawn, comp, psy);

            if (next == null)
            {
                Drain(pawn, psy, pointsBefore);
                Say("{0} grows stronger — nothing left of the technique to learn."
                                 .Formatted(pawn.LabelShortCap),
                                 pawn, MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            comp.GiveAbility(next);
            if (psy.points > pointsBefore) psy.SpentPoints();
            Say("Shrine opens further: {0}.".Formatted(next.LabelCap),
                             pawn, MessageTypeDefOf.PositiveEvent, false);

            // Whatever the finger left spendable becomes stats. ImproveStats only adds
            // to statPoints — it does NOT consume a point — so SpentPoints has to be
            // called alongside it or the point stays available for another tree.
            Drain(pawn, psy, pointsBefore);
        }

        /// <summary>
        /// Yuji (her call 2026-11-10). Kenjaku sealed one finger inside him at birth (it counts as one of the twenty). The first
        /// finger he willingly eats gives Cursed Energy Reinforcement. Every one after that opens the next Shrine
        /// technique, and once Shrine is full they go into psycaster stats. The rest of the Fundamentals and Simple Domain
        /// he learns himself, with the points he earns.
        /// </summary>
        public static void YujiFinger(Pawn pawn, Hediff_PsycastAbilities psy, int pointsBefore, bool firstWilling)
        {
            var comp = pawn?.GetComp<CompAbilities>();
            if (comp == null || psy == null) return;
            if (firstWilling)
            {
                var path = JujutsuFundamentals.JJKDefOf.JJK_CursedEnergyFundamentals;
                if (path != null && !psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
                var reinf = JujutsuFundamentals.JJKAbilityDefOf.JJK_Reinforcement;
                if (reinf != null && !comp.HasAbility(reinf))
                {
                    comp.GiveAbility(reinf);
                    if (psy.points > pointsBefore) psy.SpentPoints();
                    Say("{0} learns Cursed Energy Reinforcement.".Formatted(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent, false);
                }
                Drain(pawn, psy, pointsBefore);
                return;
            }
            var next = NextUnlockable(pawn, comp, psy);
            if (next != null)
            {
                comp.GiveAbility(next);
                if (psy.points > pointsBefore) psy.SpentPoints();
                Say("Shrine opens further: {0}.".Formatted(next.LabelCap), pawn, MessageTypeDefOf.PositiveEvent, false);
            }
            else Say("{0} grows stronger — nothing left of Shrine to learn, so his psycaster stats rise.".Formatted(pawn.LabelShortCap),
                     pawn, MessageTypeDefOf.PositiveEvent, false);
            Drain(pawn, psy, pointsBefore);
        }

        private static void Say(string text, LookTargets look, MessageTypeDef type, bool history)
        {
            if (!CompCursedFinger.Silent) Messages.Message(text, look, type, history);
        }

        private static void Drain(Pawn pawn, Hediff_PsycastAbilities psy, int pointsBefore)
        {
            int guard = 0;
            while (psy.points > pointsBefore && guard++ < 64)
            {
                psy.ImproveStats();
                psy.SpentPoints();
            }
        }

        /// <summary>Lowest tier first, then declared order — so the tree fills bottom
        /// up the way it is drawn, and never skips a prerequisite.</summary>
        internal static VEFAbilityDef NextUnlockable(Pawn pawn, CompAbilities comp,
                                                    Hediff_PsycastAbilities psy)
        {
            var path = SukunaDefOf.Sukuna_Path;
            if (path?.abilities == null) return null;

            // Yuji's first node is Black Flash, ahead of everything else and without its
            // prerequisites (her call 2026-11-09). After that the tree fills in order.
            // (Yuji's Black Flash is innate now, through the shared Black Flash, 2026-11-10.)

            var candidates = path.abilities
                .Where(a => !comp.HasAbility(a) && !YujiNoWorldSlash.Blocked(pawn, a))
                .Select(a => new { def = a, ext = a.Psycast() })
                .Where(x => x.ext != null)
                .Where(x => x.ext.PrereqsCompleted(comp))
                .OrderBy(x => x.ext.level)
                .ThenBy(x => x.ext.order)
                .ToList();

            return candidates.FirstOrDefault()?.def;
        }

        /// <summary>One finger is enough to make a pawn morbid. It costs nothing —
        /// it is not a choice they made.</summary>
        public static void GrantMorbidFocus(Hediff_PsycastAbilities psy)
        {
            var morbid = DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Morbid");
            if (morbid == null || psy == null) return;
            if (psy.unlockedMeditationFoci == null) return;
            if (!psy.unlockedMeditationFoci.Contains(morbid))
                psy.unlockedMeditationFoci.Add(morbid);
        }
    }
}
