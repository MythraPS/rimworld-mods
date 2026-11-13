using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Everything Yuji asks of the colony arrives as one of these. The letter stores
    /// only its kind and data, and rebuilds its buttons from them, so it survives a
    /// save and reload with the choice still open.
    /// </summary>
    public class ChoiceLetter_Yuji : ChoiceLetter
    {
        public string kind;
        public Faction other;
        public int silver;

        private Map MapNow => Find.AnyPlayerHomeMap;
        private QuestlineTracker T => QuestlineTracker.Instance;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) { yield return Option_Close; yield break; }
                switch (kind)
                {
                    case "intro":
                        yield return Option_Close;
                        break;

                    case "wounded":
                        // regard comes when they're actually rescued or tended, or goes if they die (DilemmaWatch)
                        yield return Pick("Take them in", () => DilemmaWatch.WatchWounded(YujiDilemmas.SpawnWounded(MapNow)));
                        yield return Pick("Turn them away", () =>
                            T.ChangeRegard(-8f, "you turned away a wounded stranger"));
                        yield return Option_Postpone;
                        break;

                    case "bounty":
                        yield return Pick($"Hand him over ({silver} silver)", () =>
                        {
                            YujiDilemmas.PaySilver(MapNow, silver);
                            T.ChangeRegard(-20f, "you sold out a man Yuji vouched for");
                        });
                        yield return Pick("Refuse", () =>
                        {
                            other?.TryAffectGoodwillWith(Faction.OfPlayer, -15, true, true, null, null);
                            T.ChangeRegard(12f, "you protected a hunted man");
                        });
                        yield return Option_Postpone;
                        break;

                    case "village":
                    {
                        var send = Pick($"Send the food ({YujiDilemmas.VillageNutrition:0} nutrition)", () =>
                        {
                            YujiDilemmas.TakeFood(MapNow, YujiDilemmas.VillageNutrition);
                            T.ChangeRegard(10f, "you fed a starving village");
                        });
                        if (MapNow == null || MapNow.resourceCounter.TotalHumanEdibleNutrition < YujiDilemmas.VillageNutrition)
                            send.Disable("not enough food in storage");
                        yield return send;
                        yield return Pick("Keep it", () =>
                            T.ChangeRegard(-4f, "you let a village starve"));
                        yield return Option_Postpone;
                        break;
                    }

                    case "sorcerer":
                        // Yuji comes and fights with you; regard when the raid is beaten (DilemmaWatch)
                        yield return Pick("Fight alongside him", () =>
                        {
                            var f = YujiDilemmas.SendSorcererRaid(MapNow, other);
                            DilemmaWatch.WatchRaid(MapNow, f);
                        });
                        yield return Pick("Stay out of it", () =>
                            T.ChangeRegard(-3f, "you stayed out of his fight"));
                        yield return Option_Postpone;
                        break;

                    case "join":
                        yield return Pick("Welcome him", () => YujiDilemmas.Join(MapNow));
                        yield return Pick("Not now", () =>
                            T.ChangeRegard(-10f, "you turned him away"));
                        yield return Option_Postpone;
                        break;

                    default:
                        yield return Option_Close;
                        break;
                }
            }
        }

        private DiaOption Pick(string label, System.Action act) => new DiaOption(label)
        {
            action = () => { act(); Find.LetterStack.RemoveLetter(this); },
            resolveTree = true,
        };

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref kind, "sukunaKind");
            Scribe_References.Look(ref other, "sukunaOther");
            Scribe_Values.Look(ref silver, "sukunaSilver", 0);
        }
    }

    public static class YujiDilemmas
    {
        public const float VillageNutrition = 20f;

        public static void Send(string kind, string label, string text, LookTargets look = null,
                                Faction other = null, int silver = 0)
        {
            var let = (ChoiceLetter_Yuji)LetterMaker.MakeLetter(SukunaQuestDefOf.Sukuna_YujiLetter);
            let.Label = label;
            let.Text = text;
            let.kind = kind;
            let.other = other;
            let.silver = silver;
            let.lookTargets = look;
            let.relatedFaction = other;
            Find.LetterStack.ReceiveLetter(let);
        }

        /// <summary>A villager from a friendly faction, beaten down by a curse, dropped
        /// at the edge of the map for the colony to rescue.</summary>
        public static Pawn SpawnWounded(Map map)
        {
            if (map == null) return null;
            var faction = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Villager, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, developmentalStages: DevelopmentalStage.Adult));
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null)) return null;
            GenSpawn.Spawn(p, cell, map);
            HealthUtility.DamageUntilDowned(p, true, null, null, null);
            Messages.Message($"{p.LabelShortCap} is lying wounded at the edge of the map. Rescue them before they bleed out.",
                p, MessageTypeDefOf.NeutralEvent, false);
            return p;
        }

        public static void PaySilver(Map map, int amount)
        {
            if (map == null || amount <= 0) return;
            var things = new List<Thing>();
            while (amount > 0)
            {
                var s = ThingMaker.MakeThing(ThingDefOf.Silver);
                s.stackCount = Mathf.Min(amount, ThingDefOf.Silver.stackLimit);
                amount -= s.stackCount;
                things.Add(s);
            }
            DropPodUtility.DropThingsNear(DropCellFinder.TradeDropSpot(map), map, things,
                110, false, false, true, false, true, null);
        }

        /// <summary>Removes the colony's stored human food, worst food first.</summary>
        public static void TakeFood(Map map, float nutrition)
        {
            if (map == null) return;
            var food = map.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(t => t.def.IsNutritionGivingIngestible && t.def.ingestible.HumanEdible
                            && !t.def.IsDrug && !(t is Corpse) && !t.IsForbidden(Faction.OfPlayer)
                            && t.IsInAnyStorage())
                .OrderBy(t => t.def.ingestible.preferability)
                .ToList();
            foreach (var t in food)
            {
                if (nutrition <= 0f) break;
                float per = t.GetStatValue(StatDefOf.Nutrition);
                if (per <= 0f) continue;
                int take = Mathf.Min(t.stackCount, Mathf.CeilToInt(nutrition / per));
                nutrition -= take * per;
                t.SplitOff(take).Destroy();
            }
        }

        public static Faction SendSorcererRaid(Map map, Faction faction)
        {
            if (map == null) return null;
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.points *= 0.7f;
            // the letter may have waited: a faction that has since made peace (or is gone) can't raid
            if (faction != null && (faction.defeated || !faction.HostileTo(Faction.OfPlayer))) faction = null;
            parms.faction = faction ?? Find.FactionManager.RandomEnemyFaction(false, false, false, TechLevel.Undefined);
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.forced = true;
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms) ? parms.faction : null;
        }

        public static void Join(Map map)
        {
            var t = QuestlineTracker.Instance;
            if (map == null || t == null || t.stage == YujiStage.Joined || t.stage == YujiStage.Gone) return;   // Gone: a fresh Yuji would be made
            var yuji = t.GetOrMakeYuji();
            if (yuji.Dead) return;
            // already on a map (he came to fight the curse user with you): he just stays
            if (yuji.MapHeld == null)
            {
                if (yuji.IsWorldPawn()) Find.WorldPawns.RemovePawn(yuji);
                if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null))
                    cell = CellFinder.RandomEdgeCell(map);
                GenSpawn.Spawn(yuji, cell, map);
            }
            yuji.SetFaction(Faction.OfPlayer);
            t.stage = YujiStage.Joined;
            // a Shinjuku Yuji comes with his brother (her call 2026-11-12)
            var ct = ChosoTracker.Instance;
            if (YujiStart.Setting == YujiStart.Shinjuku && ct != null && !ct.arrived) ct.Arrive();
            Find.LetterStack.ReceiveLetter("Yuji Itadori joins",
                "Yuji has made up his mind about you. He walks in with his hands in his pockets, " +
                "asks where the kitchen is, and says he'll help with the fingers — all of them, " +
                "including the ones he'll have to eat.\n\nHe is immune to the King of Curses: " +
                "fingers make him stronger without making him hungrier, and, he says, nothing can take his body from him.",
                LetterDefOf.PositiveEvent, yuji);
        }
    }
}
