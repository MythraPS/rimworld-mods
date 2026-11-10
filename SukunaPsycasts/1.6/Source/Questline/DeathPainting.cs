using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    /// <summary>Ten Death Paintings, one per Hemosage technique (her call 2026-11-01: at least as many as
    /// Hemosage needs). Counted live like the fingers; eaten ones are gone for good.</summary>
    public static class PaintingCensus
    {
        public const int Total = 10;

        public static int Remaining()
        {
            int eaten = SukunaArc.Instance?.paintingsEaten ?? 0;
            return Mathf.Max(0, Total - eaten - FingerCensus.CountLoose(SukunaArcDefOf.Sukuna_DeathPainting));
        }
    }

    public class CompProperties_DeathPainting : CompProperties
    {
        public CompProperties_DeathPainting() => compClass = typeof(CompDeathPainting);
    }

    public class CompDeathPainting : ThingComp
    {
        public override void PostIngested(Pawn ingester)
        {
            base.PostIngested(ingester);
            Eat(ingester);
        }

        /// <summary>Yuji: hemogenic blood, the Hemosage path, and its next technique, lowest level
        /// first. Anyone else: poison.</summary>
        public static void Eat(Pawn p)
        {
            if (p == null || p.Dead) return;
            var arc = SukunaArc.Instance;
            if (arc != null) arc.paintingsEaten++;

            if (!Yuji.Is(p))
            {
                HealthUtility.AdjustSeverity(p, HediffDefOf.ToxicBuildup, 0.45f);
                var fp = DefDatabase<HediffDef>.GetNamedSilentFail("FoodPoisoning");
                if (fp != null) p.health.AddHediff(fp);
                Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. The blood inside is poison to anyone but Yuji and his brothers.",
                    p, MessageTypeDefOf.NegativeHealthEvent, false);
                return;
            }

            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (ModsConfig.BiotechActive && hemogenic != null && p.genes != null && !p.genes.HasActiveGene(hemogenic))
                p.genes.AddGene(hemogenic, false);

            var path = SukunaArc.Hemosage;
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            var psy = PsycastSetup.Ensure(p);
            var comp = p.GetComp<CompAbilities>();
            if (path == null || psy == null || comp == null)
            {
                Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. The blood in it is his now.", p, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (!psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);

            var next = path.abilities?
                .Where(a => !comp.HasAbility(a))
                .Select(a => new { def = a, ext = a.Psycast() })
                .Where(x => x.ext != null && x.ext.PrereqsCompleted(comp))
                .OrderBy(x => x.ext.level).ThenBy(x => x.ext.order)
                .FirstOrDefault()?.def;
            if (next != null)
            {
                comp.GiveAbility(next);
                Messages.Message($"{p.LabelShortCap} swallowed a Death Painting and learned {next.label}.", p, MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                psy.ImproveStats(1);
                Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. Nothing left of Hemosage to learn, so his psycaster stats rise.", p, MessageTypeDefOf.PositiveEvent, false);
            }
        }
    }

    /// <summary>A painting surfaces, the same ways a finger does: in ruins, held by a faction, on a
    /// caravan, or (rarely) for sale.</summary>
    public class IncidentWorker_PaintingRumour : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => PaintingCensus.Remaining() > 0;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var options = new List<(string, float)> { ("ruin", 3f), ("held", 2f), ("convoy", 2f), ("trader", 0.5f) };
            while (options.Count > 0)
            {
                var pick = options.RandomElementByWeight(o => o.Item2);
                options.Remove(pick);
                if (TryKind(pick.Item1, map)) return true;
            }
            return false;
        }

        private static bool TryKind(string kind, Map map)
        {
            switch (kind)
            {
                case "ruin":   return StartQuest(SukunaArcDefOf.Sukuna_Painting_Ruin, map);
                case "held":   return StartQuest(SukunaArcDefOf.Sukuna_Painting_Held, map);
                case "convoy": return StartQuest(SukunaArcDefOf.Sukuna_Painting_Convoy, map);
                case "trader": return FingerTrader.TryArrive(map, SukunaArcDefOf.Sukuna_DeathPainting,
                                   "is carrying a jar of black glass with something curled up inside it: a Death Painting, for sale.");
            }
            return false;
        }

        private static bool StartQuest(QuestScriptDef def, Map map)
        {
            var slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            QuestStash.Put(slate, SukunaArcDefOf.Sukuna_DeathPainting, PaintingCensus.Remaining());
            if (!def.CanRun(slate, map)) return false;
            var quest = QuestUtility.GenerateQuestAndMakeAvailable(def, slate);
            if (quest == null) return false;
            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }
    }
}
