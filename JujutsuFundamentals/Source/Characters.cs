using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKCharacterDefOf
    {
        public static IncidentDef JJK_KusakabeAsks;
        public static QuestScriptDef JJK_MiwaRescue;
        public static HediffDef JJK_Vibraslap;
        public static HediffDef JJK_ReturnFire;
        public static VEFAbilityDef JJK_BoogieWoogie;
        public static VEFAbilityDef JJK_BoogieWoogieFlurry;
        public static VEFAbilityDef JJK_BoogieWoogieDeflect;
        static JJKCharacterDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKCharacterDefOf));
    }

    /// <summary>
    /// Kusakabe, Miwa and Todo: each made once per game, and the state of Miwa's rescue (her calls 2026-11-10):
    /// Kusakabe asks you to save Miwa, who has gone alone to raid a random enemy outpost hunting Kenjaku.
    /// Reach the outpost within two days and you catch her before she goes in; after that she is inside,
    /// fighting, and you finish the raid with her or for her. Success: both join. Failure (she dies, or nobody
    /// comes within the time limit): Miwa dies and Kusakabe never joins. Todo comes looking for Yuji once
    /// Yuji is in the colony.
    /// </summary>
    public class CharacterTracker : GameComponent
    {
        public static CharacterTracker Instance;

        public const int InterceptTicks = 2 * GenDate.TicksPerDay;
        public const int DeadlineTicks = 10 * GenDate.TicksPerDay;
        // Miwa is "significantly weaker" with the same techniques (her call): she is psychically dull (Looks.Miwa),
        // which halves her psychic sensitivity and so everything she does. It was a hidden x0.5 before 2026-11-10.

        public Pawn kusakabe, miwa, todo, ryu;
        public bool askedForHelp, miwaDone, todoArrived, ryuArrived;
        public Quest miwaQuest;
        public Site miwaSite;
        public int questStartTick = -1;
        public bool miwaOnSite, intercepted;
        /// <summary>The two-days-left warning has been sent (her call 2026-11-11: tell the player about the deadline).</summary>
        public bool warned;
        /// <summary>Miwa was saved but there was no home map for Kusakabe to walk into: he comes once there is one.</summary>
        public bool kusakabeDue;
        public const int WarnTicks = DeadlineTicks - 2 * GenDate.TicksPerDay;

        public CharacterTracker(Game game)
        {
            Instance = this;
        }

        static CharacterTracker()
        {
            Jujutsu.ReinforcementFactors.Add(p => Ryu.Is(p) ? Ryu.ReinforcementFactor : 1f);
        }

        public static bool IsKusakabe(Pawn p) => p != null && Instance?.kusakabe == p;
        public static bool IsMiwa(Pawn p) => p != null && Instance?.miwa == p;
        public static bool IsTodo(Pawn p) => p != null && Instance?.todo == p;

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 == 0 && miwaQuest != null && !miwaDone) MiwaTick(now);
            if (kusakabeDue && now % 2500 == 50) KusakabeJoins();
            if (now % 2500 == 100 && !todoArrived && Rand.MTBEventOccurs(3f, GenDate.TicksPerDay, 2500f) && YujiInColony(out var map))
                TodoArrives(map);
        }

        // ---------------------------------------------------------------- Miwa's rescue

        public bool StartMiwaQuest(Map map)
        {
            if (miwaQuest != null || miwaDone || map == null) return false;
            var slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            slate.Set("map", map);
            if (!JJKCharacterDefOf.JJK_MiwaRescue.CanRun(slate, map)) return false;
            var quest = QuestUtility.GenerateQuestAndMakeAvailable(JJKCharacterDefOf.JJK_MiwaRescue, slate);
            if (quest == null) return false;
            miwaQuest = quest;
            // the quest is built from its own copy of the slate, so the site has to be read from the quest itself
            // (found 2026-11-12 by the automated test: reading it from this slate gave null, and the quest failed at once)
            miwaSite = quest.PartsListForReading.OfType<QuestPart_SpawnWorldObject>().Select(p => p.worldObject).OfType<Site>().FirstOrDefault();
            questStartTick = Find.TickManager.TicksGame;
            askedForHelp = true;
            if (kusakabe == null) kusakabe = Characters.MakeKusakabe();
            if (miwa == null) miwa = Characters.MakeMiwa();
            QuestUtility.SendLetterQuestAvailable(quest);
            return true;
        }

        private void MiwaTick(int now)
        {
            if (miwa != null && miwa.Dead) { MiwaFails("Kasumi Miwa has died. Kusakabe will not come."); return; }
            // Reached: she's yours. The deadline and a lost outpost no longer kill her (her call 2026-11-11: a caravan
            // that leaves with her, or leaves before the outpost is cleared, must not cost her life). Leaving the
            // outpost alive with her, or clearing it, is a success.
            if (miwaOnSite)
            {
                bool away = miwaSite == null || miwaSite.Destroyed || !miwaSite.HasMap || miwa.MapHeld != miwaSite.Map;
                if (away || !GenHostility.AnyHostileActiveThreatToPlayer(miwaSite.Map, false)) MiwaSucceeds(false);
                return;
            }
            if (miwaSite == null || miwaSite.Destroyed)
            {
                MiwaFails("The outpost is gone, and Miwa with it. Kusakabe will not come.");
                return;
            }
            if (!miwaSite.HasMap)
            {
                if (now - questStartTick > DeadlineTicks)
                    MiwaFails("Nobody came for Kasumi Miwa. She died raiding the outpost alone, and Kusakabe will not come.");
                else if (!warned && now - questStartTick > WarnTicks)
                {
                    warned = true;
                    Find.LetterStack.ReceiveLetter("Two days left for Miwa",
                        "Kasumi Miwa can't hold out alone much longer. If nobody reaches the outpost within two days, she dies there and Kusakabe will not come.",
                        LetterDefOf.NegativeEvent, miwaSite);
                }
                return;
            }
            var map = miwaSite.Map;
            if (!miwaOnSite)
            {
                miwaOnSite = true;
                intercepted = now - questStartTick <= InterceptTicks;
                Characters.PlaceMiwa(miwa, map, intercepted);
                if (intercepted) { MiwaSucceeds(true); return; }
                Find.LetterStack.ReceiveLetter("Miwa is inside", "Kasumi Miwa is already inside the outpost, fighting. Keep her alive and finish the raid with her.",
                                               LetterDefOf.NeutralEvent, miwa);
                return;
            }
        }

        private void MiwaSucceeds(bool caught)
        {
            miwaDone = true;
            if (miwaQuest?.State == QuestState.Ongoing) miwaQuest.End(QuestEndOutcome.Success, false);
            DropEmptySite();
            kusakabeDue = true;
            KusakabeJoins();
            Find.LetterStack.ReceiveLetter("Miwa is safe",
                (caught ? "You caught Kasumi Miwa before she reached the outpost." : "The outpost has fallen, and Kasumi Miwa is alive.")
                + " She joins you, and Atsuya Kusakabe arrives to keep his word and join too.", LetterDefOf.PositiveEvent, miwa);
        }

        /// <summary>He walks in at home; with no home map right now (everyone travelling), he waits until there is one.</summary>
        private void KusakabeJoins()
        {
            if (kusakabe == null || kusakabe.Dead || kusakabe.Faction == Faction.OfPlayer) { kusakabeDue = false; return; }
            var home = Find.AnyPlayerHomeMap;
            if (home == null || kusakabe.Spawned) return;
            Characters.WalkInAndJoin(kusakabe, home);
            kusakabeDue = false;
        }

        /// <summary>The outpost world object goes with the quest unless someone is on its map right now (then the game
        /// removes it as usual when they leave).</summary>
        private void DropEmptySite()
        {
            if (miwaSite != null && !miwaSite.Destroyed && !miwaSite.HasMap) miwaSite.Destroy();
        }

        private void MiwaFails(string text)
        {
            miwaDone = true;
            if (miwa != null && !miwa.Dead)
            {
                miwa.Kill(null, null);
            }
            if (miwaQuest?.State == QuestState.Ongoing) miwaQuest.End(QuestEndOutcome.Fail, false);
            DropEmptySite();
            Find.LetterStack.ReceiveLetter("Miwa is dead", text, LetterDefOf.NegativeEvent);
        }

        // ---------------------------------------------------------------- Todo

        /// <summary>Is Yuji (from the Sukuna &amp; Yuji mod) a colonist? Read by name, so this mod doesn't need that one.</summary>
        public static bool YujiInColony(out Map map)
        {
            map = null;
            var isYuji = YujiCheck;
            if (isYuji == null) return false;
            foreach (var m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                if (m.mapPawns.FreeColonistsSpawned.Any(p => isYuji(p))) { map = m; return true; }
            }
            return false;
        }

        private static Func<Pawn, bool> yujiCheck; private static bool lookedYuji;
        private static Func<Pawn, bool> YujiCheck
        {
            get
            {
                if (lookedYuji) return yujiCheck;
                lookedYuji = true;
                var m = AccessTools.Method(AccessTools.TypeByName("SukunaPsycasts.Yuji"), "Is", new[] { typeof(Pawn) });
                if (m != null) yujiCheck = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), m);
                return yujiCheck;
            }
        }

        public void TodoArrives(Map map)
        {
            if (todoArrived || map == null) return;
            if (todo == null) todo = Characters.MakeTodo();
            todoArrived = true;
            Characters.WalkInAndJoin(todo, map);
            Find.LetterStack.ReceiveLetter("Aoi Todo arrives",
                "Aoi Todo has come looking for his best friend, Yuji Itadori, and he isn't leaving. He joins your colony.",
                LetterDefOf.PositiveEvent, todo);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref kusakabe, "kusakabe", true);
            Scribe_References.Look(ref miwa, "miwa", true);
            Scribe_References.Look(ref todo, "todo", true);
            Scribe_References.Look(ref ryu, "ryu", true);
            Scribe_Values.Look(ref ryuArrived, "ryuArrived");
            Scribe_References.Look(ref miwaQuest, "miwaQuest");
            Scribe_References.Look(ref miwaSite, "miwaSite");
            Scribe_Values.Look(ref askedForHelp, "askedForHelp");
            Scribe_Values.Look(ref miwaDone, "miwaDone");
            Scribe_Values.Look(ref todoArrived, "todoArrived");
            Scribe_Values.Look(ref questStartTick, "questStartTick", -1);
            Scribe_Values.Look(ref miwaOnSite, "miwaOnSite");
            Scribe_Values.Look(ref intercepted, "intercepted");
            Scribe_Values.Look(ref warned, "miwaWarned");
            Scribe_Values.Look(ref kusakabeDue, "kusakabeDue");
        }
    }

    public class IncidentWorker_KusakabeAsks : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            CharacterTracker.Instance != null && !CharacterTracker.Instance.askedForHelp && parms.target is Map;

        protected override bool TryExecuteWorker(IncidentParms parms) => CharacterTracker.Instance.StartMiwaQuest((Map)parms.target);
    }

    /// <summary>RimWorld of Magic gives new pawns a Might or Magic class in a postfix on PawnGenerator.GenerateTraits
    /// (TorannMagicMod+PawnGenerator_Patch). The JJK characters are made without one (her call 2026-11-10); they
    /// can still gain one later in play.</summary>
    public static class NoMagicClass
    {
        public static bool making;

        public static void Patch(Harmony h)
        {
            var t = AccessTools.TypeByName("TorannMagic.TorannMagicMod+PawnGenerator_Patch");
            var m = t == null ? null : AccessTools.Method(t, "Postfix");
            if (m == null) return;
            h.Patch(m, prefix: new HarmonyMethod(typeof(NoMagicClass), nameof(Skip)));
            Log.Message("[JJK Core] RimWorld of Magic found: the JJK characters are made without a Might or Magic class.");
        }

        public static bool Skip() => !making;
    }

    /// <summary>Making the three characters. Looks, traits and skills beyond melee are still hers to pick.</summary>
    public static class Characters
    {
        internal static Pawn Make(string first, string nick, string last, Gender gender, float? age)
        {
            var req = new PawnGenerationRequest(PawnKindDefOf.Colonist, faction: null, context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: true, allowAddictions: false,
                fixedBiologicalAge: age, fixedChronologicalAge: age, fixedGender: gender,
                forcedXenotype: XenotypeDefOf.Baseliner, developmentalStages: DevelopmentalStage.Adult);
            Pawn p;
            NoMagicClass.making = true;
            try { p = PawnGenerator.GeneratePawn(req); }
            finally { NoMagicClass.making = false; }
            p.Name = new NameTriple(first, nick, last);
            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            PointSettle.Queue(p);   // no points left to spend once made (her call 2026-11-11)
            return p;
        }

        internal static void Skill(Pawn p, SkillDef s, int level, Passion passion)
        {
            var r = p.skills?.GetSkill(s);
            if (r == null) return;
            r.Level = Mathf.Max(r.Level, level);
            r.passion = passion;
        }

        public static Hediff_PsycastAbilities Psycaster(Pawn p, int level)
        {
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            var psy = p.health.hediffSet.hediffs.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            if (psy == null)
            {
                var def = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
                if (def == null) return null;
                psy = p.health.AddHediff(def) as Hediff_PsycastAbilities;
                var link = p.health.hediffSet.hediffs.Find(h => h is Hediff_Psylink) as Hediff_Psylink;
                if (psy != null && link != null)
                {
                    try { psy.InitializeFromPsylink(link); }
                    catch (Exception e) { Log.Warning("[JJK Core] InitializeFromPsylink failed: " + e.Message); }
                }
            }
            if (psy != null && psy.level < level) psy.ChangeLevel(level - psy.level);
            return psy;
        }

        public static void Learn(Pawn p, PsycasterPathDef path, params VEFAbilityDef[] only)
        {
            var psy = Psycaster(p, 1);
            var comp = p.GetComp<CompAbilities>();
            if (psy == null || comp == null || path == null) return;
            if (!psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            foreach (var a in only.Length > 0 ? only.ToList() : path.abilities)
                if (a != null && !comp.HasAbility(a)) comp.GiveAbility(a);
        }

        private static void Sword(Pawn p)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");
            if (def == null || p.equipment == null) return;
            p.equipment.DestroyAllEquipment();
            p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def, ThingDefOf.Steel));
        }

        /// <summary>No innate technique (canon): Cursed Energy Reinforcement and the whole New Shadow Style.</summary>
        public static Pawn MakeKusakabe()
        {
            var p = Make("Atsuya", "Kusakabe", "Kusakabe", Gender.Male, 37f);   // no official age; ~37 (Anibase), same biological and chronological (her call 2026-11-10)
            Skill(p, SkillDefOf.Melee, 16, Passion.Major);
            Psycaster(p, 10);
            Learn(p, JJKDefOf.JJK_CursedEnergyFundamentals, JJKAbilityDefOf.JJK_Reinforcement,
                  JJKAbilityDefOf.JJK_AirStep, JJKAbilityDefOf.JJK_Blast);   // her call 2026-11-11
            Learn(p, JJKDefOf.JJK_NewShadowStyle);
            Looks.Kusakabe(p);
            Sword(p);
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): the good guys share one
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", RimWorld.FactionDefOf.PlayerColony, "Collectivist"));
            Looks.Refresh(p);
            return p;
        }

        /// <summary>The same techniques as Kusakabe; psychically dull, so at half the power.</summary>
        public static Pawn MakeMiwa()
        {
            var p = Make("Kasumi", "Miwa", "Miwa", Gender.Female, 17f);
            Skill(p, SkillDefOf.Melee, 10, Passion.Major);
            Psycaster(p, 5);
            Learn(p, JJKDefOf.JJK_CursedEnergyFundamentals, JJKAbilityDefOf.JJK_Reinforcement);
            Learn(p, JJKDefOf.JJK_NewShadowStyle);
            Looks.Miwa(p);
            Sword(p);
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): the good guys share one
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", RimWorld.FactionDefOf.PlayerColony, "Collectivist"));
            Looks.Refresh(p);
            return p;
        }

        /// <summary>Boogie Woogie (not a path), plus Cursed Energy Reinforcement and Black Flash. Both hands at the start.</summary>
        public static Pawn MakeTodo()
        {
            var p = Make("Aoi", "Todo", "Todo", Gender.Male, 18f);
            Skill(p, SkillDefOf.Melee, 16, Passion.Major);
            Psycaster(p, 8);
            Learn(p, JJKDefOf.JJK_CursedEnergyFundamentals, JJKAbilityDefOf.JJK_Reinforcement, JJKAbilityDefOf.JJK_BlackFlash,
                  JJKAbilityDefOf.JJK_AirStep);   // Air Step: her call 2026-11-11
            BoogieWoogie.Sync(p);
            Innate.OpenPath(p, Innate.BoogiePath);   // his Boogie Woogie path (2026-11-12)
            p.equipment?.DestroyAllEquipment();
            Looks.Todo(p);
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): the good guys share one
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", RimWorld.FactionDefOf.PlayerColony, "Collectivist"));
            Looks.Refresh(p);
            return p;
        }

        public static void WalkInAndJoin(Pawn p, Map map)
        {
            if (p == null || map == null || p.Dead) return;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Friendly)) cell = CellFinder.RandomEdgeCell(map);
            if (!p.Spawned) GenSpawn.Spawn(p, cell, map);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
        }

        /// <summary>Caught on the way: she appears beside your people. Too late: she is inside, near the middle of the outpost.
        /// Either way she fights on your side from here on.</summary>
        public static void PlaceMiwa(Pawn miwa, Map map, bool caught)
        {
            if (miwa == null || miwa.Dead) return;
            if (Find.WorldPawns.Contains(miwa)) Find.WorldPawns.RemovePawn(miwa);
            IntVec3 cell;
            var ours = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
            if (caught && ours != null) cell = CellFinder.RandomClosewalkCellNear(ours.Position, map, 4);
            else
            {
                var enemy = map.mapPawns.AllPawnsSpawned.FirstOrDefault(x => x.HostileTo(Faction.OfPlayer) && !x.Downed);
                cell = enemy != null ? CellFinder.RandomClosewalkCellNear(enemy.Position, map, 6) : map.Center;
            }
            // the finders fall back to the very cell they were given: never into rock or a wall
            if (!cell.InBounds(map) || !cell.Standable(map))
                cell = CellFinder.TryFindRandomCellNear(cell.ClampInsideMap(map), map, 12, c => c.Standable(map), out var near) ? near : CellFinder.StandableCellNear(map.Center, map, 40f);
            if (!miwa.Spawned) GenSpawn.Spawn(miwa, cell, map);
            if (miwa.Faction != Faction.OfPlayer) miwa.SetFaction(Faction.OfPlayer);
            if (!caught) miwa.drafter.Drafted = true;
        }
    }

    // ==================================================================== Boogie Woogie

    /// <summary>Todo's technique (her calls 2026-11-10): swap places with a pawn or an item. The clap needs both
    /// hands free; the Vibraslap, crafted and installed in place of a hand, needs only one and adds the flurry.</summary>
    public static class BoogieWoogie
    {
        public static bool HasVibraslap(Pawn p) => Jujutsu.Has(p, JJKCharacterDefOf.JJK_Vibraslap);

        private static int Hands(Pawn p) =>
            p.health.hediffSet.GetNotMissingParts().Count(x => x.def == BodyPartDefOf.Hand && !p.health.hediffSet.HasDirectlyAddedPartFor(x));

        /// <summary>Why he can't clap now, or null. "Hands free" read as: no weapon held (open point for her).</summary>
        public static string CantClap(Pawn p)
        {
            if (p.equipment?.Primary != null) return "Needs his hands free: no weapon held.";
            if (HasVibraslap(p)) return Hands(p) >= 1 ? null : "Needs a hand to strike the vibraslap.";
            return Hands(p) >= 2 ? null : "Needs both hands to clap (a vibraslap would do for the missing one).";
        }

        /// <summary>The abilities follow what he has: the flurry only with the Vibraslap.</summary>
        public static void Sync(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) return;
            foreach (var a in new[] { JJKCharacterDefOf.JJK_BoogieWoogie, JJKCharacterDefOf.JJK_BoogieWoogieDeflect })
                if (a != null && !comp.HasAbility(a)) comp.GiveAbility(a);
            if (HasVibraslap(p) && JJKCharacterDefOf.JJK_BoogieWoogieFlurry != null && !comp.HasAbility(JJKCharacterDefOf.JJK_BoogieWoogieFlurry))
                comp.GiveAbility(JJKCharacterDefOf.JJK_BoogieWoogieFlurry);
        }

        public static void Swap(Thing a, Thing b)
        {
            if (a == null || b == null || !a.Spawned || !b.Spawned || a.Map != b.Map) return;
            var map = a.Map;
            IntVec3 pa = a.Position, pb = b.Position;
            Place(a, pb); Place(b, pa);   // instant, in place, nothing drawn (her call 2026-11-10)
        }

        private static void Place(Thing t, IntVec3 c)
        {
            if (t is Pawn p)
            {
                p.pather?.StopDead();
                p.Position = c;
                p.Notify_Teleported(true, true);   // reset the drawn position too, or they slide across
                p.jobs?.EndCurrentJob(JobCondition.InterruptForced);
            }
            else t.Position = c;
        }

        public static void Clap(Pawn p)
        {
            if (p.Spawned) Snd.At(HasVibraslap(p) ? "JJK_Snd_Vibraslap" : "JJK_Snd_Clap", p.Position, p.Map);   // heard, not seen
        }
    }

    /// <summary>Targets a cell, so a stone chunk or any other item is as easy to pick as a pawn (her call 2026-11-10):
    /// the pawn standing there first, otherwise the item lying there.</summary>
    public class Ability_BoogieWoogie : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            reason = BoogieWoogie.CantClap(pawn);
            if (reason != null) return false;
            return base.IsEnabledForPawn(out reason);
        }

        /// <summary>Who or what stands in a cell: a pawn first (Todo himself counts: either target may be him), else an item.</summary>
        public static Thing SwapTargetAt(IntVec3 c, Map map, Pawn self)
        {
            if (map == null || !c.InBounds(map)) return null;
            Thing item = null;
            foreach (var t in c.GetThingList(map))
            {
                if (t is Pawn) return t;
                if (item == null && t.def.category == ThingCategory.Item) item = t;
            }
            return item;
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            var t = target.HasThing ? target.Thing : SwapTargetAt(target.Cell, pawn.Map, pawn);
            if (t == null)
            {
                if (showMessages) Messages.Message("Nothing there to swap: needs a person, an animal or an item.", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            if (t != pawn && !GenSight.LineOfSight(pawn.Position, t.Position, pawn.Map, true))
            {
                if (showMessages) Messages.Message("Todo can't see it.", pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length < 2) return;
            // any two targets swap (her call 2026-11-11), Todo himself being one of them or not
            var a = targets[0].HasThing ? targets[0].Thing : SwapTargetAt(targets[0].Cell, pawn.Map, pawn);
            var b = targets[1].HasThing ? targets[1].Thing : SwapTargetAt(targets[1].Cell, pawn.Map, pawn);
            if (a == null || b == null || a == b) return;
            BoogieWoogie.Clap(pawn);
            BoogieWoogie.Swap(a, b);
        }
    }

    /// <summary>Re-Boogie Woogie: dozens of swaps at once among everyone around him; enemies come out dazed.</summary>
    public class Ability_BoogieWoogieFlurry : Ability
    {
        public const int Swaps = 24;
        public const int DazeTicks = 180;

        public override bool IsEnabledForPawn(out string reason)
        {
            if (!BoogieWoogie.HasVibraslap(pawn)) { reason = "Needs the vibraslap."; return false; }
            reason = BoogieWoogie.CantClap(pawn);
            if (reason != null) return false;
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            BoogieWoogie.Clap(pawn);
            var map = pawn.Map;
            var pool = GenRadial.RadialDistinctThingsAround(pawn.Position, map, def.radius, true).OfType<Pawn>().Where(x => !x.Dead).ToList();
            if (!pool.Contains(pawn)) pool.Add(pawn);
            if (pool.Count < 2) return;
            for (int i = 0; i < Swaps; i++)
            {
                var a = pool.RandomElement(); var b = pool.RandomElement();
                if (a != b) BoogieWoogie.Swap(a, b);
            }
            foreach (var x in pool)
                if (x.HostileTo(pawn))
                {
                    x.stances?.stunner?.StunFor(DazeTicks, pawn, false);
                    MoteMaker.ThrowText(x.DrawPos, map, "Confused", new Color(1f, 0.85f, 0.4f), 1.5f);
                }
        }
    }

    public class Ability_BoogieWoogieDeflect : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            reason = BoogieWoogie.CantClap(pawn);
            if (reason != null) return false;
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            BoogieWoogie.Clap(pawn);
        }
    }

    /// <summary>Return fire: an enemy shot that reaches him is swapped back and sent at whoever fired it.</summary>
    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_ReturnFire
    {
        private static readonly List<Pawn> users = new List<Pawn>();

        public static bool Prefix(Projectile __instance)
        {
            if (!__instance.Spawned || JJKCharacterDefOf.JJK_ReturnFire == null) return true;
            var launcher = __instance.Launcher;
            if (launcher == null || !launcher.Spawned) return true;
            var map = __instance.Map;
            var pos = __instance.ExactPosition;
            var cell = pos.ToIntVec3();
            if (!cell.InBounds(map)) return true;
            foreach (var t in cell.GetThingList(map))
            {
                if (!(t is Pawn p) || !Jujutsu.Has(p, JJKCharacterDefOf.JJK_ReturnFire) || !launcher.HostileTo(p)) continue;
                var back = (Projectile)GenSpawn.Spawn(__instance.def, p.Position, map);
                // a psycast's shot (VEF AbilityProjectile, or one with CompAbilityProjectile) needs its ability on impact:
                // VEF reads it without a null check, so a fresh copy without it threw on landing and did nothing
                if (__instance is VEF.Abilities.AbilityProjectile ap && back is VEF.Abilities.AbilityProjectile bp) bp.ability = ap.ability;
                var compFrom = __instance.TryGetComp<VEF.Abilities.CompAbilityProjectile>();
                var compTo = back.TryGetComp<VEF.Abilities.CompAbilityProjectile>();
                if (compFrom != null && compTo != null) compTo.ability = compFrom.ability;
                back.Launch(p, p.DrawPos, launcher, launcher, ProjectileHitFlags.IntendedTarget, false, null, null);
                FleckMaker.ThrowMicroSparks(pos, map);
                MoteMaker.ThrowText(p.DrawPos, map, "Returned", Color.white, 1.2f);
                PsycastContext.Forget(__instance);
                __instance.Destroy();
                return false;
            }
            return true;
        }
    }

    /// <summary>Installing or removing the Vibraslap changes which Boogie Woogie abilities Todo has.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_Vibraslap_Installed
    {
        public static void Postfix(Hediff hediff)
        {
            if (hediff?.def == JJKCharacterDefOf.JJK_Vibraslap && CharacterTracker.IsTodo(hediff.pawn)) BoogieWoogie.Sync(hediff.pawn);
        }
    }
}
