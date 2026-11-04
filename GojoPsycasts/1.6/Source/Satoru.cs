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

        public static bool Exists() =>
            PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Any(p => p.kindDef == GojoDefOf.Gojo_Satoru);

        public static Pawn Make()
        {
            var req = new PawnGenerationRequest(
                GojoDefOf.Gojo_Satoru, faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true, allowAddictions: false,
                fixedBiologicalAge: 28f, fixedChronologicalAge: 28f,
                fixedGender: Gender.Male,
                forcedXenotype: XenotypeDefOf.Baseliner,
                developmentalStages: DevelopmentalStage.Adult);
            var p = PawnGenerator.GeneratePawn(req);

            p.Name = new NameTriple("Satoru", "Gojo", "Gojo");
            p.story.Childhood = GojoDefOf.Gojo_Childhood;
            p.story.Adulthood = GojoDefOf.Gojo_Adulthood;
            p.story.bodyType = BodyTypeDefOf.Thin;
            p.story.hairDef = GojoDefOf.Gojo_Hair;
            p.story.HairColor = new Color(0.96f, 0.97f, 1f);
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
            SetSkill(p, SkillDefOf.Melee, 18, Passion.Major);
            SetSkill(p, SkillDefOf.Intellectual, 10, Passion.Minor);
            SetSkill(p, SkillDefOf.Social, 10, Passion.Minor);

            Dress(p);
            p.Notify_DisabledWorkTypesChanged();

            FullStrength(p);
            SpendPoints(p);
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
                    if (f.GetValue(null) is VEF.Abilities.AbilityDef def && !comp.HasAbility(def)) comp.GiveAbility(def);
            SetTrait(p, "SpeedOffset", 2);   // jogger (fast walker is degree 1 of the same trait)
            SetTrait(p, "Tough", 0);
            SetTrait(p, "Gojo_Selfish", 0);   // her call 2026-11-01
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
            WearBlindfold(p);
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
        private static void SpendPoints(Pawn p)
        {
            var psy = Gojo.Psycasts(p, false);
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (psy == null) return;
            int cost = psy.unlockedPaths.Contains(GojoDefOf.Gojo_Path) ? 1 : 0;
            if (comp != null)
                foreach (var f in typeof(GojoAbilityDefOf).GetFields())
                    if (f.GetValue(null) is VEF.Abilities.AbilityDef def && comp.HasAbility(def)) cost++;
            psy.SpentPoints(Mathf.Min(cost, Mathf.Max(psy.points, 0)));
            if (psy.points > 0) { int rest = psy.points; psy.SpentPoints(rest); psy.ImproveStats(rest); }
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

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Friendly)) return false;
            var p = Satoru.Make();
            GenSpawn.Spawn(p, cell, map);
            SendStandardLetter("Satoru Gojo arrives",
                "A tall man in black, with white hair and a blindfold he doesn't seem to need, has walked into the colony as if he had been invited. " +
                "He introduces himself as Satoru Gojo, the strongest sorcerer alive, and says he'll stay for a while.\n\n" +
                "He has the Six Eyes, and with them the Limitless technique: Infinity keeps everything from touching him. " +
                "He fights barehanded and has never needed a gun.",
                LetterDefOf.PositiveEvent, parms, p);
            return true;
        }
    }
}
