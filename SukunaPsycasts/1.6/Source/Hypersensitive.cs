using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Sukuna and Yuji are psychically hypersensitive (her call 2026-11-07): the
    /// vanilla trait, degree 2. Given when they are made and topped up on older saves.</summary>
    public static class Hypersensitive
    {
        private static TraitDef def;
        private static TraitDef Def => def ?? (def = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity"));

        public static void Ensure(Pawn p)
        {
            var traits = p?.story?.traits;
            if (Def == null || traits == null) return;
            var have = traits.GetTrait(Def);
            if (have != null && have.Degree == 2) return;
            if (have != null) traits.RemoveTrait(have);
            traits.GainTrait(new Trait(Def, 2, true));
        }
    }
}
