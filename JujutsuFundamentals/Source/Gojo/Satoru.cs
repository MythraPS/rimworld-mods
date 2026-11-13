using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;

namespace GojoPsycasts
{
    /// <summary>Satoru Gojo: one per game. Made here, delivered by IncidentWorker_GojoArrives.</summary>
    public static class Satoru
    {
        public const int StartingPsycastLevel = 20;   // her call 2026-10-28: about Sukuna's level at full strength

        /// <summary>Young Gojo (her ask 2026-11-06): 17, psycast level 8, with Infinity, Blue (and Pull),
        /// Blue Fist, Speed, Black Flash and Cursed Energy Reinforcement. No RCT, Red, Teleport, Purple or
        /// domain yet: those he unlocks as he levels. Leftover points go into psycaster stats like any character's
        /// (her call 2026-11-11: no character arrives with points to spend).</summary>
        public const int YoungPsycastLevel = 8;
        public const int YoungAge = 17;

        /// <summary>One Gojo per game, young or adult.</summary>
        public static bool Exists() =>
            PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Any(IsGojo)
            || IsGojo(JujutsuFundamentals.PrisonRealmQuest.Instance?.HeldRealm?.Prisoner);   // sealed, with the captors (on no map)

        public static bool IsGojo(Pawn p) => p != null && (p.kindDef == GojoDefOf.Gojo_Satoru || (GojoDefOf.Gojo_YoungSatoru != null && p.kindDef == GojoDefOf.Gojo_YoungSatoru));
        public static bool IsYoung(Pawn p) => p != null && GojoDefOf.Gojo_YoungSatoru != null && p.kindDef == GojoDefOf.Gojo_YoungSatoru;

        /// <summary>Who the arrival event brings (mod setting, default young).</summary>
        public static bool YoungArrives => JujutsuFundamentals.JujutsuMod.Settings?.youngGojo ?? true;

        /// <summary>His pale skin (the same light tone as Yuji's).</summary>
        public static readonly Color Skin = new Color(0.96f, 0.85f, 0.74f);

        /// <summary>Gives a Gojo from an older save his pale skin, unless his skin was already set on
        /// purpose (Character Editor and the like set the same override, and those are left alone).</summary>
        public static void PaleSkin(Pawn p)
        {
            if (p?.story == null || p.story.skinColorOverride != null) return;
            p.story.skinColorOverride = Skin;
            p.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public static Pawn Make() => Make(false);

        public static Pawn Make(bool young)
        {
            float age = young ? YoungAge : 28f;
            var req = new PawnGenerationRequest(
                young ? GojoDefOf.Gojo_YoungSatoru : GojoDefOf.Gojo_Satoru, faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true, allowAddictions: false,
                fixedBiologicalAge: age, fixedChronologicalAge: age,
                fixedGender: Gender.Male,
                forcedXenotype: XenotypeDefOf.Baseliner,
                developmentalStages: DevelopmentalStage.Adult);
            var p = PawnGenerator.GeneratePawn(req);

            p.Name = new NameTriple("Satoru", "Gojo", "Gojo");
            // the good guys' shared ideoligion (her call 2026-11-11, replacing Individualist: he wants to raise the next
            // generation of strong sorcerers)
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", FactionDefOf.PlayerColony, "Collectivist"));
            p.story.Childhood = GojoDefOf.Gojo_Childhood;
            p.story.Adulthood = young ? null : GojoDefOf.Gojo_Adulthood;   // a student has no adult life yet
            p.story.bodyType = BodyTypeDefOf.Thin;
            p.story.hairDef = GojoDefOf.Gojo_Hair;
            p.story.HairColor = new Color(0.96f, 0.97f, 1f);
            p.story.skinColorOverride = Skin;   // pale, not left to the random skin genes (her note 2026-11-07)
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = TattooDefOf.NoTattoo_Face;   // the eyes come from the Six Eyes hediff now
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }

            p.story.traits.allTraits.Clear();
            p.story.traits.GainTrait(new Trait(GojoDefOf.Gojo_SixEyes, 0, true));
            var brawler = DefDatabase<TraitDef>.GetNamedSilentFail("Brawler");
            if (brawler != null) p.story.traits.GainTrait(new Trait(brawler, 0, true));
            // jogger and tough are added in FullStrength

            foreach (var sk in p.skills.skills) { sk.Level = Mathf.Min(sk.Level, 6); sk.passion = Passion.None; }
            SetSkill(p, SkillDefOf.Melee, young ? 12 : 18, Passion.Major);
            SetSkill(p, SkillDefOf.Intellectual, young ? 7 : 10, Passion.Minor);
            SetSkill(p, SkillDefOf.Social, young ? 6 : 10, Passion.Minor);

            Dress(p);
            p.Notify_DisabledWorkTypesChanged();

            if (young) { Young(p); SpendPoints(p, keepLeftover: false); }   // no points left either (her call 2026-11-11)
            else { FullStrength(p); SpendPoints(p); }
            p.psychicEntropy?.OffsetPsyfocusDirectly(1f);
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            return p;
        }

