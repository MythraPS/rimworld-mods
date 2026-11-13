using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>The characters' own ideoligions (her picks 2026-11-04). Each is made once per game,
    /// with exactly the chosen memes (plus whatever structure the game picks): the game may add up
    /// to two random memes of its own, so it is regenerated until none are added.</summary>
    public class CharacterIdeos : GameComponent
    {
        private static Dictionary<string, Ideo> made = new Dictionary<string, Ideo>();
        private List<string> keysWork; private List<Ideo> valsWork;   // must live across load stages

        public CharacterIdeos(Game game) { made = new Dictionary<string, Ideo>(); }

        /// <summary>The ideoligion for <paramref name="key"/>, made the first time. Null without Ideology.</summary>
        /// <summary>The Christian structure's deity names are few, and making these ideoligions takes many tries: they ran
        /// out and the game logged an error (test runner 2026-11-12). Left out of the characters' ideoligions.</summary>
        private static System.Collections.Generic.List<MemeDef> NoChristian()
        {
            var m = DefDatabase<MemeDef>.GetNamedSilentFail("Structure_OriginChristian");
            return m == null ? null : new System.Collections.Generic.List<MemeDef> { m };
        }

        public static Ideo Get(string key, FactionDef forFaction, params string[] memeNames)
        {
            if (!ModsConfig.IdeologyActive || Find.IdeoManager == null) return null;
            if (made.TryGetValue(key, out var have) && have != null && Find.IdeoManager.IdeosListForReading.Contains(have)) return have;
            var forced = memeNames.Select(n => DefDatabase<MemeDef>.GetNamedSilentFail(n)).Where(m => m != null).ToList();
            if (forced.Count == 0) return null;
            Ideo best = null;
            for (int i = 0; i < 40; i++)
            {
                Ideo ideo;
                try { ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(forFaction, disallowedMemes: NoChristian(), forcedMemes: forced)); }
                catch (System.Exception e) { Log.Warning("[SukunaPsycasts] ideoligion generation failed: " + e.Message); continue; }
                if (ideo == null) continue;
                bool all = forced.All(m => ideo.memes.Contains(m));
                bool extra = ideo.memes.Any(m => m.category != MemeCategory.Structure && !forced.Contains(m));
                if (all && !extra) { best = ideo; break; }
                if (best == null && all) best = ideo;
            }
            if (best == null) return null;
            Find.IdeoManager.Add(best);
            made[key] = best;
            return best;
        }

        public static void Give(Pawn p, Ideo ideo)
        {
            ideo = JujutsuFundamentals.IdeoJoin.Pick(p, ideo);
            if (p?.ideo == null || ideo == null || p.Ideo == ideo) return;
            p.ideo.SetIdeo(ideo);
        }

        public static bool Has(Ideo i) => i != null && made.ContainsValue(i);

        public override void ExposeData()
        {
            base.ExposeData();
            // an ideoligion the game has since removed (nobody follows it): forget it rather than save a dead reference
            if (Scribe.mode == LoadSaveMode.Saving && made != null)
                foreach (var k in made.Keys.ToList())
                    if (made[k] == null || Find.IdeoManager == null || !Find.IdeoManager.IdeosListForReading.Contains(made[k])) made.Remove(k);
            Scribe_Collections.Look(ref made, "sukunaCharacterIdeos", LookMode.Value, LookMode.Reference, ref keysWork, ref valsWork);
            if (made == null) made = new Dictionary<string, Ideo>();
        }
    }
}
