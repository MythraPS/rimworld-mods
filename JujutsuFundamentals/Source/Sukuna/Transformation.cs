using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace SukunaPsycasts
{
    public static class Transformation
    {
        /// <summary>
        /// On, for ENEMIES ONLY (user 2026-10-15). Colonists, prisoners and slaves of
        /// the colony never turn, and neither does Yuji, wherever he is.
        /// </summary>
        public static readonly bool Enabled = true;

        /// <summary>
        /// TESTING (her call 2026-10-31): a colonist who turns stays in the colony as a friendly Sukuna
        /// instead of joining the King of Curses. (Colonists turn either way since 2026-11-10.) Off for players;
        /// switched per save from Debug actions > Sukuna (2026-11-06), saved by QuestlineTracker.
        /// </summary>
        public static bool FriendlyColonistsForTesting = false;

        private static bool Friendly(Pawn pawn) =>
            FriendlyColonistsForTesting && pawn?.Faction != null && pawn.Faction.IsPlayer;

        /// <summary>
        /// Base curve ((N-3)/16)^2, hard 100% at 19 and 20, reduced by the pawn's
        /// frozen pre-finger sensitivity. A natural psycaster holds him off longer.
        /// </summary>
        public static float ChanceAt(int fingers, Pawn pawn)
        {
            if (!Enabled) return 0f;
            if (!CanEverTransform(pawn)) return 0f;
            if (fingers >= 19) return 1f;
            if (fingers <= 3)  return 0f;
            float t = (fingers - 3f) / 16f;
            float baseChance = t * t;
            float resist = Mathf.Clamp(FrozenResistance.For(pawn), 0.5f, 3f);
            return Mathf.Clamp01(baseChance / resist);
        }

        /// <summary>
        /// Only ONE Sukuna can exist, and no pawn becomes him twice. A vessel who has
        /// already turned, or any map already holding him, closes the door.
        /// </summary>
        public static bool CanEverTransform(Pawn pawn)
        {
            if (!Enabled) return false;
            if (pawn == null || pawn.Dead) return false;
            // Everyone but Yuji can turn: colonists, prisoners and slaves too (her call 2026-11-10; colonists had been
            // left out only for testing). A colonist who turns becomes the King of Curses' and leaves, unless the
            // friendly testing switch is on.
            if (Yuji.Is(pawn) || ModuloYuji.Is(pawn)) return false;
            if (SukunaBehaviour.HasEverTransformed(pawn)) return false;
            if (SukunaBehaviour.AnySukunaExists()) return false;
            return true;
        }

        public static void Become(Pawn pawn) => Become(pawn, sendLetter: true);

        private static void SetTrait(Pawn p, string defName, int degree)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            var traits = p.story?.traits;
            if (def == null || traits == null) return;
            var have = traits.GetTrait(def);
            if (have != null && have.Degree == degree) return;
            if (have != null) traits.RemoveTrait(have);
            traits.GainTrait(new Trait(def, degree, true));
        }

        public static void Become(Pawn pawn, bool sendLetter) => Become(pawn, sendLetter, false);

        /// <summary><paramref name="forceHostile"/>: the questline takeover. Whoever gets the pinky turns,
        /// friend or foe, and always as the enemy.</summary>
        public static void Become(Pawn pawn, bool sendLetter, bool forceHostile)
        {
            if (pawn == null || pawn.Dead) return;
            if (!forceHostile && !CanEverTransform(pawn)) return;
            if (forceHostile && (Yuji.Is(pawn) || SukunaBehaviour.HasEverTransformed(pawn))) return;

            var vessel = pawn.health.hediffSet.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            int fingers = vessel == null ? 1 : Mathf.RoundToInt(vessel.Severity);

            // Power scales with how many he got. Turning at 5 is survivable;
            // turning at 17 is a colony-ending event.
            float power = Mathf.Clamp01(fingers / 20f);

            if (pawn.Map != null)
            {
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.jobs?.StopAll();
            }
            bool friendly = !forceHostile && Friendly(pawn);
            if (!friendly) SukunaFaction.Claim(pawn);
            SukunaBehaviour.Rename(pawn);
            SukunaLook.TearOffTop(pawn);
            SukunaLook.Apply(pawn);
            SetTrait(pawn, "SpeedOffset", 2);   // jogger (her call 2026-10-28)
            SetTrait(pawn, "Tough", 0);
            SetTrait(pawn, "Sukuna_Selfish", 0);   // her call 2026-11-01
            Hypersensitive.Ensure(pawn);           // her call 2026-11-07
            // Sukuna has all of Cursed Energy Fundamentals and Hollow Wicker Basket (her call 2026-11-10)
            JujutsuFundamentals.Jujutsu.LearnAllFundamentals(pawn);
            JujutsuFundamentals.Jujutsu.LearnBasket(pawn);
            VesselMaker.ArmForAI(pawn);

            if (sendLetter && friendly) Find.LetterStack.ReceiveLetter(
                "The King of Curses (test)",
                ("{0} has eaten {1} of the twenty fingers and Sukuna has taken over. For testing he stays " +
                 "in the colony and takes orders.").Formatted(pawn.LabelShortCap, fingers),
                LetterDefOf.NeutralEvent, pawn);
            else if (sendLetter) Find.LetterStack.ReceiveLetter(
                "The King of Curses",
                ("{0} has eaten {1} of the twenty fingers, and there is not enough of them left " +
                 "to hold the rest of him down. Whatever is standing there now belongs to no faction " +
                 "and is not going to negotiate. Every finger inside him falls out when he dies.")
                    .Formatted(pawn.LabelShortCap, fingers),
                LetterDefOf.ThreatBig, pawn);

            SukunaBehaviour.MarkTransformed(pawn, power);
        }
    }
}
