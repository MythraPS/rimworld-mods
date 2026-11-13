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
    /// <summary>Six Death Painting wombs (her call 2026-11-10; was ten), each teaching the next Hemosage
    /// technique. Counted live like the fingers; eaten ones are gone for good.</summary>
    public static class PaintingCensus
    {
        public const int Total = 6;

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
        /// <summary>No messages (a Yuji made at a later stage, 2026-11-12).</summary>
        public static bool Silent;
        public const int PointsPerPainting = 2;

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

            // Choso is immune too: the paintings are his brothers (her call 2026-11-11). No poison, and nothing to learn.
            if (ChosoTracker.Is(p))
            {
                if (!Silent) Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. It is his brothers' blood, and it does him no harm.",
                    p, MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            if (!Yuji.Is(p))
            {
                HealthUtility.AdjustSeverity(p, HediffDefOf.ToxicBuildup, 0.45f);
                var fp = DefDatabase<HediffDef>.GetNamedSilentFail("FoodPoisoning");
                if (fp != null) p.health.AddHediff(fp);
                if (!Silent) Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. The blood inside is poison to anyone but Yuji and Choso.",
                    p, MessageTypeDefOf.NegativeHealthEvent, false);
                return;
            }

            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (ModsConfig.BiotechActive && hemogenic != null && p.genes != null && !p.genes.HasActiveGene(hemogenic))
                p.genes.AddGene(hemogenic, false);

            // Before his first finger he has no cursed energy and can't hold a technique (Heavenly Restriction, her
            // call 2026-11-10): the painting's technique waits and comes with the first finger.
            if (JujutsuFundamentals.ZeroCursedEnergy.Is(p))
            {
                if (arc != null) arc.savedPaintings++;
                if (!Silent) Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. The blood in it is his now; with no cursed energy yet, what it teaches will come with his first finger.",
                    p, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            Learn(p);
        }

        /// <summary>The paintings eaten while he had no cursed energy, given now (his first finger).</summary>
        public static void GrantSaved(Pawn p)
        {
            var arc = SukunaArc.Instance;
            if (arc == null) return;
            int n = arc.savedPaintings;
            arc.savedPaintings = 0;
            for (int i = 0; i < n; i++) Learn(p);
        }

        private static void Learn(Pawn p)
        {
            var path = SukunaArc.Hemosage;
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            var psy = PsycastSetup.Ensure(p);
            var comp = p.GetComp<CompAbilities>();
            if (path == null || psy == null || comp == null)
            {
                if (!Silent) Messages.Message($"{p.LabelShortCap} swallowed a Death Painting. The blood in it is his now.", p, MessageTypeDefOf.PositiveEvent, false);
                return;
            }
            if (!psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            // two psycast points to spend as he likes (her call 2026-11-12; was the next Hemosage technique, free)
            psy.points += PointsPerPainting;
            if (!Silent) Messages.Message($"{p.LabelShortCap} swallowed a Death Painting: {PointsPerPainting} psycast points to spend, and Hemosage is open to him.", p, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    /// <summary>A painting surfaces, the same ways a finger does: in ruins, held by a faction, on a
    /// caravan, or (rarely) for sale.</summary>
    public class IncidentWorker_PaintingRumour : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => PaintingCensus.Remaining() > 0 && (ChosoTracker.Instance?.arrived ?? true);

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
                case "convoy": return RelicConvoy.Announce(map, SukunaArcDefOf.Sukuna_DeathPainting, RelicConvoy.Count(PaintingCensus.Remaining()));
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

namespace SukunaPsycasts
{
    /// <summary>Choso never eats a Death Painting (her call 2026-11-11: "It just won't give him the option"): he
    /// won't pick one as food, and the right-click "eat" option doesn't show for him. The immunity in DeathPainting.Eat
    /// stays as a fallback.</summary>
    public static class ChosoNoPaintings
    {
        public static bool Blocked(Pawn p, ThingDef def) =>
            def != null && def == SukunaArcDefOf.Sukuna_DeathPainting && ChosoTracker.Is(p);
    }

    [HarmonyLib.HarmonyPatch]
    public static class Patch_ChosoWillEat
    {
        public static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods() =>
            HarmonyLib.AccessTools.GetDeclaredMethods(typeof(RimWorld.FoodUtility)).Where(m => m.Name == "WillEat");

        public static void Postfix(Pawn p, object food, ref bool __result)
        {
            if (!__result) return;
            var def = food as ThingDef ?? (food as Thing)?.def;
            if (ChosoNoPaintings.Blocked(p, def)) __result = false;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(RimWorld.FloatMenuOptionProvider_Ingest), "GetSingleOptionFor")]
    public static class Patch_ChosoNoEatOption
    {
        public static void Postfix(Thing clickedThing, RimWorld.FloatMenuContext context, ref Verse.FloatMenuOption __result)
        {
            if (__result != null && ChosoNoPaintings.Blocked(context?.FirstSelectedPawn, clickedThing?.def)) __result = null;
        }
    }
}
