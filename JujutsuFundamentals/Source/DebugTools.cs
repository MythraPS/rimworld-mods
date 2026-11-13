using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using VanillaPsycastsExpanded;
using VEF.Abilities;

namespace JujutsuFundamentals
{
    /// <summary>Dev mode ▸ Debug actions ▸ "Jujutsu" (her ask 2026-11-10): set a pawn up and test every
    /// technique without waiting on levels, dice or enemies. "Tool" actions are clicked on a pawn or a cell.</summary>
    public static class JujutsuDebug
    {
        private const string Cat = "Jujutsu Kaisen";   // one section for the whole mod (merged 2026-11-10)

        /// <summary>Every Black Flash roll succeeds while this is on (dev only, not saved).</summary>
        public static bool AlwaysBlackFlash;

        private static void Say(string s) => Messages.Message("[Jujutsu dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        private static Hediff_PsycastAbilities Prepare(Pawn p)
        {
            if (p == null) return null;
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            var psy = p.health.hediffSet.hediffs.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            if (psy != null) return psy;
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
            if (def == null) return null;
            psy = p.health.AddHediff(def) as Hediff_PsycastAbilities;
            var link = p.health.hediffSet.hediffs.Find(h => h is Hediff_Psylink) as Hediff_Psylink;
            if (psy != null && link != null)
            {
                try { psy.InitializeFromPsylink(link); }
                catch (System.Exception e) { Log.Warning("[JJK Core] InitializeFromPsylink failed: " + e.Message); }
            }
            return psy;
        }

        private static void LearnPath(Pawn p, PsycasterPathDef path)
        {
            var psy = Prepare(p);
            var comp = p?.GetComp<CompAbilities>();
            if (psy == null || comp == null || path == null) { Say("Couldn't set this pawn up as a psycaster."); return; }
            if (!psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            foreach (var a in path.abilities)
                if (!comp.HasAbility(a)) comp.GiveAbility(a);
            PassiveSync.Sync(p);
            Say($"{p.LabelShort} knows all of {path.label}.");
        }

        // ---------------------------------------------------------------- learning

        [DebugAction(Cat, "Pawn: learn all of Cursed Energy Fundamentals", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnFundamentals(Pawn p) => LearnPath(p, JJKDefOf.JJK_CursedEnergyFundamentals);

        [DebugAction(Cat, "Pawn: learn all of New Shadow Style", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnShadow(Pawn p) => LearnPath(p, JJKDefOf.JJK_NewShadowStyle);

        [DebugAction(Cat, "Pawn: learn both shared paths", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnBoth(Pawn p)
        {
            LearnPath(p, JJKDefOf.JJK_CursedEnergyFundamentals);
            LearnPath(p, JJKDefOf.JJK_NewShadowStyle);
        }

        [DebugAction(Cat, "Pawn: forget the shared paths' techniques", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Forget(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) return;
            foreach (var key in Toggles.Specs.Keys.ToList()) Toggles.Set(p, key, false, quiet: true);
            int n = comp.LearnedAbilities.RemoveAll(a =>
            {
                var path = a.def.GetModExtension<AbilityExtension_Psycast>()?.path;
                return path == JJKDefOf.JJK_CursedEnergyFundamentals || path == JJKDefOf.JJK_NewShadowStyle;
            });
            PassiveSync.Sync(p);
            Say($"{p.LabelShort} forgot {n} technique(s).");
        }

        [DebugAction(Cat, "Pawn: born with a cursed technique (pick one)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GiveInnate(Pawn p)
        {
            if (p == null) return;
            var old = Innate.Marker(p);
            if (old != null) { Say($"{p.LabelShort} was already born with {Innate.Label(old.technique)}."); return; }
            Find.WindowStack.Add(new FloatMenu(Innate.All.Select(k => new FloatMenuOption(Innate.Label(k), () =>
            {
                Innate.Mark(p, k);
                Say($"{p.LabelShort} now has {Innate.Label(k)} from birth. It wakes once they have a psylink.");
            })).ToList()));
        }

        [DebugAction(Cat, "Pawn: add 10 psycast levels", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TenLevels(Pawn p)
        {
            var psy = Prepare(p);
            if (psy == null) return;
            psy.ChangeLevel(10);
            Say($"{p.LabelShort} is psycast level {psy.level} with {psy.points} points to spend.");
        }

        [DebugAction(Cat, "Pawn: refill (full psyfocus, no neural heat, full hemogen)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Refill(Pawn p)
        {
            if (p?.psychicEntropy == null) return;
            p.psychicEntropy.OffsetPsyfocusDirectly(1f);
            p.psychicEntropy.RemoveAllEntropy();
            var g = SukunaPsycasts.YujiBlood.Hemogen(p);   // Yuji's blood techniques
            if (g != null) g.Value = g.Max;
            Say($"{p.LabelShort} refilled.");
        }

        [DebugAction(Cat, "Pawn: show technique info", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Info(Pawn p)
        {
            if (p == null) return;
            var on = Toggles.Specs.Keys.Where(k => Toggles.IsOn(p, k)).ToList();
            var bf = BlackFlashHit.PropsFor(p);
            var sd = Hediff_SimpleDomain.Of(p);
            var r = Hediff_Reinforcement.Of(p);
            string s = $"{p.LabelShort}\nPsychic sensitivity {Jujutsu.Sensitivity(p):P0}, power {Jujutsu.Power(p):0.00}"
                     + $", psycast level {BlackFlashRules.PsycastLevel(p)}"
                     + $"\nPsyfocus {p.psychicEntropy?.CurrentPsyfocus:P0}, neural heat {p.psychicEntropy?.EntropyValue:0}/{p.psychicEntropy?.MaxEntropy:0}"
                     + $"\nToggles on: {(on.Count == 0 ? "none" : string.Join(", ", on))}"
                     + $"\nReinforcement: {(r == null ? "not learned" : (r.Raised ? "raised" : "passive") + $", strength {r.K:0.00}")}"
                     + $"\nBlack Flash chance per melee hit: {(bf == null ? "not learned" : BlackFlashRules.Chance(p, bf).ToStringPercent("0.#"))}"
                     + (AlwaysBlackFlash ? " (dev: always on)" : "")
                     + $"\nSimple Domain: {(sd == null ? "down" : $"up, radius {sd.Radius:0.0}" + (sd.EveningMoon ? ", Evening Moon" : ""))}"
                     + $"\nSafe from sure hits: {SureHit.Blocked(p)}"
                     + $"\nHazy Moon blade: {HazyMoon.On(p)}, holding a melee weapon: {Jujutsu.HoldsMeleeWeapon(p)}";
            Log.Message("[Jujutsu dev] " + s);
            Find.WindowStack.Add(new Dialog_MessageBox(s));
        }

        [DebugAction(Cat, "Test: every hit is a Black Flash (toggle)", allowedGameStates = AllowedGameStates.Playing)]
        private static void ToggleBlackFlash()
        {
            AlwaysBlackFlash = !AlwaysBlackFlash;
            Say("Every hit a Black Flash: " + (AlwaysBlackFlash ? "ON" : "off") + " (only for pawns who have Black Flash).");
        }

        // ---------------------------------------------------------------- protection tests

        /// <summary>A domain's sure hit on every pawn within 4 cells: a cut and a 2-second stun, both
        /// blocked for anyone under Hollow Wicker Basket, Simple Domain or Domain Amplification.</summary>
        [DebugAction(Cat, "Test: sure hit here (4 cells)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SureHitHere()
        {
            var map = Find.CurrentMap; var c = UI.MouseCell();
            var hit = new List<string>(); var safe = new List<string>();
            foreach (var p in GenRadial.RadialDistinctThingsAround(c, map, 4f, true).OfType<Pawn>().ToList())
            {
                (SureHit.Blocked(p) ? safe : hit).Add(p.LabelShort);
                SureHit.Active = true;
                try
                {
                    p.TakeDamage(new DamageInfo(DamageDefOf.Cut, 8f, 0.5f));
                    p.stances?.stunner?.StunFor(120, null, false);
                }
                finally { SureHit.Active = false; }
            }
            FleckMaker.Static(c, map, FleckDefOf.PsycastAreaEffect, 8f);
            Say($"Sure hit. Hit: {(hit.Count == 0 ? "nobody" : string.Join(", ", hit))}. Safe: {(safe.Count == 0 ? "nobody" : string.Join(", ", safe))}.");
        }

        /// <summary>A psycast from the nearest enemy on the map at this pawn: damage, a stun, psychic shock
        /// and a berserk. A shielded pawn should take none of it.</summary>
        [DebugAction(Cat, "Test: hostile psycast at this pawn", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HostilePsycast(Pawn p)
        {
            var caster = p.Map.mapPawns.AllPawnsSpawned.Where(o => o != p && !o.Dead && o.HostileTo(p))
                          .OrderBy(o => o.Position.DistanceToSquared(p.Position)).FirstOrDefault();
            if (caster == null) { Say("No enemy on the map to cast it. Spawn one first (\"Spawn enemy gunner\")."); return; }
            bool shielded = Protection.Parried(p, caster);
            float before = p.health.summaryHealth.SummaryHealthPercent;
            PsycastContext.Push(caster);
            try
            {
                p.TakeDamage(new DamageInfo(DamageDefOf.Burn, 10f, 0.5f, -1f, caster));
                p.stances?.stunner?.StunFor(180, caster, false);
                var shock = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicShock");
                if (shock != null) p.health.AddHediff(shock);
                p.mindState?.mentalStateHandler?.TryStartMentalState(MentalStateDefOf.Berserk, "dev test", forced: true);
            }
            finally { PsycastContext.Pop(); }
            bool tookIt = p.health.summaryHealth.SummaryHealthPercent < before || (p.stances?.stunner?.Stunned ?? false) || p.InMentalState;
            Say($"Psycast from {caster.LabelShort} at {p.LabelShort} (shielded: {shielded}). It {(tookIt ? "landed" : "was blocked")}.");
        }

        // ---------------------------------------------------------------- injuries, for Reverse Cursed Technique

        [DebugAction(Cat, "Pawn: cut off an arm", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CutArm(Pawn p)
        {
            var arm = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == BodyPartDefOf.Arm);
            if (arm == null) { Say("No arm left to cut off."); return; }
            p.health.AddHediff(HediffDefOf.MissingBodyPart, arm);
            Say($"{p.LabelShort} lost the {arm.Label}.");
        }

        [DebugAction(Cat, "Pawn: add 5 cuts", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Cuts(Pawn p)
        {
            for (int i = 0; i < 5; i++) p.TakeDamage(new DamageInfo(DamageDefOf.Cut, 4f, 1f));
            Say($"{p.LabelShort} has 5 new cuts.");
        }

        // ---------------------------------------------------------------- enemies, for Evening Moon and Simple Domain

        private static void SpawnEnemy(string kind)
        {
            var map = Find.CurrentMap;
            var faction = Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false);
            var kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(kind);
            if (faction == null || kindDef == null) { Say("No enemy faction or pawn kind to spawn."); return; }
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef, faction, mustBeCapableOfViolence: true));
            GenSpawn.Spawn(p, UI.MouseCell(), map);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, new List<Pawn> { p });
            Say($"Spawned {p.LabelShort} ({kindDef.label}) of {faction.Name}.");
        }

        [DebugAction(Cat, "Test: spawn enemy gunner", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Gunner() => SpawnEnemy("Mercenary_Gunner");

        [DebugAction(Cat, "Test: spawn enemy melee fighter", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Slasher() => SpawnEnemy("Mercenary_Slasher");

        // ---------------------------------------------------------------- Kusakabe, Miwa and Todo

        private static CharacterTracker CT => CharacterTracker.Instance;

        [DebugAction(Cat, "Miwa quest: Kusakabe asks for help now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MiwaStart()
        {
            if (CT == null) return;
            if (CT.miwaQuest != null && !CT.miwaDone) { Say("The Miwa quest is already running."); return; }
            if (CT.miwaDone) { Say("The Miwa quest already ended. Use \"Miwa quest: reset\" first."); return; }
            Say(CT.StartMiwaQuest(Find.CurrentMap) ? "Miwa quest started." : "Couldn't start the Miwa quest (no site tile or enemy faction found).");
        }

        [DebugAction(Cat, "Miwa quest: show state", allowedGameStates = AllowedGameStates.Playing)]
        private static void MiwaState()
        {
            var c = CT;
            if (c == null) return;
            int now = Find.TickManager.TicksGame;
            float hours = c.questStartTick < 0 ? 0f : (now - c.questStartTick) / 2500f;
            string s = $"Asked for help: {c.askedForHelp}, quest: {(c.miwaQuest == null ? "none" : c.miwaQuest.State.ToString())}, done: {c.miwaDone}"
                     + $"\nHours since accepted: {hours:0.0} (catch her within {CharacterTracker.InterceptTicks / 2500}, deadline {CharacterTracker.DeadlineTicks / 2500})"
                     + $"\nSite: {(c.miwaSite == null ? "none" : c.miwaSite.Label + (c.miwaSite.HasMap ? " (map open)" : ""))}"
                     + $"\nMiwa: {Who(c.miwa)}, on the site: {c.miwaOnSite}, caught on the way: {c.intercepted}"
                     + $"\nKusakabe: {Who(c.kusakabe)}\nTodo: {Who(c.todo)}, arrived: {c.todoArrived}";
            Log.Message("[Jujutsu dev] " + s);
            Find.WindowStack.Add(new Dialog_MessageBox(s));
        }

        private static string Who(Pawn p) => p == null ? "not made" : p.Dead ? "dead" : p.Spawned ? $"on a map ({p.Faction?.Name ?? "no faction"})" : "off map";

        [DebugAction(Cat, "Miwa quest: too late to catch her", allowedGameStates = AllowedGameStates.Playing)]
        private static void MiwaLate()
        {
            if (CT == null || CT.miwaQuest == null) { Say("No Miwa quest running."); return; }
            CT.questStartTick = Find.TickManager.TicksGame - CharacterTracker.InterceptTicks - 1;
            Say("The two days are up: Miwa will be inside the outpost when you arrive.");
        }

        [DebugAction(Cat, "Miwa quest: deadline passes now", allowedGameStates = AllowedGameStates.Playing)]
        private static void MiwaDeadline()
        {
            if (CT == null || CT.miwaQuest == null) { Say("No Miwa quest running."); return; }
            CT.questStartTick = Find.TickManager.TicksGame - CharacterTracker.DeadlineTicks - 1;
            Say("Deadline passed. If nobody is at the outpost, the quest fails within a few seconds.");
        }

        [DebugAction(Cat, "Miwa quest: reset (forget everything)", allowedGameStates = AllowedGameStates.Playing)]
        private static void MiwaReset()
        {
            var c = CT;
            if (c == null) return;
            if (c.miwaQuest != null && c.miwaQuest.State == QuestState.Ongoing) c.miwaQuest.End(QuestEndOutcome.Unknown, false);
            if (c.miwaSite != null && !c.miwaSite.Destroyed && !c.miwaSite.HasMap) c.miwaSite.Destroy();
            c.miwaQuest = null; c.miwaSite = null; c.askedForHelp = false; c.miwaDone = false;
            c.questStartTick = -1; c.miwaOnSite = false; c.intercepted = false; c.warned = false;
            if (c.miwa != null && !c.miwa.Spawned) c.miwa = null;
            if (c.kusakabe != null && !c.kusakabe.Spawned) c.kusakabe = null;
            Say("Miwa quest reset. Kusakabe and Miwa will be made fresh next time (unless already on a map).");
        }

        [DebugAction(Cat, "Kusakabe: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnKusakabe() => Spawn(c => c.kusakabe ?? (c.kusakabe = Characters.MakeKusakabe()));

        [DebugAction(Cat, "Miwa: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnMiwa() => Spawn(c => c.miwa ?? (c.miwa = Characters.MakeMiwa()));

        [DebugAction(Cat, "Todo: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnTodo() => Spawn(c => { c.todoArrived = true; return c.todo ?? (c.todo = Characters.MakeTodo()); });

        private static void Spawn(System.Func<CharacterTracker, Pawn> get)
        {
            if (CT == null) return;
            var p = get(CT);
            if (p == null || p.Dead) { Say("That character is dead."); return; }
            if (p.Spawned) { Say($"{p.LabelShort} is already on a map."); return; }
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Say($"{p.LabelShort} is here and has joined.");
        }

        [DebugAction(Cat, "Todo: arrives now (as if for Yuji)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TodoNow()
        {
            if (CT == null) return;
            if (CT.todoArrived) { Say("Todo has already arrived."); return; }
            CT.TodoArrives(Find.CurrentMap);
        }

        [DebugAction(Cat, "Todo: install a vibraslap (left hand)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InstallVibraslap(Pawn p)
        {
            var hand = p.RaceProps.body.AllParts.FirstOrDefault(x => x.def == BodyPartDefOf.Hand && x.Label.Contains("left"))
                       ?? p.RaceProps.body.AllParts.FirstOrDefault(x => x.def == BodyPartDefOf.Hand);
            if (hand == null) { Say("No hand to replace."); return; }
            p.health.RestorePart(hand);
            p.health.AddHediff(JJKCharacterDefOf.JJK_Vibraslap, hand);
            Say($"{p.LabelShort} has a vibraslap in place of the {hand.Label}.");
        }

        [DebugAction(Cat, "Pawn: cut off left hand", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CutHand(Pawn p)
        {
            var hand = p.health.hediffSet.GetNotMissingParts().FirstOrDefault(x => x.def == BodyPartDefOf.Hand && x.Label.Contains("left"));
            if (hand == null) { Say("No left hand to cut off."); return; }
            p.health.AddHediff(HediffDefOf.MissingBodyPart, hand);
            Say($"{p.LabelShort} lost the {hand.Label}.");
        }

        [DebugAction(Cat, "Todo: spawn a vibraslap item", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VibraslapItem() =>
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("JJK_Vibraslap")), UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);

        // ---------------------------------------------------------------- Ryu and domains

        [DebugAction(Cat, "Ryu: arrives now (hostile)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RyuNow()
        {
            if (CT == null) return;
            if (CT.ryu != null && (CT.ryu.Spawned || CT.ryu.Dead)) { Say("Ryu is already on a map, or dead."); return; }
            Say(Ryu.ArriveHostile(Find.CurrentMap) ? "Ryu has arrived." : "Couldn't bring Ryu in (no enemy faction?).");
        }

        [DebugAction(Cat, "Ryu: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnRyu() => Spawn(c => { c.ryuArrived = true; return c.ryu ?? (c.ryu = Ryu.Make()); });

        /// <summary>A domain owned by the pawn nearest the click (any side: allies clash too), with that pawn's
        /// psychic sensitivity as its refinement; no effect, gone after 20 seconds.</summary>
        private static void TestDomain(bool sukunaLike)
        {
            var map = Find.CurrentMap; var c = UI.MouseCell();
            var owner = map.mapPawns.AllPawnsSpawned.Where(p => !p.Dead && p.RaceProps.Humanlike)
                           .OrderBy(p => p.Position.DistanceToSquared(c)).FirstOrDefault();
            if (owner == null) { Say("Needs a person on the map to own the domain."); return; }
            var d = new DomainInfo { caster = owner, map = map, center = c, radius = 9f, alwaysWins = sukunaLike,
                                     label = $"{owner.LabelShort}'s test domain" + (sukunaLike ? " (wins like Sukuna's)" : "") };
            d.collapse = () => TestDomains.Open.RemoveAll(x => x.d == d);
            TestDomains.Open.Add((d, Find.TickManager.TicksGame + 1200));
            bool stands = Domains.Open(d);
            Say(stands ? $"{d.label} is open for 20 seconds (refinement: psychic sensitivity {d.Refinement:P0})." : $"{d.label} collapsed at once.");
        }

        [DebugAction(Cat, "Test: domain here (owner = nearest person)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestDomainNormal() => TestDomain(false);

        [DebugAction(Cat, "Test: domain here that wins like Sukuna's", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TestDomainSukuna() => TestDomain(true);

        [DebugAction(Cat, "Test: list open domains", allowedGameStates = AllowedGameStates.Playing)]
        private static void ListDomains()
        {
            string s = Domains.Active.Count == 0 ? "No domains open." :
                string.Join("\n", Domains.Active.Select(d => $"{d.label} ({d.caster?.LabelShort}): psychic sensitivity {d.Refinement:P0}{(d.alwaysWins ? ", always wins" : "")}, radius {d.radius:0.0}"));
            Find.WindowStack.Add(new Dialog_MessageBox(s));
        }

        // ---------------------------------------------------------------- Cursed tools, Toji, Maki

        [DebugAction(Cat, "Cursed tools: spawn one of each here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void AllTools()
        {
            var map = Find.CurrentMap; var c = UI.MouseCell();
            foreach (var def in DefDatabase<ThingDef>.AllDefs.Where(d => d.thingSetMakerTags != null && d.thingSetMakerTags.Contains("JJK_CursedTool")).ToList())
            {
                if (def == JJKToolDefOf.JJK_PrisonRealm && Apparel_PrisonRealm.Exists) { Say("A Prison Realm already exists in this game; skipped it."); continue; }
                GenPlace.TryPlaceThing(ThingMaker.MakeThing(def), c, map, ThingPlaceMode.Near);
            }
        }

        [DebugAction(Cat, "Cursed tools: fill this pawn's Dragon-Bone (60)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FillDragonBone(Pawn p)
        {
            var comp = p.equipment?.Primary?.TryGetComp<CompDragonBone>();
            if (comp == null) { Say($"{p.LabelShort} isn't holding a Dragon-Bone."); return; }
            comp.stored = CursedTools.DragonBoneCap;
            Say("Dragon-Bone is full.");
        }

        [DebugAction(Cat, "Cursed tools: seal this pawn in the Prison Realm", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SealPawn(Pawn p)
        {
            var realm = Find.CurrentMap.listerThings.AllThings.Select(t => t as Apparel_PrisonRealm ?? (t as PrisonRealmTrap)?.Realm).FirstOrDefault(r => r != null)
                        ?? Find.CurrentMap.mapPawns.AllPawnsSpawned.SelectMany(x => x.apparel?.WornApparel ?? new List<Apparel>()).OfType<Apparel_PrisonRealm>().FirstOrDefault();
            if (realm == null) { Say("No Prison Realm on this map."); return; }
            Say(realm.Seal(p) ? $"{p.LabelShort} is sealed." : "Couldn't seal them (not a person, or the realm is full).");
        }

        private static RestrictedTracker RT => RestrictedTracker.Instance;

        [DebugAction(Cat, "Toji: offer his services now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TojiOffer()
        {
            var parms = StorytellerUtility.DefaultParmsNow(JJKRestrictedDefOf.JJK_TojiOffers.category, Find.CurrentMap);
            Say(JJKRestrictedDefOf.JJK_TojiOffers.Worker.TryExecute(parms) ? "Toji's offer has arrived." : "Toji can't offer now (already hired, or dead).");
        }

        [DebugAction(Cat, "Toji: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnToji() { if (RT == null) return; RT.tojiHired = true; SpawnPawn(RT.Toji); }

        [DebugAction(Cat, "Toji: spawn 2000 silver here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TojiSilver()
        {
            var silver = ThingMaker.MakeThing(ThingDefOf.Silver); silver.stackCount = Restricted.TojiPrice;
            GenPlace.TryPlaceThing(silver, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);
        }

        [DebugAction(Cat, "Maki: joins now (wanderer)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakiNow()
        {
            if (RT == null) return;
            if (RT.makiArrived) { Say("Maki has already arrived."); return; }
            Restricted.MakiJoins(Find.CurrentMap);
        }

        [DebugAction(Cat, "Maki: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnMaki() { if (RT == null) return; RT.makiArrived = true; SpawnPawn(RT.Maki); }

        [DebugAction(Cat, "Pawn: give Heavenly Restriction (zero cursed energy)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GiveRestriction(Pawn p)
        {
            if (p.story?.traits == null || ZeroCursedEnergy.Restricted(p)) return;
            p.story.traits.GainTrait(new Trait(JJKZeroDefOf.JJK_HeavenlyRestriction, 0, true));
            ZeroCursedEnergy.Sync(p);
            Say($"{p.LabelShort} now has zero cursed energy (psycasts removed).");
        }

        [DebugAction(Cat, "Toji/Maki: reset (forget both)", allowedGameStates = AllowedGameStates.Playing)]
        private static void ResetRestricted()
        {
            if (RT == null) return;
            RT.toji = null; RT.maki = null; RT.tojiHired = false; RT.makiArrived = false;
            Say("Forgot Toji and Maki; the next one made is new.");
        }

        private static void SpawnPawn(Pawn p)
        {
            if (p == null || p.Dead) { Say("That character is dead."); return; }
            if (p.Spawned) { Say($"{p.LabelShort} is already on a map."); return; }
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Say($"{p.LabelShort} is here and has joined.");
        }
    }
}
