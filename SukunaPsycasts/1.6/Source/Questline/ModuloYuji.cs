using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;

namespace SukunaPsycasts
{
    /// <summary>
    /// Yuji Itadori as he is in JJK Modulo, decades later (her call 2026-11-01): white
    /// hair, more scars, two fingers gone from his left hand. Every Shrine technique and
    /// the whole Hemosage path are his own, with 60 psycaster stat upgrades on top. Kind,
    /// never selfish. He arrives once per game and joins. He counts as Yuji for
    /// <see cref="Yuji.Is"/>, so a cursed finger never turns him into Sukuna.
    /// </summary>
    public static class ModuloYuji
    {
        public const int PsycastLevel = 20;      // the same as Gojo and a full-strength Sukuna
        public const int StatUpgrades = 60;      // her call 2026-11-01: stronger than both

        public static readonly Color WhiteHair = new Color(0.92f, 0.92f, 0.94f);
        private static readonly string[] LostFingers = { "left ring finger", "left pinky" };

        public static bool Is(Pawn p) => p != null && (p.kindDef == SukunaQuestDefOf.Sukuna_ModuloYuji
            || p.health?.hediffSet?.HasHediff(SukunaArcDefOf.Sukuna_JujutsuPinnacle) == true);

        public static bool Exists() =>
            PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.Any(Is);

        public static Pawn Make()
        {
            var req = new PawnGenerationRequest(
                SukunaQuestDefOf.Sukuna_ModuloYuji, faction: Faction.OfPlayer,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true, allowAddictions: false,
                fixedBiologicalAge: 24f, fixedChronologicalAge: 83f,
                fixedGender: Gender.Male, forceNoIdeo: false,
                forcedXenotype: XenotypeDefOf.Baseliner,
                developmentalStages: DevelopmentalStage.Adult);
            var p = PawnGenerator.GeneratePawn(req);

            p.Name = new NameTriple("Yuji", "Yuji", "Itadori");
            // his own ideoligion (her pick 2026-11-04): Collectivist and Guilty
            CharacterIdeos.Give(p, CharacterIdeos.Get("yuji", FactionDefOf.PlayerColony, "Collectivist", "Guilty"));
            p.story.Childhood = SukunaQuestDefOf.Sukuna_YujiChildhood;
            p.story.Adulthood = SukunaQuestDefOf.Sukuna_ModuloAdulthood;
            p.story.hairDef = SukunaQuestDefOf.Sukuna_YujiHair;   // Itadori spikes, white (her call 2026-11-07: the combed hair was ugly)
            p.story.HairColor = WhiteHair;
            p.story.skinColorOverride = Yuji.Skin;
            Yuji.Slim(p);
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = SukunaQuestDefOf.Sukuna_ModuloScars;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }

            p.story.traits.allTraits.Clear();
            SetTrait(p, "Kind", 0);
            SetTrait(p, "Tough", 0);
            SetTrait(p, "SpeedOffset", 2);   // jogger
            SetTrait(p, "Sukuna_PoisonBlood", 0);
            Hypersensitive.Ensure(p);

            foreach (var sk in p.skills.skills) { sk.Level = Mathf.Min(sk.Level, 8); sk.passion = Passion.None; }
            SetSkill(p, SkillDefOf.Melee, 20, Passion.Major);
            SetSkill(p, SkillDefOf.Cooking, 10, Passion.Minor);
            SetSkill(p, SkillDefOf.Social, 10, Passion.Minor);
            SetSkill(p, SkillDefOf.Medicine, 8, Passion.None);

            p.health.AddHediff(SukunaQuestDefOf.Sukuna_YujiPhysique);
            RemoveFingers(p);

            // Hemosage only opens for the hemogenic. Blood manipulation came to him from his
            // brother, so the gene is part of who he is (endogene).
            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (ModsConfig.BiotechActive && hemogenic != null && p.genes != null && !p.genes.HasActiveGene(hemogenic))
                p.genes.AddGene(hemogenic, false);

            Yuji.Dress(p);
            p.Notify_DisabledWorkTypesChanged();
            FullStrength(p);
            p.psychicEntropy?.OffsetPsyfocusDirectly(1f);
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            return p;
        }

        private static void RemoveFingers(Pawn p)
        {
            foreach (var label in LostFingers) RemoveFinger(p, label);
        }

