using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Yuji: an introduction the first time, then a dilemma each visit, and
    /// an offer to join once he trusts the colony.</summary>
    public class IncidentWorker_YujiEncounter : IncidentWorker
    {
        private static readonly string[] Rotation = { "wounded", "village", "bounty", "sorcerer" };

        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var t = QuestlineTracker.Instance;
            return t != null && t.stage != YujiStage.Joined && t.stage != YujiStage.Gone;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var t = QuestlineTracker.Instance;
            var map = (Map)parms.target;
            var yuji = t.GetOrMakeYuji();

            if (t.stage == YujiStage.Unmet)
            {
                t.stage = YujiStage.Met;
                YujiDilemmas.Send("intro", "Yuji Itadori",
                    "A teenager with pink hair and a uniform two sizes too warm for the weather has been " +
                    "asking around about your colony. His name is Yuji Itadori. He is hunting the cursed " +
                    "fingers of Ryomen Sukuna so the King of Curses can be put down for good.\n\n" +
                    "He could use help. He is also watching how you treat people. He will come back with " +
                    "requests, and he hears about everything else: who you shelter, who you turn away, " +
                    "who you execute. If he comes to trust you, he will join you.\n\n" +
                    $"Right now he {Yuji.RegardWord(t.regard)}.");
                return true;
            }

            if (t.regard >= Yuji.JoinAt)
            {
                YujiDilemmas.Send("join", "Yuji wants to join",
                    "Yuji has seen enough. He trusts you, and he wants to fight beside you.\n\n" +
                    "He is unnaturally strong and fast, fights only with his fists, and as far as he knows the King of " +
                    "Curses cannot take his body: he is the one vessel who can eat the fingers safely.");
                return true;
            }

            for (int i = 0; i < Rotation.Length; i++)
            {
                string kind = Rotation[(t.dilemmaIndex + i) % Rotation.Length];
                if (TrySend(kind, map, t))
                {
                    t.dilemmaIndex = (t.dilemmaIndex + i + 1) % Rotation.Length;
                    return true;
                }
            }
            return false;
        }

        private static bool TrySend(string kind, Map map, QuestlineTracker t)
        {
            string standing = $"\n\nRight now Yuji {Yuji.RegardWord(t.regard)}.";
            switch (kind)
            {
                case "wounded":
                    YujiDilemmas.Send(kind, "Yuji: a wounded stranger",
                        "Yuji pulled someone out of a curse's nest a few miles from here. They're alive, " +
                        "barely, and he can't carry them and keep fighting. He's asking you to take them in " +
                        "until they can walk.\n\nIf you agree, they will be lying wounded at the edge of " +
                        "the map. Rescue them and tend their wounds: he'll hear whether you did." + standing);
                    return true;

                case "village":
                    YujiDilemmas.Send(kind, "Yuji: a starving village",
                        "Curses have been feeding on a farming village nearby, and nobody has dared " +
                        $"work the fields since. Yuji is asking you to send them food: {YujiDilemmas.VillageNutrition:0} " +
                        "nutrition's worth from your stores. He'll carry it himself." + standing);
                    return true;

                case "bounty":
                {
                    var f = Find.FactionManager.RandomNonHostileFaction(false, false, false, TechLevel.Undefined);
                    if (f == null) return false;
                    int silver = Mathf.RoundToInt(Rand.Range(400f, 700f) / 10f) * 10;
                    YujiDilemmas.Send(kind, "Yuji: a hunted man",
                        $"{f.Name} are offering {silver} silver for a man hiding near your colony. They say he " +
                        "stole from them. Yuji says he's a frightened kid who saw something he shouldn't " +
                        "have, and asks you not to hand him over.\n\nRefusing will cost you goodwill with " +
                        $"{f.Name}." + standing, null, f, silver);
                    return true;
                }

                case "sorcerer":
                {
                    if (!Find.Storyteller.difficulty.allowViolentQuests) return false;
                    var f = Find.FactionManager.RandomEnemyFaction(false, false, false, TechLevel.Undefined);
                    if (f == null || !f.def.humanlikeFaction) return false;
                    YujiDilemmas.Send(kind, "Yuji: a curse user",
                        $"A curse user riding with {f.Name} has been setting curses loose on travellers. Yuji " +
                        "has tracked them to the hills near your colony and is going in either way. He's " +
                        "asking whether you'll fight with him.\n\nIf you agree, they will attack your " +
                        "colony shortly, and Yuji will be there fighting beside you. Beat them and he won't forget it." + standing, null, f);
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// A finger surfaces. Picks one of the ways it can turn up, weighted, among the
    /// ones possible right now: sealed in ruins, held by a faction, on a caravan, in
    /// the belly of an enemy psycaster, in the King of Curses himself, or (rarely)
    /// for sale.
    /// </summary>
    public class IncidentWorker_FingerRumour : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => FingerCensus.Remaining() > 0;

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var options = new List<(string, float)> { ("ruin", 3f), ("held", 2f), ("convoy", 2f), ("trader", 0.5f) };
            if (Find.Storyteller.difficulty.allowViolentQuests)
            {
                options.Add(("vessel", 2f));
                if (!SukunaBehaviour.AnySukunaExists() && FingerCensus.Remaining() >= 3)
                    options.Add(("king", 1f));
            }

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
                case "ruin":   return StartQuest(SukunaQuestDefOf.Sukuna_Finger_Ruin, map);
                case "held":   return StartQuest(SukunaQuestDefOf.Sukuna_Finger_Held, map);
                case "convoy": return RelicConvoy.Announce(map, SukunaThingDefOf.Sukuna_Finger, RelicConvoy.Count(FingerCensus.Remaining()));
                case "vessel": return Fire(SukunaQuestDefOf.Sukuna_VesselRaid, map);
                case "king":   return Fire(SukunaQuestDefOf.Sukuna_KingRaid, map);
                case "trader": return FingerTrader.TryArrive(map);
            }
            return false;
        }

        private static bool StartQuest(QuestScriptDef def, Map map)
        {
            var slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            int n = QuestStash.Put(slate, SukunaThingDefOf.Sukuna_Finger, FingerCensus.Remaining());
            if (!def.CanRun(slate, map)) return false;
            var quest = QuestUtility.GenerateQuestAndMakeAvailable(def, slate);
            if (quest == null) return false;
            SukunaHunt.OnFingerQuest(quest, n);      // while Sukuna is loose: raced, maybe hunted (her call 2026-11-11)
            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }

        private static bool Fire(IncidentDef def, Map map)
        {
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            return def.Worker.TryExecute(parms);
        }
    }

    /// <summary>
    /// A raid with a vessel in it: one raider has swallowed fingers and fights as a
    /// Shrine psycaster. They do not turn into Sukuna — that is the other event.
    /// Killing them drops every finger they ate.
    /// </summary>
    public class IncidentWorker_VesselRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            FingerCensus.Remaining() > 0 && EnemyFaction() != null;

        private static Faction EnemyFaction() =>
            Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction
                            && f.HostileTo(Faction.OfPlayer) && f.def.pawnGroupMakers != null
                            && f != SukunaFaction.Existing())
                .RandomElementWithFallback();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var faction = parms.faction != null && parms.faction.HostileTo(Faction.OfPlayer) ? parms.faction : EnemyFaction();
            if (faction == null) return false;

            var before = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
            var raid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            raid.faction = faction;
            raid.points = Mathf.Max(raid.points * 0.8f, 200f);
            raid.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            raid.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            raid.forced = true;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(raid)) return false;

            var raiders = map.mapPawns.AllPawnsSpawned
                .Where(p => !before.Contains(p) && p.Faction == faction && p.RaceProps.Humanlike && !p.Downed)
                .ToList();
            if (raiders.Count == 0) return true;   // the raid happened; just no vessel in it

            var vessel = raiders.OrderByDescending(p => p.skills?.GetSkill(SkillDefOf.Melee)?.Level ?? 0).First();
            int fingers = Mathf.Min(FingerCensus.Remaining(), Rand.RangeInclusive(1, 3));
            if (fingers <= 0) return true;
            VesselMaker.Make(vessel, fingers);

            Find.LetterStack.ReceiveLetter("Vessel among the raiders",
                $"{vessel.LabelShortCap} has swallowed {fingers} of the cursed fingers. The King of Curses is " +
                "still asleep in there, but his technique is not: they fight with the Shrine techniques " +
                "they have learned, and they are stronger the more fingers they carry.\n\nKill them and the fingers fall out.",
                LetterDefOf.ThreatBig, vessel);
            return true;
        }
    }

    /// <summary>
    /// The King of Curses walks in. Somebody out there swallowed too many fingers and
    /// he took the body; now he is coming for the strongest psycaster on the map, and
    /// any loose fingers he passes. Kill him and every finger he holds drops.
    /// </summary>
    public class IncidentWorker_KingRaid : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            !SukunaBehaviour.AnySukunaExists() && FingerCensus.Remaining() >= 3;

        /// <summary>How many fingers the King comes with, from the raid points (colony wealth and
        /// colonists, the way a normal raid is sized; her call 2026-11-07). A young colony meets
        /// 3 or 4, a rich late one the whole King. Rounded at random, so it varies a little.</summary>
        private static readonly SimpleCurve FingersByPoints = new SimpleCurve
        {
            new CurvePoint(0f, 3f), new CurvePoint(500f, 4f), new CurvePoint(1500f, 7f),
            new CurvePoint(3000f, 11f), new CurvePoint(5000f, 15f), new CurvePoint(8000f, 20f),
        };

        public static int FingersFor(float points) =>
            Mathf.Clamp(GenMath.RoundRandom(FingersByPoints.Evaluate(points)), 3, 20);

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            // TryExecute doesn't ask CanFireNow (the dev action calls it directly): otherwise a vessel spawns who can't turn
            if (SukunaBehaviour.AnySukunaExists()) return false;
            int fingers = Mathf.Min(FingerCensus.Remaining(), FingersFor(parms.points > 0f ? parms.points : StorytellerUtility.DefaultThreatPointsNow(map)));
            if (fingers < 3) return false;

            var host = Find.FactionManager.RandomEnemyFaction(false, false, false, TechLevel.Undefined);
            var kind = host?.def?.basicMemberKind ?? PawnKindDefOf.Villager;
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, host, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                mustBeCapableOfViolence: true, developmentalStages: DevelopmentalStage.Adult));
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0.2f, false, null)) return false;
            GenSpawn.Spawn(p, cell, map);

            VesselMaker.Make(p, fingers);
            Transformation.Become(p, sendLetter: false);
            if (!SukunaBehaviour.IsSukuna(p)) return false;

            Find.LetterStack.ReceiveLetter("The King of Curses",
                $"Whoever this was, they swallowed {fingers} fingers, and that was enough. Ryomen Sukuna " +
                "is wearing their body and walking toward your colony. He belongs to no faction and " +
                "fights everything; he will go for your strongest psycaster first, and eat any finger " +
                "he finds on the way.\n\nKill him and every finger he holds falls out.",
                LetterDefOf.ThreatBig, p);
            return true;
        }
    }
}
