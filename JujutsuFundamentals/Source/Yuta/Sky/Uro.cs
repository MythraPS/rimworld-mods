using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld.Planet;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Takako Uro (her calls 2026-11-11): a Heian-era sorcerer reincarnated for the Culling Game. She has only her Sky
    /// Manipulation (Flight, Redirect, Thin Ice-Breaker) and can never gain anything else (YutaCore enforces it). She
    /// arrives as an enemy, alone. Beaten (downed) and then healed, she either asks to join or turns friendly and moves on.
    /// </summary>
    public static partial class Uro
    {
        public const int PsycastLevel = 10;           // proposal
        public const float HealedHealth = 0.8f;       // proposal: "healed" = no bleeding, at least 80% health
        public const float AsksToJoinChance = 0.5f;   // proposal

        public static Pawn Make()
        {
            // Body age isn't given in canon; Ryu's precedent (her call 2026-11-10): body 28. She lived in the Heian era,
            // about a thousand years ago, so 1000 years on top of it chronologically.
            var req = new PawnGenerationRequest(PawnKindDefOf.Colonist, faction: null, context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false, mustBeCapableOfViolence: true, allowAddictions: false,
                fixedGender: Gender.Female, forcedXenotype: XenotypeDefOf.Baseliner, developmentalStages: DevelopmentalStage.Adult,
                fixedBiologicalAge: 28f, fixedChronologicalAge: 1028f);
            Pawn p;
            NoMagicClass.making = true;
            try { p = PawnGenerator.GeneratePawn(req); }
            finally { NoMagicClass.making = false; }
            PointSettle.Queue(p);   // no points left to spend once made (her call 2026-11-11)
            p.Name = new NameTriple("Takako", "Uro", "Uro");
            var t = YutaTracker.Instance;
            if (t != null) t.uro = p;   // before her abilities, or the core turns them away
            // her OK 2026-11-11: Melee 12 (major), Intellectual 8, Social 6, Shooting 4
            Characters.Skill(p, SkillDefOf.Melee, 12, Passion.Major);
            Characters.Skill(p, SkillDefOf.Intellectual, 8, Passion.Minor);
            Characters.Skill(p, SkillDefOf.Social, 6, Passion.None);
            Characters.Skill(p, SkillDefOf.Shooting, 4, Passion.None);
            Characters.Psycaster(p, PsycastLevel);
            var comp = p.GetComp<CompAbilities>();
            foreach (var n in YutaAbilities.Sky.Concat(new[] { "JJK_UroDomainExpansion" }))   // + her domain (her call 2026-11-11)
            {
                var d = YutaAbilities.Def(n);
                if (comp != null && d != null && !comp.HasAbility(d)) comp.GiveAbility(d);
            }
            p.equipment?.DestroyAllEquipment();
            UroLooks.Apply(p);
            PassiveSync.Sync(p);
            Looks.Refresh(p);
            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.KeepForever);
            return p;
        }

        /// <summary>She comes as an enemy, alone, to fight the colony (as Ryu does).</summary>
        public static bool ArriveHostile(Map map)
        {
            var t = YutaTracker.Instance;
            if (t == null || map == null) return false;
            if (t.uro == null) Make();
            var p = t.uro;
            if (p == null || p.Dead || p.Spawned) return false;
            var faction = Find.FactionManager.RandomEnemyFaction(false, false, false);
            if (faction == null) return false;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, 0f)) return false;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (p.Faction != faction) p.SetFaction(faction);
            GenSpawn.Spawn(p, cell, map);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, new List<Pawn> { p });
            t.uroArrived = true;
            var st = UroTracker.Instance;
            if (st != null) { st.beaten = false; st.resolved = false; }
            Find.LetterStack.ReceiveLetter("Takako Uro", "Takako Uro, a sorcerer from the Heian era, has come to fight your colony. She bends the sky itself: shots aimed at her may turn back on the shooter. Beat her and heal her, and she may change her mind about you.",
                                           LetterDefOf.ThreatBig, p);
            return true;
        }

        public static void Join(Pawn p)
        {
            if (p == null || p.Dead) return;
            p.GetLord()?.RemovePawn(p);      // the waiting lord, if any
            if (p.guest != null && p.guest.IsPrisoner) p.guest.SetGuestStatus(null);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("Uro joins", "Takako Uro joins your colony.", LetterDefOf.PositiveEvent, p);
        }

        /// <summary>Waiting for the colony's answer (her call 2026-11-11: she must actually stay put): a lord that keeps
        /// her near where she stands, wandering a few cells, until Join or MoveOn replaces it.</summary>
        public static void Wait(Pawn p)
        {
            if (p == null || p.Dead || !p.Spawned || p.IsPrisonerOfColony || p.Faction == null || p.Faction.IsPlayer) return;
            p.GetLord()?.RemovePawn(p);
            p.jobs?.StopAll();
            LordMaker.MakeNewLord(p.Faction, new LordJob_DefendPoint(p.Position, 4f, null, false, false), p.Map, new List<Pawn> { p });
        }

        /// <summary>Friendly now, and on her way: a non-hostile faction (if there is one) and a walk off the map.</summary>
        public static void MoveOn(Pawn p)
        {
            if (p == null || p.Dead) return;
            p.GetLord()?.RemovePawn(p);      // the waiting lord, if any
            if (p.guest != null && p.guest.IsPrisoner) p.guest.SetGuestStatus(null);
            var f = Find.FactionManager.RandomNonHostileFaction(false, false, false);
            if (f != null && p.Faction != f) p.SetFaction(f);
            if (p.Spawned && p.Faction != null && !p.Faction.IsPlayer)
            {
                p.jobs?.StopAll();
                LordMaker.MakeNewLord(p.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Walk, false, true), p.Map, new List<Pawn> { p });
            }
            Find.LetterStack.ReceiveLetter("Uro moves on", "Takako Uro holds no grudge any more. She leaves on good terms.", LetterDefOf.NeutralEvent, p);
        }
    }

    public static class UroLooks
    {
        /// <summary>Her calls 2026-11-11: long pink hair (fan sources), black sclera with pink irises, the Nudist trait
        /// and no clothes (canon: what looks like clothing is sky, made by her technique). Art: tools/yuta/art_sky_uro.py.
        /// Backstories are still Ryu's pair until she picks hers (proposals in the Sky report).</summary>
        public static void Apply(Pawn p)
        {
            if (p?.story == null) return;
            if (p.genes != null)
                foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null || g.def.skinColorOverride != null).ToList())
                    p.genes.RemoveGene(g);
            p.story.bodyType = BodyTypeDefOf.Female;
            p.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail("JJK_UroHair") ?? p.story.hairDef;
            p.story.HairColor = new Color(0.93f, 0.55f, 0.72f);
            p.story.skinColorOverride = new Color(0.96f, 0.94f, 0.92f);   // pale white (her call 2026-11-11)
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
            p.story.Childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_HeianSorcererChild") ?? p.story.Childhood;   // her OK 2026-11-11
            p.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_Sorcerer") ?? p.story.Adulthood;
            if (p.story.traits != null)
            {
                p.story.traits.allTraits.Clear();
                var nudist = DefDatabase<TraitDef>.GetNamedSilentFail("Nudist");
                if (nudist != null) p.story.traits.GainTrait(new Trait(nudist, 0, true));
                var smart = DefDatabase<TraitDef>.GetNamedSilentFail("TooSmart");   // her OK 2026-11-11: sharp intellect
                if (smart != null) p.story.traits.GainTrait(new Trait(smart, 0, true));
            }
            p.apparel?.DestroyAll();
            Eyes(p);
            // her own ideoligion (coordinator's pick 2026-11-11; she'll adjust after testing)
            CharacterIdeos.Give(p, CharacterIdeos.Get("uro", FactionDefOf.PlayerColony, "Individualist", "Nudism"));
        }

        /// <summary>Black sclera, pink irises: a hediff that carries the eye pictures (as the Six Eyes do).</summary>
        public static void Eyes(Pawn p)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("JJK_UroEyes");
            if (def != null && p?.health != null && !p.health.hediffSet.HasHediff(def)) p.health.AddHediff(def);
        }
    }

    /// <summary>Like the Six Eyes pictures: skipped under Facial Animation, whose own eyes don't line up with them.</summary>
    public class PawnRenderNodeWorker_UroEyes : PawnRenderNodeWorker_Eye
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            !GojoPsycasts.SixEyesLook.FacialAnimation && base.CanDrawNow(node, parms);
    }

    /// <summary>Uro's own state: has she been beaten, and is it settled. Also drives her Sky Manipulation while she
    /// fights as an enemy (the AI doesn't cast it on its own).</summary>
    public class UroTracker : GameComponent
    {
        public static UroTracker Instance;
        public bool beaten, resolved;
        private int nextRedirect, nextIce;

        public const int RedirectGap = 1800, IceGap = 1200;   // proposals, the same as the ability cooldowns

        public UroTracker(Game game) { Instance = this; }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 60 != 23) return;
            var p = YutaTracker.Instance?.uro;
            if (p == null || p.Dead || resolved) return;
            bool hostile = p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer) && !(p.guest?.IsPrisoner ?? false);
            if (p.Spawned && hostile && !p.Downed) Fight(p, now);
            if (!beaten)
            {
                if (p.Downed && (hostile || p.IsPrisonerOfColony)) beaten = true;
                return;
            }
            if (p.Faction == Faction.OfPlayer) { resolved = true; return; }   // recruited the ordinary way
            if (Healed(p) && (p.IsPrisonerOfColony || (p.Spawned && p.Map.IsPlayerHome))) Resolve(p);
        }

        public static bool Healed(Pawn p) =>
            !p.Dead && !p.Downed && !p.InMentalState && p.health.hediffSet.BleedRateTotal <= 0f
            && p.health.summaryHealth.SummaryHealthPercent >= Uro.HealedHealth;

        public void Resolve(Pawn p)
        {
            resolved = true;
            if (Rand.Chance(Uro.AsksToJoinChance))
            {
                // she stays put (no fighting the colony, no leaving) until the letter is answered
                if (!p.IsPrisonerOfColony && p.Faction != null && p.Faction.HostileTo(Faction.OfPlayer))
                {
                    var f = Find.FactionManager.RandomNonHostileFaction(false, false, false);
                    if (f != null) p.SetFaction(f);
                }
                Uro.Wait(p);
                var letter = (ChoiceLetter_UroAsks)LetterMaker.MakeLetter("Uro asks to join",
                    "Takako Uro was beaten and then healed by your colony. She asks to join you.", UroDefOf.JJK_UroAsks, p);
                letter.uro = p;
                Find.LetterStack.ReceiveLetter(letter);
            }
            else Uro.MoveOn(p);
        }

        /// <summary>Redirect whenever enemies are near; Thin Ice-Breaker on the nearest one when she's beside them.</summary>
        private void Fight(Pawn p, int now)
        {
            var map = p.Map;
            Pawn near = null; float best = 999f;
            foreach (var o in map.mapPawns.AllPawnsSpawned)
            {
                if (o.Dead || o.Downed || !o.HostileTo(p)) continue;
                float d = o.Position.DistanceTo(p.Position);
                if (d < best) { best = d; near = o; }
            }
            if (near == null || best > 25f) return;
            if (now >= nextRedirect && !Jujutsu.Has(p, SkyDefOf.JJK_SkyRedirect)) { SkyRedirect.Apply(p); nextRedirect = now + RedirectGap; }
            // Thin Ice-Breaker is a melee strike (her call 2026-11-11): only on someone right beside her; otherwise she
            // closes in (her normal melee AI does the walking)
            if (now >= nextIce && best <= 1.9f)
            {
                ThinIce.Shockwave(p, near.Position, ThinIce.Damage * Jujutsu.Power(p));
                nextIce = now + IceGap;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Values.Look(ref beaten, "uroBeaten");
            Scribe_Values.Look(ref resolved, "uroResolved");
        }
    }

    [DefOf]
    public static class UroDefOf
    {
        public static LetterDef JJK_UroAsks;
        public static IncidentDef JJK_UroChallenge;
        static UroDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(UroDefOf));
    }

    public class ChoiceLetter_UroAsks : ChoiceLetter
    {
        public Pawn uro;

        /// <summary>She waits in place (Uro.Wait) until this is answered; dismissing it unanswered would leave her
        /// standing there for good.</summary>
        public override bool CanDismissWithRightClick => false;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) { yield return Option_Close; yield break; }
                yield return new DiaOption("Welcome her")
                {
                    action = () => { Uro.Join(uro); Find.LetterStack.RemoveLetter(this); },
                    resolveTree = true,
                };
                yield return new DiaOption("Let her go")
                {
                    action = () => { Uro.MoveOn(uro); Find.LetterStack.RemoveLetter(this); },
                    resolveTree = true,
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref uro, "uro");
        }
    }

    public class IncidentWorker_UroChallenge : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) =>
            YutaTracker.Instance != null && !YutaTracker.Instance.uroArrived && parms.target is Map;

        protected override bool TryExecuteWorker(IncidentParms parms) => Uro.ArriveHostile((Map)parms.target);
    }

    public static class SkyDebug
    {
        private const string Cat = "Jujutsu Kaisen";
        private static void Say(string s) => Messages.Message("[Jujutsu dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        [DebugAction(Cat, "Uro: arrives as an enemy now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UroArrives()
        {
            var t = YutaTracker.Instance;
            if (t == null) return;
            t.uroArrived = false;
            Say(Uro.ArriveHostile(Find.CurrentMap) ? "Uro is here." : "Uro couldn't arrive (already on a map, dead, or no enemy faction).");
        }

        /// <summary>Testing (her ask 2026-11-11): Uro as your colonist where you click, with her arrival marked done.</summary>
        [DebugAction(Cat, "Uro: spawn here as a colonist (testing)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UroColonist()
        {
            var t = YutaTracker.Instance; var st = UroTracker.Instance;
            if (t == null) return;
            var map = Find.CurrentMap;
            var p = t.uro;
            if (p != null && p.Spawned) { Say("Uro is already on a map."); return; }
            if (p == null || p.Dead) p = Uro.Make();
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            t.uroArrived = true;
            if (st != null) { st.beaten = true; st.resolved = true; }
            var cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map))
                CellFinder.TryFindRandomCellNear(cell.ClampInsideMap(map), map, 6, c => c.Standable(map), out cell);
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, cell, map);
            Say("Uro is here as your colonist.");
        }

        [DebugAction(Cat, "Uro: beaten and healed now", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UroHealed()
        {
            var p = YutaTracker.Instance?.uro; var st = UroTracker.Instance;
            if (p == null || p.Dead || st == null) { Say("No living Uro."); return; }
            st.beaten = true;
            st.Resolve(p);
        }

        [DebugAction(Cat, "Uro: beaten and healed (force asks to join)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void UroAsks()
        {
            var p = YutaTracker.Instance?.uro; var st = UroTracker.Instance;
            if (p == null || p.Dead || st == null) { Say("No living Uro."); return; }
            st.beaten = true; st.resolved = true;
            var letter = (ChoiceLetter_UroAsks)LetterMaker.MakeLetter("Uro asks to join",
                "Takako Uro was beaten and then healed by your colony. She asks to join you.", UroDefOf.JJK_UroAsks, p);
            letter.uro = p;
            Find.LetterStack.ReceiveLetter(letter);
        }

        [DebugAction(Cat, "Pawn: learn Sky Manipulation, Dhruv and G Warstaff", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void LearnSky(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) { Say("Not a pawn with abilities."); return; }
            Characters.Psycaster(p, 1);
            foreach (var n in YutaAbilities.Sky.Concat(new[] { YutaAbilities.Dhruv, YutaAbilities.GWarstaff }))
            {
                var d = YutaAbilities.Def(n);
                if (d != null && !comp.HasAbility(d)) comp.GiveAbility(d);
            }
            Say($"{p.LabelShort}: given what they're allowed of Sky, Dhruv and G Warstaff.");
        }

        [DebugAction(Cat, "Pawn: toggle Sky flight", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ToggleFlight(Pawn p)
        {
            if (p == null) return;
            if (p.psychicEntropy != null && p.psychicEntropy.CurrentPsyfocus < 0.5f) p.psychicEntropy.OffsetPsyfocusDirectly(0.5f);
            Toggles.Set(p, SkyStartup.FlightKey, !Toggles.IsOn(p, SkyStartup.FlightKey));
        }

        [DebugAction(Cat, "Pawn: Redirect now", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Redirect(Pawn p) => SkyRedirect.Apply(p);

        [DebugAction(Cat, "Pawn: Thin Ice-Broken (armour useless)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void IceBreak(Pawn p) { if (p != null) ThinIce.Break(p, null); }
    }
}
