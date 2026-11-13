using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Toge Inumaki (her calls 2026-11-11): every Cursed Speech command and Cursed Energy Reinforcement, and nothing else, ever (the core's
    /// Patch_YutaPathGate and Patch_RestrictedLearners). He arrives on his own, as a wanderer who joins, once per game.
    /// Age 17: a second-year at Tokyo Jujutsu High in the main story. Looks: short light ash hair (vanilla Fringe),
    /// the Jujutsu High uniform (the same pieces as Maki's), and the snake-and-fang crest at both corners of his mouth.
    /// Melee 8 and psycast level 10 are proposals; no traits are forced (hers to pick).
    /// </summary>
    public static partial class Inumaki
    {
        public static readonly Color Hair = new Color(0.82f, 0.82f, 0.8f);

        public static Pawn Make()
        {
            var tr = YutaTracker.Instance;
            if (tr == null) return null;
            if (tr.inumaki != null) return tr.inumaki;
            var p = Characters.Make("Toge", "Inumaki", "Inumaki", Gender.Male, 17f);
            tr.inumaki = p;   // before any ability, or the core turns them away
            DropDullness(p);
            // her calls 2026-11-11: Kind + Iron-willed; Melee 6, Shooting 4; Social disabled (his childhood backstory)
            Characters.Skill(p, SkillDefOf.Melee, 6, Passion.Minor);
            Characters.Skill(p, SkillDefOf.Shooting, 4, Passion.None);
            if (p.story?.traits != null)
            {
                p.story.traits.allTraits.Clear();
                var kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
                if (kind != null) p.story.traits.GainTrait(new Trait(kind, 0, true));
                var nerves = DefDatabase<TraitDef>.GetNamedSilentFail("Nerves");
                if (nerves != null) p.story.traits.GainTrait(new Trait(nerves, 2, true));   // iron-willed
            }
            Characters.Psycaster(p, 10);
            // her calls 2026-11-11: all of Cursed Speech, and Cursed Energy Reinforcement (given directly, so the
            // Fundamentals path stays closed to him)
            var commands = YutaAbilities.Speech.Select(YutaAbilities.Def).Where(d => d != null).ToArray();
            Characters.Learn(p, YutaDefOf.JJK_CursedSpeech, commands);
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (comp != null && !comp.HasAbility(JJKAbilityDefOf.JJK_Reinforcement)) comp.GiveAbility(JJKAbilityDefOf.JJK_Reinforcement);
            InumakiLooks.Apply(p);
            // the good guys share one ideoligion (coordinator's pick 2026-11-11)
            CharacterIdeos.Give(p, CharacterIdeos.Get("jujutsu", FactionDefOf.PlayerColony, "Collectivist"));
            PassiveSync.Sync(p);
            Looks.Refresh(p);
            return p;
        }

        /// <summary>Not a personality pick: a rolled psychically dull or deaf trait would leave his commands doing nothing.</summary>
        private static void DropDullness(Pawn p)
        {
            var traits = p.story?.traits;
            if (traits == null) return;
            foreach (var t in traits.allTraits.Where(t => t.def.defName == "PsychicSensitivity" && t.Degree < 0).ToList())
                traits.RemoveTrait(t);
        }

        public static bool Joins(Map map)
        {
            var tr = YutaTracker.Instance;
            if (tr == null || map == null || tr.inumakiArrived) return false;
            var p = Make();
            if (p == null || p.Dead) return false;
            tr.inumakiArrived = true;
            Characters.WalkInAndJoin(p, map);
            Find.LetterStack.ReceiveLetter("Toge Inumaki joins",
                "Toge Inumaki walks in and joins your colony.\n\nHe speaks only in rice ball fillings, so that no stray word becomes a command. "
                + "His Cursed Speech strains his throat; push it too far and he loses his voice for a while.",
                LetterDefOf.PositiveEvent, p);
            return true;
        }
    }

    public class IncidentWorker_InumakiJoins : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var tr = YutaTracker.Instance;
            return tr != null && !tr.inumakiArrived && parms.target is Map && (tr.inumaki == null || !tr.inumaki.Dead);
        }

        /// <summary>He asks to join (her call 2026-11-11: a real choice). He's made only if welcomed.</summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            JoinOffer.Send(JoinOffer.Inumaki, (Map)parms.target, "Toge Inumaki asks to join",
                "Toge Inumaki, a cursed speech user from Jujutsu High, is at the edge of the colony. He can't say much that isn't a rice ball "
                + "filling, but it's clear he's asking to join you.\n\nWelcome him and he walks in and joins. Refuse, and he may come by again another time.",
                def.letterDef);
            return true;
        }
    }

    public static class InumakiLooks
    {
        public static void Apply(Pawn p)
        {
            if (p?.story == null) return;
            if (p.genes != null)
                foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null || g.def.skinColorOverride != null).ToList())
                    p.genes.RemoveGene(g);
            p.story.bodyType = BodyTypeDefOf.Male;
            p.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail("Fringe") ?? p.story.hairDef;
            p.story.HairColor = Inumaki.Hair;
            p.story.skinColorOverride = Looks.Skin;
            // her call 2026-11-11: jujutsu student as a child (his own copy, with Social disabled), sorcerer as an adult
            p.story.Childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_InumakiStudent") ?? p.story.Childhood;
            p.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_Sorcerer") ?? p.story.Adulthood;
            p.Notify_DisabledWorkTypesChanged();
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("JJK_InumakiCrest") ?? TattooDefOf.NoTattoo_Face;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
            if (p.apparel == null) return;
            p.apparel.DestroyAll();
            Wear(p, "Apparel_CollarShirt", new Color(0.92f, 0.92f, 0.9f));
            Wear(p, "Apparel_Jacket", new Color(0.12f, 0.13f, 0.18f));
            Wear(p, "Apparel_Pants", new Color(0.12f, 0.13f, 0.18f));
        }

        private static void Wear(Pawn p, string defName, Color color)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            var stuff = def.MadeFromStuff ? (DefDatabase<ThingDef>.GetNamedSilentFail("Cloth") ?? GenStuff.DefaultStuffFor(def)) : null;
            var a = (Apparel)ThingMaker.MakeThing(def, stuff);
            a.SetColor(color, false);
            p.apparel.Wear(a, false, true);
        }
    }

    public static class SpeechDebug
    {
        private const string Cat = "Jujutsu Kaisen";
        private static void Say(string s) => Messages.Message("[Jujutsu dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        [DebugAction(Cat, "Inumaki: fire his join incident", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Incident()
        {
            var parms = StorytellerUtility.DefaultParmsNow(SpeechDefOf.JJK_InumakiJoins.category, Find.CurrentMap);
            Say(SpeechDefOf.JJK_InumakiJoins.Worker.TryExecute(parms) ? "Inumaki is on his way." : "Inumaki can't come (already arrived, or dead).");
        }

        [DebugAction(Cat, "Inumaki: spawn here (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn()
        {
            var tr = YutaTracker.Instance;
            var p = Inumaki.Make();
            if (tr == null || p == null || p.Dead || p.Spawned) { Say("Inumaki is already here, or dead."); return; }
            tr.inumakiArrived = true;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
        }

        [DebugAction(Cat, "Cursed Speech: fill throat strain (loses voice)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Fill(Pawn p)
        {
            var h = p.health.hediffSet.GetFirstHediffOfDef(SpeechDefOf.JJK_ThroatStrain) ?? p.health.AddHediff(SpeechDefOf.JJK_ThroatStrain);
            h.Severity = 1f;
            if (!p.health.hediffSet.HasHediff(SpeechDefOf.JJK_VoiceLost)) p.health.AddHediff(SpeechDefOf.JJK_VoiceLost);
        }

        [DebugAction(Cat, "Cursed Speech: clear throat strain", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Clear(Pawn p)
        {
            foreach (var def in new[] { SpeechDefOf.JJK_ThroatStrain, SpeechDefOf.JJK_VoiceLost })
            {
                var h = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null) p.health.RemoveHediff(h);
            }
        }

        [DebugAction(Cat, "Cursed Speech: show strength ratio (Inumaki/Yuta vs pawn)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ratio(Pawn p)
        {
            var tr = YutaTracker.Instance;
            foreach (var s in new[] { tr?.inumaki, tr?.yuta })
                if (s != null) Say($"{s.LabelShort} vs {p.LabelShort}: speaker {CursedSpeech.SpeakerStrength(s):0.00}, target {CursedSpeech.TargetStrength(p):0.00}, ratio {CursedSpeech.Ratio(s, p):0.00}");
        }
    }
}
