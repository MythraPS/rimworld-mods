using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace GojoPsycasts
{
    /// <summary>Dev mode ▸ Debug actions ▸ "Satoru Gojo" (her ask 2026-11-03): set up each thing
    /// that needs testing without waiting for it. "Tool" actions are clicked on a pawn or a cell.</summary>
    public static class GojoDebug
    {
        private const string Cat = "Jujutsu Kaisen";   // one section for the whole mod (merged 2026-11-10)

        private static void Say(string s) => Messages.Message("[Gojo dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        private static IEnumerable<VEFAbilityDef> AllTechniques() =>
            typeof(GojoAbilityDefOf).GetFields().Select(f => f.GetValue(null)).OfType<VEFAbilityDef>();

        // ---------------------------------------------------------------- getting Gojo

        [DebugAction(Cat, "Gojo: arrives now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Arrives()
        {
            if (Satoru.Exists()) { Say("A Satoru Gojo already exists in this game, so the event won't fire. Use \"Spawn a Gojo here\" for another one."); return; }
            var parms = StorytellerUtility.DefaultParmsNow(GojoDefOf.Gojo_Arrives.category, Find.CurrentMap);
            if (!GojoDefOf.Gojo_Arrives.Worker.TryExecute(parms)) Say("The arrival event failed (no map edge cell to walk in from?).");
        }

        [DebugAction(Cat, "Gojo: spawn here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnHere()
        {
            var cell = UI.MouseCell(); var map = Find.CurrentMap;
            if (!cell.InBounds(map) || !cell.Standable(map)) { Say("Click a cell he can stand on."); return; }
            var p = Satoru.Make();
            GenSpawn.Spawn(p, cell, map);
            Say("Spawned a Satoru Gojo (made the same way as the arrival event).");
        }

        [DebugAction(Cat, "Gojo: spawn a young Gojo here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnYoungHere()
        {
            var cell = UI.MouseCell(); var map = Find.CurrentMap;
            if (!cell.InBounds(map) || !cell.Standable(map)) { Say("Click a cell he can stand on."); return; }
            var p = Satoru.Make(true);
            GenSpawn.Spawn(p, cell, map);
            var psy = Gojo.Psycasts(p, false);
            Say($"Spawned a young Satoru Gojo: psycast level {psy?.level}, {psy?.points} points left to spend.");
        }

        [DebugAction(Cat, "Gojo: check this Gojo", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Check(Pawn p)
        {
            if (p == null) return;
            var lines = new List<string>();
            System.Action<bool, string> Item = (ok, what) => lines.Add((ok ? "OK    " : "WRONG ") + what);
            var psy = Gojo.Psycasts(p, false);
            var comp = p.GetComp<CompAbilities>();
            bool isSatoru = p.kindDef == GojoDefOf.Gojo_Satoru;

            Item(Gojo.HasSixEyes(p), "has the Six Eyes trait");
            Item(p.health.hediffSet.HasHediff(GojoDefOf.Gojo_Limitless), "has the Limitless hediff");
            Item(psy != null && psy.unlockedPaths.Contains(GojoDefOf.Gojo_Path), "Limitless path unlocked");
            if (isSatoru)
            {
                Item(psy != null && psy.level >= Satoru.StartingPsycastLevel, $"psycast level {psy?.level} (should be {Satoru.StartingPsycastLevel}+)");
                var missing = AllTechniques().Where(d => comp == null || !comp.HasAbility(d)).Select(d => d.label).ToList();
                Item(missing.Count == 0, missing.Count == 0 ? "knows every technique" : "missing: " + string.Join(", ", missing));
                Item(psy != null && psy.points == 0, $"unspent points: {psy?.points} (a new Gojo should have 0)");
                foreach (var f in new[] { "Void", "VPE_Science" })
                {
                    var def = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(f);
                    Item(def != null && psy != null && psy.unlockedMeditationFoci.Contains(def), $"meditation focus {f}");
                }
                Item(HasTrait(p, "Brawler", 0), "Brawler");
                Item(HasTrait(p, "SpeedOffset", 2), "Jogger");
                Item(HasTrait(p, "Tough", 0), "Tough");
                Item(p.story.traits.HasTrait(GojoDefOf.Gojo_Selfish), "Selfish");
                if (Satoru.IsYoung(p)) Item(p.apparel?.WornApparel.Any(a => a.def == GojoDefOf.Gojo_SunglassesApparel) == true, "wears the round sunglasses (clothing)");
                else Item(p.apparel?.WornApparel.Any(a => a.def == GojoDefOf.Gojo_BlindfoldApparel) == true, "wears the blindfold (clothing)");
                Item(p.style?.FaceTattoo != GojoDefOf.Gojo_Eyes && p.style?.FaceTattoo != GojoDefOf.Gojo_Blindfold, "no old eye or blindfold face tattoo (the eyes are drawn by Six Eyes now)");
                Item(p.equipment?.Primary == null, "no weapon");
            }
            var tr = p.psychicEntropy;
            if (tr != null) lines.Add($"      psyfocus {tr.CurrentPsyfocus:P0}, heat {tr.EntropyValue:0}/{tr.MaxEntropy:0}");
            string s = $"{p.LabelShort}{(isSatoru ? " (Satoru Gojo)" : "")}\n\n" + string.Join("\n", lines);
            Log.Message("[Gojo dev] " + s.Replace("\n", " | "));
            Find.WindowStack.Add(new Dialog_MessageBox(s));
        }

        private static bool HasTrait(Pawn p, string defName, int degree)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def != null && p.story?.traits?.HasTrait(def, degree) == true;
        }

        [DebugAction(Cat, "Gojo: give Six Eyes", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void GiveSixEyes(Pawn p)
        {
            if (p?.story?.traits == null) return;
            if (Gojo.HasSixEyes(p)) { Say($"{p.LabelShort} already has the Six Eyes."); return; }
            p.story.traits.GainTrait(new Trait(GojoDefOf.Gojo_SixEyes, 0, true));
            Say($"{p.LabelShort} has the Six Eyes. The path and hediff arrive within a few seconds (the same check a trait from an editor gets).");
        }

        [DebugAction(Cat, "Gojo: make a baby of this pawn (Six Eyes inheritance)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Baby(Pawn parent)
        {
            if (parent == null || !parent.RaceProps.Humanlike) return;
            var stage = ModsConfig.BiotechActive ? DevelopmentalStage.Newborn : DevelopmentalStage.Adult;
            var req = new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true,
                canGeneratePawnRelations: false, allowDowned: true, developmentalStages: stage);
            var child = PawnGenerator.GeneratePawn(req);
            child.relations.AddDirectRelation(PawnRelationDefOf.Parent, parent);
            GenSpawn.Spawn(child, CellFinder.StandableCellNear(parent.Position, parent.Map, 3f), parent.Map);
            Say($"{child.LabelShort} is {parent.LabelShort}'s {(stage == DevelopmentalStage.Newborn ? "baby" : "child (Biotech is off, so an adult)")}. "
                + (Gojo.HasSixEyes(parent) ? "Within a few seconds the child should get the Six Eyes." : "The parent has no Six Eyes, so the child shouldn't get them."));
        }

        // ---------------------------------------------------------------- psyfocus, heat, cooldowns

        [DebugAction(Cat, "Pawn: neural heat to maximum", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MaxHeat(Pawn p)
        {
            var tr = p?.psychicEntropy;
            if (tr == null) return;
            tr.TryAddEntropy(Mathf.Max(0f, tr.MaxEntropy - tr.EntropyValue), p, scale: false, overLimit: true);
            Say($"{p.LabelShort}: heat {tr.EntropyValue:0}/{tr.MaxEntropy:0}. Infinity should drop.");
        }

        [DebugAction(Cat, "Pawn: reset all cooldowns", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ResetCooldowns(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) { Say($"{p?.LabelShort} has no abilities."); return; }
            int n = 0;
            foreach (var a in AbilitiesOf(comp))
                foreach (var f in a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             .Where(f => f.FieldType == typeof(int) && f.Name.ToLowerInvariant().Contains("cooldown")))
                {
                    f.SetValue(a, 0); n++;
                }
            Say(n > 0 ? $"{p.LabelShort}: every cooldown cleared." : "Couldn't find the cooldown field (Vanilla Expanded may have renamed it).");
        }

        private static IEnumerable<object> AbilitiesOf(CompAbilities comp)
        {
            foreach (var m in new[] { "LearnedAbilities", "learnedAbilities" })
            {
                var prop = comp.GetType().GetProperty(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var field = comp.GetType().GetField(m, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if ((prop?.GetValue(comp) ?? field?.GetValue(comp)) is System.Collections.IEnumerable list)
                    return list.Cast<object>().ToList();
            }
            return Enumerable.Empty<object>();
        }

        // ---------------------------------------------------------------- things to fight and fire at

        [DebugAction(Cat, "Test: spawn 5 enemies here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Enemies()
        {
            var map = Find.CurrentMap; var cell = UI.MouseCell();
            var faction = Find.FactionManager.RandomEnemyFaction(false, false, true, TechLevel.Industrial);
            if (faction == null) { Say("No enemy faction in this world."); return; }
            var kind = faction.def.basicMemberKind ?? PawnKindDefOf.Villager;
            var pawns = new List<Pawn>();
            for (int i = 0; i < 5; i++)
            {
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, mustBeCapableOfViolence: true));
                GenSpawn.Spawn(p, CellFinder.StandableCellNear(cell, map, 4f), map);
                pawns.Add(p);
            }
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false), map, pawns);
            Say($"5 hostile {faction.Name} attacking. For Infinity, the domain, Blue and Red.");
        }

        [DebugAction(Cat, "Gojo: place a held Red here", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void HeldRedHere()
        {
            var map = Find.CurrentMap; var cell = UI.MouseCell();
            if (!cell.InBounds(map)) return;
            var gojo = map.mapPawns.AllPawnsSpawned.Where(Gojo.HasSixEyes).OrderBy(p => p.Position.DistanceToSquared(cell)).FirstOrDefault();
            var held = (HeldRed)ThingMaker.MakeThing(GojoDefOf.Gojo_RedOrbHeld);
            held.caster = gojo;
            GenSpawn.Spawn(held, cell, map);
            Say(gojo == null ? "Held Red placed with no owner: no Six Eyes pawn on the map."
                : $"Held Red placed, owned by {gojo.LabelShort}. Cast Blue at it for Purple, or walk someone else into it.");
        }

        // ---------------------------------------------------------------- old saves

        [DebugAction(Cat, "Gojo: turn into an old-save Gojo (save test)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void MakeOld(Pawn p)
        {
            if (p?.kindDef != GojoDefOf.Gojo_Satoru) { Say("Click Satoru Gojo."); return; }
            var selfish = p.story.traits.GetTrait(GojoDefOf.Gojo_Selfish);
            if (selfish != null) p.story.traits.RemoveTrait(selfish);
            var blind = p.apparel?.WornApparel.FirstOrDefault(a => a.def == GojoDefOf.Gojo_BlindfoldApparel);
            if (blind != null) p.apparel.Remove(blind);
            blind?.Destroy();
            if (p.style != null) p.style.FaceTattoo = GojoDefOf.Gojo_Blindfold;
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            Say($"{p.LabelShort} now looks like a Gojo from before 31 October (no Selfish trait, blindfold as a face tattoo). "
                + "Within a few seconds the mod should upgrade him again: run \"Check this Gojo\" to see.");
        }
    }
}