        /// <summary>Level 20, every Limitless technique learned, Void and Science
        /// focus, jogger and tough. Safe to run on a Gojo from an older save.</summary>
        public static void FullStrength(Pawn p)
        {
            Gojo.Awaken(p);
            var psy = Gojo.Psycasts(p, false);
            if (psy != null)
            {
                if (psy.level < StartingPsycastLevel) psy.ChangeLevel(StartingPsycastLevel - psy.level);
                foreach (var name in new[] { "Void", "VPE_Science" })
                {
                    var f = DefDatabase<MeditationFocusDef>.GetNamedSilentFail(name);
                    if (f != null && !psy.unlockedMeditationFoci.Contains(f)) psy.UnlockMeditationFocus(f);
                }
            }
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (comp != null)
                foreach (var f in typeof(GojoAbilityDefOf).GetFields())
                    if (f.GetValue(null) is VEF.Abilities.AbilityDef def && !comp.HasAbility(def)
                        && !JujutsuFundamentals.SharedMigration.Moved.ContainsKey(def.defName))   // the three now shared (2026-11-10)
                        comp.GiveAbility(def);
            // all of Cursed Energy Fundamentals, and Simple Domain (her call 2026-11-10)
            JujutsuFundamentals.Jujutsu.LearnAllFundamentals(p);
            JujutsuFundamentals.Jujutsu.LearnUpToSimpleDomain(p);
            SetTrait(p, "SpeedOffset", 2);   // jogger (fast walker is degree 1 of the same trait)
            SetTrait(p, "Tough", 0);
            SetTrait(p, "Gojo_Selfish", 0);   // her call 2026-11-01
            SetTrait(p, "PsychicSensitivity", 2);   // psychically hypersensitive (her call 2026-11-07)
        }

        /// <summary>Young Gojo's start: level 8, the Void focus, the techniques he has as a student.</summary>
        public static void Young(Pawn p)
        {
            Gojo.Awaken(p);
            var psy = Gojo.Psycasts(p, false);
            if (psy != null)
            {
                if (psy.level < YoungPsycastLevel) psy.ChangeLevel(YoungPsycastLevel - psy.level);
                var f = DefDatabase<MeditationFocusDef>.GetNamedSilentFail("Void");
                if (f != null && !psy.unlockedMeditationFoci.Contains(f)) psy.UnlockMeditationFocus(f);
            }
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (comp != null)
                foreach (var def in new[] { GojoAbilityDefOf.Gojo_Infinity, GojoAbilityDefOf.Gojo_Blue, GojoAbilityDefOf.Gojo_BlueFist,
                                            GojoAbilityDefOf.Gojo_Speed })
                    if (def != null && !comp.HasAbility(def)) comp.GiveAbility(def);
            // his student-days Black Flash and Reinforcement are the shared ones now (2026-11-10)
            JujutsuFundamentals.Jujutsu.Learn(p, JujutsuFundamentals.JJKDefOf.JJK_CursedEnergyFundamentals,
                JujutsuFundamentals.JJKAbilityDefOf.JJK_Reinforcement, JujutsuFundamentals.JJKAbilityDefOf.JJK_BlackFlash);
            SetTrait(p, "SpeedOffset", 2);
            SetTrait(p, "Tough", 0);
            SetTrait(p, "Gojo_Selfish", 0);
            SetTrait(p, "PsychicSensitivity", 2);   // psychically hypersensitive (her call 2026-11-07)
        }

        public static void MakeHypersensitive(Pawn p) => SetTrait(p, "PsychicSensitivity", 2);

        public static bool Hypersensitive(Pawn p)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
            return def == null || p.story?.traits?.GetTrait(def)?.Degree == 2;
        }

