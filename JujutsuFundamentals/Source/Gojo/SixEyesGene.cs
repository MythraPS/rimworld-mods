using RimWorld;
using Verse;

namespace GojoPsycasts
{
    /// <summary>
    /// The Six Eyes as a Biotech gene (her call 2026-11-12): normal gene rules, so a child gets it half the time from
    /// one parent who has it and always from two. The gene brings the Six Eyes trait. Anyone with the trait but not
    /// the gene (Gojo, a child from an older save, an editor) is given the gene, so it passes on.
    /// Limitless is separate: see Innate.
    /// </summary>
    public static class SixEyesGene
    {
        private static GeneDef def;
        public static GeneDef Def => def ?? (def = DefDatabase<GeneDef>.GetNamedSilentFail("Gojo_SixEyesGene"));

        public static void Keep(Pawn p)
        {
            if (!ModsConfig.BiotechActive || p?.genes == null || p.story?.traits == null || Def == null || !Gojo.HasSixEyes(p)) return;
            if (p.genes.HasEndogene(Def) || p.genes.HasXenogene(Def)) return;
            // the gene brings the trait itself: take the old one off first so it isn't there twice
            var t = p.story.traits.GetTrait(GojoDefOf.Gojo_SixEyes);
            if (t != null && t.sourceGene == null) p.story.traits.RemoveTrait(t);
            p.genes.AddGene(Def, false);
            if (!Gojo.HasSixEyes(p)) p.story.traits.GainTrait(new Trait(GojoDefOf.Gojo_SixEyes, 0, true));
        }
    }
}