        /// <summary>An old, healed loss: no bleeding, no pain spike.</summary>
        public static void RemoveFinger(Pawn p, string label)
        {
            var part = p?.RaceProps?.body?.AllParts?.FirstOrDefault(x => x.customLabel == label);
            if (part == null || p.health.hediffSet.PartIsMissing(part)) return;
            var missing = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, p, part);
            missing.IsFresh = false;
            missing.lastInjury = HediffDefOf.Cut;
            p.health.AddHediff(missing, part);
        }

        /// <summary>The questline's ending: Yuji beat Sukuna and takes his Modulo form. White hair, the
        /// scars, two fingers gone, the Modulo backstory, psycaster stat upgrades topped up to 60 (each
        /// one a psycast level he gains and spends at once), Benevolent Shrine and Piercing Blood: Scatter.</summary>
        public static void Ascend(Pawn p)
        {
            if (p == null || p.Dead) return;
            if (!p.health.hediffSet.HasHediff(SukunaArcDefOf.Sukuna_JujutsuPinnacle))
                p.health.AddHediff(SukunaArcDefOf.Sukuna_JujutsuPinnacle);
            p.story.hairDef = SukunaQuestDefOf.Sukuna_YujiHair;   // Itadori spikes, white (her call 2026-11-07; Ascend was missed then, fixed 2026-11-09)
            p.story.HairColor = WhiteHair;
            p.story.Adulthood = SukunaQuestDefOf.Sukuna_ModuloAdulthood;
            if (p.style != null) p.style.FaceTattoo = SukunaQuestDefOf.Sukuna_ModuloScars;
            RemoveFingers(p);
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);

            var psy = PsycastSetup.Ensure(p);
            // Both trees in full, as the arriving Modulo Yuji has them (her report 2026-11-09: the debug
            // option and a Sukuna killed outside the lair left them half learned). Free: no points spent.
            LearnBothTrees(p, psy);
            if (psy != null)
            {
                int have = StatUpgradesOf(psy);
                int need = StatUpgrades - have;
                if (need > 0)
                {
                    int before = psy.points;
                    psy.ChangeLevel(need);
                    int gained = Mathf.Max(0, psy.points - before);
                    if (gained > 0) psy.SpentPoints(Mathf.Min(gained, need));
                    psy.ImproveStats(need);
                }
            }
            SukunaArc.GrantBenevolent(p);
            var comp = p.GetComp<CompAbilities>();
            if (comp != null && !comp.HasAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter))
                comp.GiveAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter);
            p.Notify_DisabledWorkTypesChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(p);
        }

        private static readonly System.Reflection.FieldInfo StatPointsField =
            HarmonyLib.AccessTools.Field(typeof(Hediff_PsycastAbilities), "statPoints");

        /// <summary>Upgrades already bought (VPE keeps them in a private field).</summary>
        public static int StatUpgradesOf(Hediff_PsycastAbilities psy) =>
            psy == null || StatPointsField == null ? 0 : (int)StatPointsField.GetValue(psy);

        /// <summary>Shrine and Hemosage unlocked with every technique in them learned (Malevolent Shrine
        /// becomes his Benevolent Shrine, see YujiDomain). Hemosage needs the Hemogenic gene, so he gets it.</summary>
        private static void LearnBothTrees(Pawn p, Hediff_PsycastAbilities psy)
        {
            var comp = p.GetComp<CompAbilities>();
            if (psy == null || comp == null) return;
            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (ModsConfig.BiotechActive && hemogenic != null && p.genes != null && !p.genes.HasActiveGene(hemogenic))
                p.genes.AddGene(hemogenic, false);
            FingerProgression.GrantMorbidFocus(psy);
            foreach (var path in new[] { SukunaDefOf.Sukuna_Path, SukunaArc.Hemosage })
            {
                if (path == null) continue;
                if (!psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
                if (path.abilities == null) continue;
                foreach (var a in path.abilities)
                    if (!comp.HasAbility(a)) comp.GiveAbility(a);
            }
        }

        /// <summary>Psycast level 20, Shrine and Hemosage unlocked with every technique in
        /// them learned, nothing left to spend, then 60 stat upgrades.</summary>
        private static void FullStrength(Pawn p)
        {
            if (p.GetPsylinkLevel() < 1) p.ChangePsylinkLevel(1, false);
            var psy = PsycastSetup.Ensure(p);
            var comp = p.GetComp<CompAbilities>();
            if (psy == null || comp == null) return;
            if (psy.level < PsycastLevel) psy.ChangeLevel(PsycastLevel - psy.level);
            FingerProgression.GrantMorbidFocus(psy);

            LearnBothTrees(p, psy);

            if (psy.points > 0) psy.SpentPoints(psy.points);
            psy.ImproveStats(StatUpgrades);
            // He arrives already past the pinnacle: his own domain and his own Piercing Blood.
            SukunaArc.GrantBenevolent(p);
            if (!comp.HasAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter)) comp.GiveAbility(SukunaArcDefOf.Sukuna_PiercingBloodScatter);
            p.health.AddHediff(SukunaArcDefOf.Sukuna_JujutsuPinnacle);
        }

        private static void SetTrait(Pawn p, string defName, int degree)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def != null) p.story.traits.GainTrait(new Trait(def, degree, true));
        }

        private static void SetSkill(Pawn p, SkillDef def, int level, Passion passion)
        {
            var s = p.skills.GetSkill(def);
            if (s == null) return;
            s.Level = level;
            s.passion = passion;
        }
    }

    public class IncidentWorker_ModuloYujiArrives : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => !ModuloYuji.Exists();

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Friendly)) return false;
            var p = ModuloYuji.Make();
            GenSpawn.Spawn(p, cell, map);
            SendStandardLetter("Yuji Itadori arrives",
                "A young man with white hair and an old scarred face has walked into the colony. He says his name is Yuji Itadori, " +
                "and that he is a lot older than he looks. Two fingers are missing from his left hand.\n\n" +
                "He carries the King of Curses' slashing technique as his own, and his brother's blood manipulation with it. " +
                "He asks if he can stay and help.",
                LetterDefOf.PositiveEvent, parms, p);
            return true;
        }
    }
}