        private static void SetTrait(Pawn p, string defName, int degree)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            var traits = p.story?.traits;
            if (def == null || traits == null) return;
            var have = traits.GetTrait(def);
            if (have != null && have.Degree == degree) return;
            if (have != null) traits.RemoveTrait(have);
            traits.GainTrait(new Trait(def, degree, true));
        }

        private static void SetSkill(Pawn p, SkillDef def, int level, Passion passion)
        {
            var s = p.skills.GetSkill(def);
            if (s == null) return;
            s.Level = level;
            s.passion = passion;
        }

        /// <summary>The high-collared black uniform: vanilla shirt, trousers and
        /// jacket, dyed. No weapon.</summary>
        public static void Dress(Pawn p)
        {
            p.apparel?.DestroyAll();
            p.equipment?.DestroyAllEquipment();
            var black = new Color(0.09f, 0.09f, 0.12f);
            Wear(p, "Apparel_BasicShirt", black);
            Wear(p, "Apparel_Pants", black);
            Wear(p, "Apparel_Jacket", black);
            if (IsYoung(p)) WearSunglasses(p);   // her call 2026-11-07
            else WearBlindfold(p);
        }

        /// <summary>Young Gojo's round sunglasses, on the same eye layer as the blindfold.</summary>
        public static void WearSunglasses(Pawn p)
        {
            var def = GojoDefOf.Gojo_SunglassesApparel;
            if (def == null || p.apparel == null || p.apparel.WornApparel.Any(a => a.def == def)) return;
            p.apparel.Wear((Apparel)ThingMaker.MakeThing(def), dropReplacedApparel: false);
        }

        /// <summary>His blindfold, as ordinary clothing on the eye layer.</summary>
        public static void WearBlindfold(Pawn p)
        {
            var def = GojoDefOf.Gojo_BlindfoldApparel;
            if (def == null || p.apparel == null || p.apparel.WornApparel.Any(a => a.def == def)) return;
            p.apparel.Wear((Apparel)ThingMaker.MakeThing(def), dropReplacedApparel: false);
        }

        /// <summary>A new Gojo arrives with no points left (her call 2026-10-31): the
        /// path and each technique he was given cost one point, as in the Psycasts
        /// tab, and whatever is left goes into psycaster stats, as the Upgrade button
        /// does (SpentPoints + ImproveStats).</summary>
        private static void SpendPoints(Pawn p) => SpendPoints(p, false);

        /// <summary>keepLeftover: young Gojo keeps the points he hasn't used, to unlock the rest himself.</summary>
        private static void SpendPoints(Pawn p, bool keepLeftover)
        {
            var psy = Gojo.Psycasts(p, false);
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (psy == null) return;
            int cost = psy.unlockedPaths.Contains(GojoDefOf.Gojo_Path) ? 1 : 0;
            if (comp != null)
                foreach (var f in typeof(GojoAbilityDefOf).GetFields())
                    if (f.GetValue(null) is VEF.Abilities.AbilityDef def && comp.HasAbility(def)) cost++;
            psy.SpentPoints(Mathf.Min(cost, Mathf.Max(psy.points, 0)));
            if (!keepLeftover && psy.points > 0) { int rest = psy.points; psy.SpentPoints(rest); psy.ImproveStats(rest); }
        }

        private static void Wear(Pawn p, string defName, Color color)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || p.apparel == null) return;
            var a = (Apparel)ThingMaker.MakeThing(def, def.MadeFromStuff ? (DefDatabase<ThingDef>.GetNamedSilentFail("Synthread") ?? ThingDefOf.Cloth) : null);
            a.TryGetComp<CompColorable>()?.SetColor(color);
            p.apparel.Wear(a, dropReplacedApparel: false);
        }
    }

    public class IncidentWorker_GojoArrives : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => !Satoru.Exists();

        /// <summary>He asks to join (her call 2026-11-11: a real choice). He's made only if welcomed.</summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            bool young = Satoru.YoungArrives;
            string text = young
                ? "A tall teenager in a black school uniform, with white hair and small round sunglasses, is at the edge of the colony "
                  + "as if he owned the place. He introduces himself as Satoru Gojo, announces that he's going to be the strongest, and asks to stay.\n\n"
                  + "He has the Six Eyes, and with them the Limitless technique: Infinity keeps everything from touching him, and he can already "
                  + "call up Blue. Red, reverse cursed technique and his domain are still beyond him. Level him up and he'll get there."
                : "A tall man in black, with white hair and a blindfold he doesn't seem to need, is at the edge of the colony as if he had been invited. "
                  + "He introduces himself as Satoru Gojo, the strongest sorcerer alive, and asks to stay for a while.\n\n"
                  + "He has the Six Eyes, and with them the Limitless technique: Infinity keeps everything from touching him. "
                  + "He fights barehanded and has never needed a gun.";
            JujutsuFundamentals.JoinOffer.Send(JujutsuFundamentals.JoinOffer.Gojo, map, "Satoru Gojo asks to join",
                text + "\n\nWelcome him and he walks in and joins. Refuse, and he may come by again another time.", def.letterDef);
            return true;
        }

        /// <summary>Welcomed: he's made (young or adult, from the setting) and walks in as a colonist.</summary>
        public static void Arrive(Map map)
        {
            if (map == null || Satoru.Exists()) return;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Friendly)) cell = CellFinder.RandomEdgeCell(map);
            var p = Satoru.Make(Satoru.YoungArrives);
            GenSpawn.Spawn(p, cell, map);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("Satoru Gojo joins", "Satoru Gojo walks in and joins your colony.", LetterDefOf.PositiveEvent, p);
        }
    }
}
