using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using VanillaPsycastsExpanded;
using VEF.Abilities;

namespace SukunaPsycasts
{
    /// <summary>Dev mode ▸ Debug actions ▸ "Sukuna" (her ask 2026-11-02): every step of the questline on demand,
    /// so it can be tested without waiting for the dice. "Tool" actions are clicked on a pawn or a cell.</summary>
    public static class SukunaDebug
    {
        private const string Cat = "Jujutsu Kaisen";   // one section for the whole mod (merged 2026-11-10)
        private static SukunaArc Arc => SukunaArc.Instance;
        private static Pawn TheYuji => QuestlineTracker.Instance?.yuji;

        private static void Say(string s) => Messages.Message("[Sukuna dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        // ---------------------------------------------------------------- state

        [DebugAction(Cat, "Sukuna: show questline state", allowedGameStates = AllowedGameStates.Playing)]
        private static void ShowState()
        {
            var q = QuestlineTracker.Instance; var a = Arc; var y = TheYuji;
            // read only: Ensure() would add a psycast hediff, and throws on a null Yuji
            var psy = y?.health?.hediffSet?.hediffs.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            string s = $"Yuji: {(y == null ? "not made" : y.LabelShort)}, stage {q?.stage}, regard {q?.regard:0}"
                     + $"\nYuji fingers {(y == null ? 0 : Scaling.FingersEaten(y))}, psycast level {psy?.level}"
                     + $", masters both trees: {SukunaArc.Masters(y)}, Benevolent: {SukunaArc.HasBenevolent(y)}, Modulo: {ModuloYuji.Is(y)}"
                     + $"\nArc stage {a?.stage}, escaped King: {a?.sukuna?.LabelShort ?? "none"}"
                     + $" ({(a?.sukuna == null ? "-" : a.sukuna.Dead ? "dead" : a.sukuna.Spawned ? "on a map" : "off map")})"
                     + $"\nFingers left in the world pool {FingerCensus.Remaining()}, destroyed {a?.fingersDestroyed}"
                     + $"\nPaintings left {PaintingCensus.Remaining()}, eaten {a?.paintingsEaten}";
            Log.Message("[Sukuna dev] " + s);
            Find.WindowStack.Add(new Dialog_MessageBox(s));
        }

        // ---------------------------------------------------------------- Yuji

        [DebugAction(Cat, "Yuji: joins now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void YujiJoins()
        {
            var t = QuestlineTracker.Instance;
            if (t == null) return;
            if (t.stage == YujiStage.Joined && TheYuji?.Spawned == true) { Say("Yuji has already joined."); return; }
            YujiDilemmas.Join(Find.CurrentMap);
        }

        [DebugAction(Cat, "Sukuna: feed 1 finger", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FeedOne(Pawn p) => Feed(p, 1);

        [DebugAction(Cat, "Sukuna: feed fingers up to 10", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FeedTen(Pawn p) => Feed(p, Mathf.Max(0, 10 - Scaling.FingersEaten(p)));

        [DebugAction(Cat, "Sukuna: feed fingers up to 19", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FeedNineteen(Pawn p) => Feed(p, Mathf.Max(0, 19 - Scaling.FingersEaten(p)));

        /// <summary>Quietly, no takeover roll (so a colonist doesn't turn while you set things up).</summary>
        private static void Feed(Pawn p, int n)
        {
            if (p == null || n <= 0) return;
            bool was = CompCursedFinger.Silent;
            CompCursedFinger.Silent = true;
            try { for (int i = 0; i < n; i++) CompCursedFinger.Consume(p, rollTakeover: false); }
            finally { CompCursedFinger.Silent = was; }
            Say($"{p.LabelShort} now has {Scaling.FingersEaten(p)} fingers.");
        }

        [DebugAction(Cat, "Sukuna: takes over Yuji now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Takeover()
        {
            var y = TheYuji;
            if (y == null || !y.Spawned) { Say("Yuji has to be on this map (use \"Yuji joins now\")."); return; }
            if (Arc.stage != ArcStage.None) { Say($"The takeover already happened (stage {Arc.stage}). Use \"Reset the escape questline\" first."); return; }
            if (SukunaArc.PickHost(y) == null) { Say("Nobody else on the map that Yuji can reach."); return; }
            if (!Arc.TryTakeover(y, true)) Say("Takeover failed to start (is Yuji downed or already in a mental state?).");
        }

        [DebugAction(Cat, "Sukuna: learn all of Shrine", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnShrine(Pawn p)
        {
            var psy = Prepare(p);
            if (psy == null) return;
            SukunaArc.Imprint(p);
            if (!psy.unlockedPaths.Contains(SukunaDefOf.Sukuna_Path)) psy.unlockedPaths.Add(SukunaDefOf.Sukuna_Path);
            var comp = p.GetComp<CompAbilities>();
            foreach (var a in SukunaDefOf.Sukuna_Path.abilities)
                if (!comp.HasAbility(a) && !(a == SukunaAbilityDefOf.Sukuna_MalevolentShrine && SukunaArc.HasBenevolent(p))) comp.GiveAbility(a);
            Say($"{p.LabelShort} knows all of Shrine.");
        }

        [DebugAction(Cat, "Yuji: learn all of Hemosage", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnHemosage(Pawn p)
        {
            var path = SukunaArc.Hemosage;
            if (path == null) { Say("The Hemosage mod isn't loaded."); return; }
            int guard = 0;
            var comp = p.GetComp<CompAbilities>();
            while (guard++ < 20 && path.abilities.Any(a => comp == null || !comp.HasAbility(a)))
            {
                CompDeathPainting.Eat(p);
                comp = p.GetComp<CompAbilities>();
                if (Arc != null) Arc.paintingsEaten--;   // learning by dev tool doesn't use up the world's paintings
            }
            Say($"{p.LabelShort} knows all of Hemosage.");
        }

        [DebugAction(Cat, "Yuji: give Benevolent Shrine", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GiveBenevolent(Pawn p) { SukunaArc.GrantBenevolent(p); Say($"{p.LabelShort} has Benevolent Shrine."); }

        [DebugAction(Cat, "Yuji: give Piercing Blood: Scatter", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GivePiercing(Pawn p)
        {
            var comp = p.GetComp<CompAbilities>();
            if (comp != null && !comp.HasAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter)) comp.GiveAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter);
            Say($"{p.LabelShort} has Piercing Blood: Scatter.");
        }

        [DebugAction(Cat, "Yuji: turn into Modulo Yuji", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ascend(Pawn p) { ModuloYuji.Ascend(p); Say($"{p.LabelShort} has taken the Modulo form."); }

        [DebugAction(Cat, "Yuji: spawn Modulo Yuji", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnModulo()
        {
            var p = ModuloYuji.Make();
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
        }

        // ---------------------------------------------------------------- the escaped King

        [DebugAction(Cat, "Sukuna: skip ahead, he has left the map", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SukunaLeaves()
        {
            var s = Arc?.sukuna;
            if (s == null || s.Dead || Arc.stage != ArcStage.Escaping) { Say("Only right after the takeover, while he is still here."); return; }
            if (s.Spawned) { s.GetLord()?.RemovePawn(s); s.DeSpawn(); }
            if (!Find.WorldPawns.Contains(s)) Find.WorldPawns.PassToWorld(s, RimWorld.Planet.PawnDiscardDecideMode.KeepForever);
            Say("Sukuna is off the map; the letter comes on the next check (a few seconds).");
        }

        [DebugAction(Cat, "Sukuna: open his lair now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OpenLair()
        {
            if (Arc?.sukuna == null || Arc.sukuna.Dead) { Say("There is no escaped Sukuna (do the takeover first)."); return; }
            var y = TheYuji;
            if (y != null && !SukunaArc.HasBenevolent(y)) SukunaArc.GrantBenevolent(y);
            Arc.DebugOpenLair();
        }

        [DebugAction(Cat, "Sukuna: bring the escaped Sukuna here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BringSukuna()
        {
            var s = Arc?.sukuna;
            if (s == null || s.Dead) { Say("There is no escaped Sukuna."); return; }
            if (s.Spawned) { s.GetLord()?.RemovePawn(s); s.DeSpawn(); }
            if (Find.WorldPawns.Contains(s)) Find.WorldPawns.RemovePawn(s);
            SukunaArc.KeepForever(s);
            GenSpawn.Spawn(s, UI.MouseCell(), Find.CurrentMap);
            SukunaFaction.Claim(s);
            VesselMaker.ArmForAI(s);
            if (Arc.stage == ArcStage.Escaping || Arc.stage == ArcStage.Hunting) Arc.stage = ArcStage.Lair;   // fight him here
        }

        [DebugAction(Cat, "Sukuna: cut soul 50%", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CutHalf(Pawn p) => Cut(p, 0.5f);

        [DebugAction(Cat, "Sukuna: cut soul 100% (exorcise)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void CutAll(Pawn p) => Cut(p, 1f);

        private static void Cut(Pawn p, float f)
        {
            if (!SukunaBehaviour.IsSukuna(p)) { Say($"{p.LabelShort} isn't Sukuna."); return; }
            SukunaArc.Sever(p, f);
        }

        // ---------------------------------------------------------------- items

        [DebugAction(Cat, "Yuji: spawn a Death Painting", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnPainting() =>
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(SukunaArcDefOf.Sukuna_DeathPainting), UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);

        [DebugAction(Cat, "Sukuna: spawn a cursed finger", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnFinger() =>
            GenPlace.TryPlaceThing(ThingMaker.MakeThing(SukunaThingDefOf.Sukuna_Finger), UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near);

        [DebugAction(Cat, "Yuji: start a painting quest", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PaintingQuest()
        {
            var def = SukunaArcDefOf.Sukuna_PaintingRumour;
            var parms = StorytellerUtility.DefaultParmsNow(def.category, Find.CurrentMap);
            parms.forced = true;
            if (!def.Worker.TryExecute(parms)) Say($"No painting quest could start (all {PaintingCensus.Total} may already be out in the world).");
        }

        // ---------------------------------------------------------------- more test shortcuts (2026-11-06)

        [DebugAction(Cat, "Yuji: encounter now (next dilemma)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Encounter() => Fire(SukunaQuestDefOf.Sukuna_YujiEncounter, "No Yuji encounter could start (has he already joined?).");

        [DebugAction(Cat, "Yuji: regard to 45 (one good deed from joining)", allowedGameStates = AllowedGameStates.Playing)]
        private static void Regard45() => SetRegard(45f);

        [DebugAction(Cat, "Yuji: regard to -60", allowedGameStates = AllowedGameStates.Playing)]
        private static void RegardLow() => SetRegard(-60f);

        private static void SetRegard(float r)
        {
            var t = QuestlineTracker.Instance; if (t == null) return;
            t.regard = r; Say($"Yuji's regard is now {r:0} ({Yuji.RegardWord(r)}). He joins at {Yuji.JoinAt:0}.");
        }

        [DebugAction(Cat, "Sukuna: finger quest now (rumour)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FingerQuest() => Fire(SukunaQuestDefOf.Sukuna_FingerRumour, "No finger quest could start (all 20 may already be out in the world).");

        [DebugAction(Cat, "Sukuna: raid, a vessel with fingers", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VesselRaid() => Fire(SukunaQuestDefOf.Sukuna_VesselRaid, "No vessel raid could start.");

        [DebugAction(Cat, "Sukuna: raid, the King of Curses", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void KingRaid() => Fire(SukunaQuestDefOf.Sukuna_KingRaid, "No King raid could start (is there already a Sukuna?).");

        private static void Fire(IncidentDef def, string fail)
        {
            if (def == null) { Say("That event is missing from the defs."); return; }
            var parms = StorytellerUtility.DefaultParmsNow(def.category, Find.CurrentMap);
            parms.forced = true;
            if (!def.Worker.TryExecute(parms)) Say(fail);
        }

        [DebugAction(Cat, "Sukuna: feed fingers up to 20", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FeedTwenty(Pawn p) => Feed(p, Mathf.Max(0, 20 - Scaling.FingersEaten(p)));

        /// <summary>A full-power Sukuna (20 fingers) where you click, made the way the King raid makes him:
        /// an enemy fighter who swallowed the fingers (her ask 2026-11-07).</summary>
        [DebugAction(Cat, "Sukuna: spawn here (20 fingers)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnSukuna()
        {
            if (SukunaBehaviour.AnySukunaExists()) { Say("There is already a Sukuna somewhere. Only one can exist."); return; }
            var map = Find.CurrentMap;
            var host = Find.FactionManager.RandomEnemyFaction(false, false, false, TechLevel.Undefined);
            var kind = host?.def?.basicMemberKind ?? PawnKindDefOf.Villager;
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                kind, host, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                mustBeCapableOfViolence: true, developmentalStages: DevelopmentalStage.Adult));
            var cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map))   // never inside a wall
                CellFinder.TryFindRandomCellNear(cell.ClampInsideMap(map), map, 6, c => c.Standable(map), out cell);
            GenSpawn.Spawn(p, cell, map);
            VesselMaker.Make(p, 20);
            Transformation.Become(p, sendLetter: false);
            Say(SukunaBehaviour.IsSukuna(p) ? $"Ryomen Sukuna ({p.LabelShort}) is here with 20 fingers." : "He didn't turn (see the log).");
        }

        /// <summary>Testing (her ask 2026-11-11): a full-power Sukuna who is your colonist. Made as a colonist and
        /// turned with "turned colonists stay yours" on for that moment only.</summary>
        [DebugAction(Cat, "Sukuna: spawn here as a colonist (testing, 20 fingers)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnFriendlySukuna()
        {
            if (SukunaBehaviour.AnySukunaExists()) { Say("There is already a Sukuna somewhere. Only one can exist."); return; }
            var map = Find.CurrentMap;
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true,
                mustBeCapableOfViolence: true, developmentalStages: DevelopmentalStage.Adult));
            var cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map))
                CellFinder.TryFindRandomCellNear(cell.ClampInsideMap(map), map, 6, c => c.Standable(map), out cell);
            GenSpawn.Spawn(p, cell, map);
            VesselMaker.Make(p, 20);
            bool was = Transformation.FriendlyColonistsForTesting;
            Transformation.FriendlyColonistsForTesting = true;
            try { Transformation.Become(p, sendLetter: false); }
            finally { Transformation.FriendlyColonistsForTesting = was; }
            Say(SukunaBehaviour.IsSukuna(p) ? $"Ryomen Sukuna ({p.LabelShort}) is here as your colonist, with 20 fingers." : "He didn't turn (see the log).");
        }

        [DebugAction(Cat, "Sukuna: turn this pawn into Sukuna", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeSukuna(Pawn p)
        {
            if (p == null) return;
            if (Yuji.Is(p)) { Say("Yuji never turns."); return; }
            if (SukunaBehaviour.AnySukunaExists()) { Say("There is already a Sukuna somewhere. Only one can exist."); return; }
            bool friendly = Transformation.FriendlyColonistsForTesting && p.Faction?.IsPlayer == true;
            Transformation.Become(p, true, !friendly);
        }

        [DebugAction(Cat, "Sukuna: turned colonists stay yours (toggle)", allowedGameStates = AllowedGameStates.Playing)]
        private static void ToggleFriendly()
        {
            Transformation.FriendlyColonistsForTesting = !Transformation.FriendlyColonistsForTesting;
            Say(Transformation.FriendlyColonistsForTesting
                ? "ON: a colonist who turns into Sukuna stays in your colony and takes orders. Saved with this game."
                : "OFF: a colonist who turns into Sukuna becomes the enemy (the normal rule).");
        }

        [DebugAction(Cat, "Sukuna: force true form (four arms)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void TrueForm(Pawn p)
        {
            if (p == null) return;
            if (!SukunaBehaviour.IsSukuna(p)) { Say("Only Sukuna takes his true form (use \"Turn this pawn into Sukuna\" first)."); return; }
            FourArmed.Take(p);
        }

        [DebugAction(Cat, "Yuji: poison with his blood (one dose)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void PoisonDose(Pawn p)
        {
            if (p == null) return;
            if (PoisonBlood.IsYuji(p)) { Say($"{p.LabelShort} has poisonous blood, so it can't poison {p.LabelShort}."); return; }
            PoisonBlood.Poison(p, PoisonBlood.MistDose);
            var h = PoisonBlood.Def == null ? null : p.health.hediffSet.GetFirstHediffOfDef(PoisonBlood.Def);
            Say($"{p.LabelShort}: poisoned blood {(h == null ? "none" : h.Severity.ToString("0.00"))} (stages at 0.30, 0.60, 0.85).");
        }

        [DebugAction(Cat, "Yuji: give the poisonous blood trait", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GivePoisonTrait(Pawn p) { PoisonBlood.EnsureTrait(p); Say($"{p?.LabelShort} has poisonous blood."); }

        [DebugAction(Cat, "Yuji: dress in his uniform", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DressYuji(Pawn p)
        {
            if (p == null) return;
            if (!Yuji.Is(p)) { Say("That isn't Yuji."); return; }
            Yuji.Dress(p);
            Say("Yuji is back in his uniform (this replaces whatever he was wearing).");
        }

        // ---------------------------------------------------------------- reset

        [DebugAction(Cat, "Sukuna: reset the escape questline", allowedGameStates = AllowedGameStates.Playing)]
        private static void Reset()
        {
            var a = Arc;
            if (a == null) return;
            if (a.sukuna != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(a.sukuna);
            a.stage = ArcStage.None;
            a.sukuna = null;
            a.lair = null;
            a.lairRetryTick = -1;
            Say("Escape questline reset to the start. The old escaped Sukuna, if alive, is now just an ordinary Sukuna.");
        }

        private static Hediff_PsycastAbilities Prepare(Pawn p)
        {
            if (p == null) return null;
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            return PsycastSetup.Ensure(p);
        }

        // ---------------------------------------------------------------- Choso (2026-11-11)

        [DebugAction(Cat, "Choso: notices Yuji now (arrives at once, beside him)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChosoNow()
        {
            var c = ChosoTracker.Instance;
            if (c == null) return;
            if (c.arrived) { Say("Choso has already arrived."); return; }
            c.noticed = true; c.beside = true; c.arriveTick = Find.TickManager.TicksGame;
            c.Arrive();
        }

        [DebugAction(Cat, "Choso: notices Yuji now (arrives in an hour, walking in)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChosoSoon()
        {
            var c = ChosoTracker.Instance;
            if (c == null) return;
            if (c.arrived) { Say("Choso has already arrived."); return; }
            c.noticed = true; c.beside = false; c.arriveTick = Find.TickManager.TicksGame + GenDate.TicksPerHour;
            Say("Choso has noticed; he walks in within the hour.");
        }

        [DebugAction(Cat, "Choso: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ChosoSpawn()
        {
            var c = ChosoTracker.Instance;
            if (c == null) return;
            var p = c.Choso;
            if (p.Dead || p.Spawned) { Say("Choso is dead or already on a map."); return; }
            c.noticed = c.arrived = true;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Say("Choso is here and has joined (no painting given this way).");
        }

        [DebugAction(Cat, "Choso: show state", allowedGameStates = AllowedGameStates.Playing)]
        private static void ChosoState()
        {
            var c = ChosoTracker.Instance;
            if (c == null) return;
            string when = c.noticed && !c.arrived ? $", arrives in {(c.arriveTick - Find.TickManager.TicksGame) / (float)GenDate.TicksPerHour:0.0} h" : "";
            Say($"Choso: noticed {c.noticed}, arrived {c.arrived}{when}. A hit of {ChosoTracker.DamageForCertain} damage (or Yuji near death) is always noticed.");
        }

        [DebugAction(Cat, "Choso: reset (forget him)", allowedGameStates = AllowedGameStates.Playing)]
        private static void ChosoReset()
        {
            var c = ChosoTracker.Instance;
            if (c == null) return;
            c.choso = null; c.noticed = c.arrived = c.beside = false; c.arriveTick = -1;
            Say("Choso forgotten; the next one made is new.");
        }
    }
}
