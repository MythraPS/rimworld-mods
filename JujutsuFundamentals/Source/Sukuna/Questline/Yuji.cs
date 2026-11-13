using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    public enum YujiStage { Unmet, Met, Joined, Gone }

    /// <summary>
    /// Yuji Itadori: one unique pawn per world. He has no faction and does not visit
    /// in person until he joins; he turns up in letters, asks things of the colony,
    /// and keeps score. Once his regard reaches <see cref="JoinAt"/> he offers to
    /// join. Anything he hears about (executions, organ harvesting, charity given or
    /// refused) moves the score, whether or not he has been met yet.
    /// </summary>
    public static class Yuji
    {
        public const float JoinAt = 50f;
        public const float Floor = -100f, Ceiling = 100f;

        public static bool Is(Pawn p) =>
            p?.health?.hediffSet?.HasHediff(SukunaQuestDefOf.Sukuna_YujiPhysique) == true;

        public static string RegardWord(float r)
        {
            if (r >= JoinAt) return "trusts you";
            if (r >= 15f) return "is warming to you";
            if (r > -15f) return "is unsure of you";
            if (r > -50f) return "is wary of you";
            return "is disgusted by you";
        }

        /// <summary>Make him, once. Kept as a world pawn forever until he joins.</summary>
        public static Pawn Make()
        {
            var req = new PawnGenerationRequest(
                SukunaQuestDefOf.Sukuna_Yuji, faction: null,
                context: PawnGenerationContext.NonPlayer,
                forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true, allowAddictions: false,
                fixedBiologicalAge: 18f, fixedChronologicalAge: 18f,
                fixedGender: Gender.Male, forceNoIdeo: false,
                forcedXenotype: XenotypeDefOf.Baseliner,
                developmentalStages: DevelopmentalStage.Adult);
            var p = PawnGenerator.GeneratePawn(req);

            p.Name = new NameTriple("Yuji", "Yuji", "Itadori");
            // his own ideoligion (her pick 2026-11-04): Collectivist and Guilty
            CharacterIdeos.Give(p, CharacterIdeos.Get("yuji", FactionDefOf.PlayerColony, "Collectivist", "Guilty"));
            p.story.Childhood = SukunaQuestDefOf.Sukuna_YujiChildhood;
            p.story.Adulthood = SukunaQuestDefOf.Sukuna_YujiAdulthood;
            // Fifteen years since he was born, eighteen years of body (her call 2026-11-02).
            p.ageTracker.BirthAbsTicks = GenTicks.TicksAbs - (long)ChronologicalYears * GenDate.TicksPerYear;
            ApplyLook(p);

            // Kind, hard to break, quick. The trait list replaces whatever rolled.
            p.story.traits.allTraits.Clear();
            TryTrait(p, "Kind", 0);
            TryTrait(p, "Tough", 0);
            TryTrait(p, "SpeedOffset", 2);   // jogger
            TryTrait(p, "Sukuna_PoisonBlood", 0);
            Hypersensitive.Ensure(p);

            foreach (var sk in p.skills.skills)
            {
                sk.Level = Mathf.Min(sk.Level, 6);
                sk.passion = Passion.None;
            }
            SetSkill(p, SkillDefOf.Melee, 14, Passion.Major);
            SetSkill(p, SkillDefOf.Cooking, 7, Passion.Minor);
            SetSkill(p, SkillDefOf.Social, 6, Passion.Minor);

            p.health.AddHediff(SukunaQuestDefOf.Sukuna_YujiPhysique);
            // No fingers, no cursed energy, no psycasts when you meet him (her call 2026-11-10, replacing the 11-02
            // "one finger already"): Heavenly Restriction until his first finger bypasses it. The superhuman body stays.
            TryTrait(p, "JJK_HeavenlyRestriction", 0);
            JujutsuFundamentals.ZeroCursedEnergy.Sync(p);
            // Kenjaku sealed one finger inside him at birth, and it counts as one of the twenty (her call 2026-11-10):
            // he is a one-finger vessel from the start, with no psylink and nothing learned from it.
            var vessel = p.health.AddHediff(SukunaDefOf.Sukuna_Vessel);
            vessel.Severity = 1f;
            FrozenResistance.Record(p);
            Dress(p);
            p.Notify_DisabledWorkTypesChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            return p;
        }

        public const int ChronologicalYears = 15;
        public static readonly Color PinkHair = new Color(1f, 0.56f, 0.72f);
        public static readonly Color Skin = new Color(0.96f, 0.85f, 0.74f);

        /// <summary>Her note 2026-11-07: Yuji a bit slimmer. The average male body, never Hulk or Fat.</summary>
        public static void Slim(Pawn p)
        {
            if (p?.story == null) return;
            var b = p.story.bodyType;
            if (b == BodyTypeDefOf.Hulk || b == BodyTypeDefOf.Fat || b == BodyTypeDefOf.Female)
            {
                p.story.bodyType = BodyTypeDefOf.Male;
                p.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }

        /// <summary>Pink spikes, light skin, the marks under his eyes. Hair-colour genes go, so nothing
        /// repaints the hair. Modulo Yuji keeps his white hair and scars.</summary>
        public static void ApplyLook(Pawn p)
        {
            if (p?.story == null) return;
            bool modulo = ModuloYuji.Is(p);
            if (p.genes != null)
                foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null).ToList())
                    p.genes.RemoveGene(g);
            p.story.skinColorOverride = Skin;
            Slim(p);
            // Modulo Yuji keeps the Itadori spikes (her call 2026-11-07); saves from 11-06 had the combed hair
            if (modulo && SukunaQuestDefOf.Sukuna_ModuloHair != null && p.story.hairDef == SukunaQuestDefOf.Sukuna_ModuloHair)
                p.story.hairDef = SukunaQuestDefOf.Sukuna_YujiHair;
            if (!modulo)
            {
                p.story.hairDef = SukunaQuestDefOf.Sukuna_YujiHair;
                p.story.HairColor = PinkHair;
            }
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                // the marks under his eyes come with his first finger (her call 2026-11-10)
                if (!modulo) p.style.FaceTattoo = !JujutsuFundamentals.ZeroCursedEnergy.Is(p) && Scaling.FingersEaten(p) >= 1 ? SukunaQuestDefOf.Sukuna_YujiMarks : TattooDefOf.NoTattoo_Face;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            if (p.Spawned) PortraitsCache.SetDirty(p);
        }

        private static void TryTrait(Pawn p, string defName, int degree)
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

        /// <summary>His school uniform (2026-11-06: his own closed coat with the red hood as a scarf;
        /// it was a dyed vanilla jacket), navy trousers, a red shirt under it.</summary>
        public static void Dress(Pawn p)
        {
            p.apparel?.DestroyAll();
            p.equipment?.DestroyAllEquipment();
            var navy = new Color(0.13f, 0.15f, 0.26f);
            Wear(p, "Apparel_BasicShirt", new Color(0.82f, 0.18f, 0.2f));
            Wear(p, "Apparel_Pants", navy);
            if (ModuloYuji.Is(p) && DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_ModuloHoodie") != null)
                Wear(p, "Sukuna_ModuloHoodie", Hoodie);   // adult Yuji wears a hoodie, not the uniform (her note 2026-11-07)
            else if (DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_YujiUniform") != null) Wear(p, "Sukuna_YujiUniform", Color.white);
            else Wear(p, "Apparel_Jacket", navy);
        }

        public static readonly Color Hoodie = new Color(0.25f, 0.26f, 0.29f);   // charcoal

        /// <summary>A Modulo Yuji from before 2026-11-07 still wears the school uniform: swap it for the
        /// hoodie, once per save (QuestlineTracker.moduloHoodie), so dressing him otherwise later sticks.</summary>
        public static void UniformToHoodie(Pawn p)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("Sukuna_ModuloHoodie");
            var uni = p?.apparel?.WornApparel.FirstOrDefault(a => a.def.defName == "Sukuna_YujiUniform");
            if (def == null || uni == null) return;
            p.apparel.Remove(uni); uni.Destroy();
            Wear(p, "Sukuna_ModuloHoodie", Hoodie);
        }

        internal static void Wear(Pawn p, string defName, Color color)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || p.apparel == null) return;
            var stuff = def.MadeFromStuff ? (ThingDefOf.Cloth) : null;
            var a = (Apparel)ThingMaker.MakeThing(def, stuff);
            a.TryGetComp<CompColorable>()?.SetColor(color);
            p.apparel.Wear(a, dropReplacedApparel: false);
        }
    }

    /// <summary>Questline state and pacing. RimWorld creates one per game.</summary>
    public class QuestlineTracker : GameComponent
    {
        public static QuestlineTracker Instance => Current.Game?.GetComponent<QuestlineTracker>();

        public Pawn yuji;
        public float regard;
        public YujiStage stage = YujiStage.Unmet;
        public int nextYujiTick = -1;
        public int nextFingerTick = -1;
        public int dilemmaIndex;
        public List<string> history = new List<string>();
        public bool moduloHoodie;

        // Pacing (in-game days). Yuji shows up every week or so until he joins;
        // a finger surfaces every month or so (a season is 15 days).
        public static readonly FloatRange FirstYujiDays = new FloatRange(4f, 6f);
        public static readonly FloatRange YujiEveryDays = new FloatRange(6f, 10f);
        public static readonly FloatRange FirstFingerDays = new FloatRange(10f, 14f);
        public static readonly FloatRange FingerEveryDays = new FloatRange(25f, 40f);

        public QuestlineTracker(Game game) { Transformation.FriendlyColonistsForTesting = false; }

        public Pawn GetOrMakeYuji()
        {
            if (yuji != null && !yuji.Discarded) return yuji;
            yuji = Yuji.Make();
            if (!yuji.IsWorldPawn())
                Find.WorldPawns.PassToWorld(yuji, PawnDiscardDecideMode.KeepForever);
            YujiStart.Apply(yuji, YujiStart.Setting);   // which Yuji (her call 2026-11-12)
            return yuji;
        }

        public void ChangeRegard(float delta, string reason)
        {
            if (stage == YujiStage.Joined || stage == YujiStage.Gone || Mathf.Approximately(delta, 0f)) return;
            float before = regard;
            regard = Mathf.Clamp(regard + delta, Yuji.Floor, Yuji.Ceiling);
            history.Add($"{reason} ({delta:+0;-0})");
            if (history.Count > 30) history.RemoveAt(0);

            // Before they have met, he only hears rumours: no message, the score still moves.
            if (stage != YujiStage.Met) return;
            Messages.Message(
                $"Yuji heard about it: {reason} ({delta:+0;-0}). He {Yuji.RegardWord(regard)}.",
                delta > 0 ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent, false);
            if (before < Yuji.JoinAt && regard >= Yuji.JoinAt)
                nextYujiTick = Mathf.Min(nextYujiTick, Find.TickManager.TicksGame + GenDate.TicksPerDay);
        }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % GenTicks.TickLongInterval != 0) return;

            if (nextYujiTick < 0) nextYujiTick = now + Days(FirstYujiDays);
            if (nextFingerTick < 0) nextFingerTick = now + Mathf.RoundToInt(Days(FirstFingerDays) * RelicPace.Interval);

            if (yuji != null && yuji.Dead && stage != YujiStage.Gone) stage = YujiStage.Gone;

            var map = Find.AnyPlayerHomeMap;
            if (map == null) return;

            if (stage != YujiStage.Joined && stage != YujiStage.Gone && now >= nextYujiTick)
            {
                nextYujiTick = now + Days(YujiEveryDays);
                Fire(SukunaQuestDefOf.Sukuna_YujiEncounter, map);
            }

            if (now >= nextFingerTick)
            {
                // half again as often while Sukuna is loose and hunting them (her call 2026-11-11)
                nextFingerTick = now + Mathf.RoundToInt(Days(FingerEveryDays) * RelicPace.Interval / (SukunaHunt.Loose() ? SukunaHunt.RumourFaster : 1f));
                if (FingerCensus.Remaining() > 0) Fire(SukunaQuestDefOf.Sukuna_FingerRumour, map);
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            if (yuji != null && !yuji.Dead && !yuji.Discarded && !SukunaBehaviour.IsSukuna(yuji)) Yuji.ApplyLook(yuji);
            // saves from before 2026-11-06: Yuji's blood becomes a trait
            if (yuji != null && !yuji.Dead && !yuji.Discarded) { PoisonBlood.EnsureTrait(yuji); Hypersensitive.Ensure(yuji); }
            if (!moduloHoodie)
            {
                moduloHoodie = true;
                foreach (var m in PawnsFinder.AllMapsWorldAndTemporary_Alive.Where(ModuloYuji.Is).ToList()) Yuji.UniformToHoodie(m);
            }
        }

        private static int Days(FloatRange r) => Mathf.RoundToInt(r.RandomInRange * GenDate.TicksPerDay);

        private static void Fire(IncidentDef def, Map map)
        {
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            def.Worker.TryExecute(parms);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref yuji, "sukunaYuji");
            Scribe_Values.Look(ref regard, "sukunaYujiRegard", 0f);
            Scribe_Values.Look(ref stage, "sukunaYujiStage", YujiStage.Unmet);
            Scribe_Values.Look(ref nextYujiTick, "sukunaNextYujiTick", -1);
            Scribe_Values.Look(ref nextFingerTick, "sukunaNextFingerTick", -1);
            Scribe_Values.Look(ref dilemmaIndex, "sukunaDilemmaIndex", 0);
            Scribe_Values.Look(ref moduloHoodie, "sukunaModuloHoodie", false);
            Scribe_Values.Look(ref Transformation.FriendlyColonistsForTesting, "sukunaFriendlyTest", false);
            Scribe_Collections.Look(ref history, "sukunaYujiHistory", LookMode.Value);
            if (history == null) history = new List<string>();
        }
    }
}
