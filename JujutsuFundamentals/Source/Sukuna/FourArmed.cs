using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// The four-armed form (her call 2026-11-01). Sukuna takes it, for good, once he holds at least
    /// <see cref="MinFingers"/> fingers and is brought down to <see cref="HealthAt"/> of his health
    /// without dying. It brings the arms (Sukuna_FourArmed's render node) and the short pink King hair.
    /// </summary>
    public static class FourArmed
    {
        public const int MinFingers = 19;
        public const float HealthAt = 0.25f;

        public static bool Is(Pawn p) =>
            SukunaDefOf.Sukuna_FourArmed != null && p?.health?.hediffSet?.HasHediff(SukunaDefOf.Sukuna_FourArmed) == true;

        public static void Check(Pawn p)
        {
            if (p == null || p.Dead || p.Destroyed || !SukunaBehaviour.IsSukuna(p)) return;
            if (Is(p)) { EnsureParts(p); return; }
            if (Scaling.FingersEaten(p) < MinFingers) return;
            if (p.health.summaryHealth.SummaryHealthPercent > HealthAt) return;
            Take(p);
        }

        /// <summary>The second pair of arms, the abdominal mouth (and the mask, on the head), as torso hediffs so they are listed in
        /// the health tab (her call 2026-11-02). Also fills them in for a King from an older save.</summary>
        public static void EnsureParts(Pawn p)
        {
            var torso = p.RaceProps?.body?.corePart;
            if (torso == null) return;
            bool added = false;
            added |= Add(p, SukunaDefOf.Sukuna_ExtraArms, torso);
            added |= Add(p, SukunaDefOf.Sukuna_BellyMouth, torso);
            // the mask over his right eyes (2026-11-06), on the head
            var head = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == BodyPartDefOf.Head);
            if (head != null) added |= Add(p, SukunaDefOf.Sukuna_Mask, head);
            if (added) { p.Drawer?.renderer?.SetAllGraphicsDirty(); PortraitsCache.SetDirty(p); }
        }

        private static bool Add(Pawn p, HediffDef def, BodyPartRecord torso)
        {
            if (def == null || p.health.hediffSet.HasHediff(def)) return false;
            p.health.AddHediff(def, torso);
            return true;
        }

        public static void Take(Pawn p) => Take(p, true);

        public static void Take(Pawn p, bool letter)
        {
            if (!Is(p)) p.health.AddHediff(SukunaDefOf.Sukuna_FourArmed);   // never twice (the dev action can repeat)
            EnsureParts(p);
            SukunaLook.Apply(p, true);
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(p);
            if (letter) Find.LetterStack.ReceiveLetter("The King's true form",
                ("Cut down to a quarter of {0}'s strength, the King of Curses has stopped holding back. "
                 + "A second pair of arms tears out of {0}'s sides, a mouth splits open across the stomach, and the hair turns pink: this is the form "
                 + "he wore a thousand years ago, and he will not give it up again.").Formatted(p.LabelShortCap),
                LetterDefOf.ThreatBig, p);
        }
    }

    /// <summary>Right after a hit lands, while he is still alive to take it.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_FourArmedOnDamage
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance?.RaceProps?.Humanlike != true || !SukunaBehaviour.IsSukuna(__instance)) return;
            FourArmed.Check(__instance);
        }
    }
}

namespace SukunaPsycasts
{
    /// <summary>The King in his true form is drawn larger than a human (her call 2026-11-02). Scales the
    /// root draw matrix, so the head, hair, clothes and everything else scale with the body. Not in
    /// portraits, where he would not fit the frame.</summary>
    [HarmonyPatch(typeof(PawnRenderer), "GetDrawParms")]
    public static class Patch_TrueFormSize
    {
        public const float Scale = 1.35f;

        public static void Postfix(ref PawnDrawParms __result)
        {
            var p = __result.pawn;
            if (p == null || (__result.flags & PawnRenderFlags.Portrait) != 0 || !SukunaBehaviour.IsSukuna(p) || !FourArmed.Is(p)) return;
            __result.matrix = __result.matrix * UnityEngine.Matrix4x4.Scale(new UnityEngine.Vector3(Scale, 1f, Scale));
        }
    }
}
