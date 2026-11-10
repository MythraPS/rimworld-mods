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
                        yield return Pick("Take them in", () =>
                        {
                            YujiDilemmas.SpawnWounded(MapNow);
                            T.ChangeRegard(8f, "you took in a wounded stranger");
                        });
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
                        yield return Pick("Fight alongside him", () =>
                        {
                            YujiDilemmas.SendSorcererRaid(MapNow, other);
                            T.ChangeRegard(10f, "you stood with him against a curse user");
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
        public static void SpawnWounded(Map map)
        {
            if (map == null) return;
            var faction = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Villager, faction, PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, developmentalStages: DevelopmentalStage.Adult));
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null)) return;
            GenSpawn.Spawn(p, cell, map);
            HealthUtility.DamageUntilDowned(p, true, null, null, null);
            Messages.Message($"{p.LabelShortCap} is lying wounded at the edge of the map. Rescue them before they bleed out.",
                p, MessageTypeDefOf.NeutralEvent, false);
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

        public static void SendSorcererRaid(Map map, Faction faction)
        {
            if (map == null) return;
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.points *= 0.7f;
            parms.faction = faction ?? Find.FactionManager.RandomEnemyFaction(false, false, false, TechLevel.Undefined);
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.forced = true;
            IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        public static void Join(Map map)
        {
            var t = QuestlineTracker.Instance;
            if (map == null || t == null || t.stage == YujiStage.Joined) return;
            var yuji = t.GetOrMakeYuji();
            if (yuji.Dead || yuji.Spawned) return;
            if (yuji.IsWorldPawn()) Find.WorldPawns.RemovePawn(yuji);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null))
                cell = CellFinder.RandomEdgeCell(map);
            GenSpawn.Spawn(yuji, cell, map);
            yuji.SetFaction(Faction.OfPlayer);
            t.stage = YujiStage.Joined;
            Find.LetterStack.ReceiveLetter("Yuji Itadori joins",
                "Yuji has made up his mind about you. He walks in with his hands in his pockets, " +
                "asks where the kitchen is, and says he'll help with the fingers — all of them, " +
                "including the ones he'll have to eat.\n\nHe is immune to the King of Curses: " +
                "fingers make him stronger without making him hungrier, and nothing can take his body from him.",
                LetterDefOf.PositiveEvent, yuji);
        }
    }
}
