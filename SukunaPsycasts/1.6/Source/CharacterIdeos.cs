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
                try { ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(forFaction, forcedMemes: forced)); }
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
            if (p?.ideo == null || ideo == null || p.Ideo == ideo) return;
            p.ideo.SetIdeo(ideo);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref made, "sukunaCharacterIdeos", LookMode.Value, LookMode.Reference, ref keysWork, ref valsWork);
            if (made == null) made = new Dictionary<string, Ideo>();
        }
    }
}
