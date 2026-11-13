using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Gojo's and Sukuna's own Black Flash, Reverse Cursed Technique and Cursed Energy Reinforcement (and Sukuna's
    /// Air Step) became the shared Cursed Energy Fundamentals techniques (2026-11-10). Old saves: whoever learned an
    /// old one gets the shared one instead, the old hediffs go, and Sukuna's switched-on toggles stay on.
    /// Runs on load and every few seconds (cheap: only pawns with abilities, only while something is left to convert).
    /// </summary>
    public class SharedMigration : GameComponent
    {
        public static readonly Dictionary<string, string> Moved = new Dictionary<string, string>
        {
            { "Sukuna_SlashDefensePseudoInfinity", "JJK_Reinforcement" },
            { "Sukuna_ReverseCursedTechnique", "JJK_ReverseCursedTechnique" },
            { "Sukuna_BlackFlash", "JJK_BlackFlash" },
            { "Sukuna_AirStep", "JJK_AirStep" },
            { "Gojo_Reinforcement", "JJK_Reinforcement" },
            { "Gojo_ReverseCursedTechnique", "JJK_ReverseCursedTechnique" },
            { "Gojo_BlackFlash", "JJK_BlackFlash" },
        };

        private static readonly string[] OldHediffs =
        {
            "Sukuna_PseudoInfinity", "Sukuna_BlackFlash", "Sukuna_ReverseCursedTechnique",
            "Gojo_Reinforcement", "Gojo_BlackFlash", "Gojo_ReverseCursedTechnique",
        };

        private static List<(VEFAbilityDef from, VEFAbilityDef to)> pairs;
        private static List<HediffDef> oldHediffs;

        public SharedMigration(Game game) { }

        private static void Resolve()
        {
            if (pairs != null) return;
            pairs = new List<(VEFAbilityDef, VEFAbilityDef)>();
            foreach (var kv in Moved)
            {
                var a = DefDatabase<VEFAbilityDef>.GetNamedSilentFail(kv.Key);
                var b = DefDatabase<VEFAbilityDef>.GetNamedSilentFail(kv.Value);
                if (a != null && b != null) pairs.Add((a, b));
            }
            oldHediffs = OldHediffs.Select(n => DefDatabase<HediffDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
        }

        public override void LoadedGame() { base.LoadedGame(); RunAll(); }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 == 31) RunAll();
        }

        private static void RunAll()
        {
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned.ToList()) Convert(p);
            foreach (var c in Find.WorldObjects.Caravans)
                foreach (var p in c.PawnsListForReading.ToList()) Convert(p);
        }

        public static void Convert(Pawn p)
        {
            if (p == null || p.Dead || !p.RaceProps.Humanlike) return;
            var comp = p.GetComp<CompAbilities>();
            if (comp == null) return;
            Resolve();
            foreach (var pair in pairs)
            {
                var from = pair.Item1; var to = pair.Item2;
                if (!comp.HasAbility(from)) continue;
                comp.LearnedAbilities.RemoveAll(a => a.def == from);
                var path = to.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>()?.path;
                Jujutsu.Learn(p, path, to);
            }
            foreach (var d in oldHediffs)
            {
                var h = p.health.hediffSet.GetFirstHediffOfDef(d);
                if (h != null) p.health.RemoveHediff(h);
            }
            // Sukuna's sustained toggles that were on stay on, as the shared ones
            if (SukunaPsycasts.SustainedToggles.IsOn(p, "infinity"))
            {
                SukunaPsycasts.SustainedToggles.Set(p, "infinity", SukunaPsycasts.SukunaDefOf.Sukuna_PseudoInfinity, "", false);
                Toggles.Set(p, "reinforcement", true, quiet: true);
            }
            if (SukunaPsycasts.SustainedToggles.IsOn(p, "rct"))
            {
                SukunaPsycasts.SustainedToggles.Set(p, "rct", SukunaPsycasts.SukunaDefOf.Sukuna_ReverseCursedTechnique, "", false);
                Toggles.Set(p, "rct", true, quiet: true);
            }
        }
    }
}
