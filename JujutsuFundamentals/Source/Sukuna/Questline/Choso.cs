using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEFAbility = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    [DefOf]
    public static class ChosoDefOf
    {
        public static VEFAbilityDef Choso_PiercingBlood;
        public static TattooDef Choso_NoseMark;
        static ChosoDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(ChosoDefOf));
    }

    /// <summary>
    /// Choso (her ask 2026-11-11). Already on Yuji's side when he first appears. Every time Yuji (a colonist) is hurt
    /// there is a chance Choso notices: the bigger the hit, the likelier; near death, always. How hurt Yuji is then
    /// decides how soon he comes: near death, at once (walking in from the edge nearest Yuji); lightly hurt, up to a few days later. He
    /// joins, and gives Yuji the first Death Painting (paintings only start turning up after that).
    /// Numbers are proposals.
    /// </summary>
    public class ChosoTracker : GameComponent
    {
        public static ChosoTracker Instance;

        public const float DamageForCertain = 40f;          // a hit this big is always noticed
        public static readonly FloatRange DelayDays = new FloatRange(0.25f, 3f);   // badly hurt .. barely hurt

        public Pawn choso;
        public bool noticed, arrived, beside;
        public int arriveTick = -1;

        public ChosoTracker(Game game) { Instance = this; }

        public static bool Is(Pawn p) => p != null && Instance?.choso == p;
        public Pawn Choso => choso ?? (choso = Choso_.Make());

        /// <summary>Near death: downed, under a third of his health, or bleeding out within a day.</summary>
        public static bool NearDeath(Pawn y) =>
            y.Downed || y.health.summaryHealth.SummaryHealthPercent < 0.35f
            || (y.health.hediffSet.BleedRateTotal > 0.01f && HealthUtility.TicksUntilDeathDueToBloodLoss(y) < GenDate.TicksPerDay);

        public void YujiHurt(Pawn yuji, float dealt)
        {
            if (noticed || arrived || yuji?.Faction == null || !yuji.Faction.IsPlayer || yuji.Dead) return;
            bool dying = NearDeath(yuji);
            float chance = dying ? 1f : Mathf.Clamp01(dealt / DamageForCertain);
            if (!Rand.Chance(chance)) return;
            noticed = true;
            beside = dying;
            float health = Mathf.Clamp01(yuji.health.summaryHealth.SummaryHealthPercent);
            arriveTick = Find.TickManager.TicksGame + (dying ? 0 : Mathf.RoundToInt(Mathf.Lerp(DelayDays.min, DelayDays.max, health) * GenDate.TicksPerDay));
        }

        /// <summary>His clothes were cream before her call 2026-11-11 (white): recolour the old cream ones, leave dyed ones.</summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            var old = new Color(0.86f, 0.83f, 0.76f);
            var worn = choso?.apparel?.WornApparel;
            if (worn == null) return;
            foreach (var a in worn)
                if ((a.def.defName == "Choso_Outfit" || a.def.defName == "Apparel_Pants")
                    && Mathf.Abs(a.DrawColor.r - old.r) < 0.02f && Mathf.Abs(a.DrawColor.g - old.g) < 0.02f && Mathf.Abs(a.DrawColor.b - old.b) < 0.02f)
                    a.SetColor(new Color(0.97f, 0.97f, 0.97f), false);
        }

        public override void GameComponentTick()
        {
            if (!noticed || arrived || Find.TickManager.TicksGame % 60 != 0 || Find.TickManager.TicksGame < arriveTick) return;
            Arrive();
        }

        public void Arrive()
        {
            var yuji = QuestlineTracker.Instance?.yuji;
            var map = yuji != null && yuji.Spawned ? yuji.Map : Find.AnyPlayerHomeMap;
            if (map == null) return;          // try again next check
            var p = Choso;
            if (p.Dead) { arrived = true; return; }
            arrived = true;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            // He can't teleport (her call 2026-11-11): he always walks in from the map edge. Near death he comes at
            // once, from the edge nearest Yuji.
            IntVec3 cell = IntVec3.Invalid;
            if (beside && yuji != null && yuji.Spawned && yuji.Map == map)
                cell = CellRect.WholeMap(map).EdgeCells
                    .Where(c => c.Standable(map) && !c.Fogged(map) && map.reachability.CanReach(c, yuji, Verse.AI.PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)))
                    .OrderBy(c => c.DistanceToSquared(yuji.Position)).FirstOrDefault();
            if (!cell.IsValid && !RCellFinder.TryFindRandomPawnEntryCell(out cell, map, CellFinder.EdgeRoadChance_Friendly)) cell = CellFinder.RandomEdgeCell(map);
            if (!p.Spawned) GenSpawn.Spawn(p, cell, map);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Choso_.Brothers(p, yuji);

            // the first Death Painting, for Yuji
            var painting = PaintingCensus.Remaining() > 0 ? ThingMaker.MakeThing(SukunaArcDefOf.Sukuna_DeathPainting) : null;
            bool handed = painting != null && yuji != null && !yuji.Dead && yuji.Spawned && yuji.Map == map && yuji.inventory != null
                          && yuji.inventory.innerContainer.TryAdd(painting);
            if (painting != null && !handed) GenPlace.TryPlaceThing(painting, p.Position, map, ThingPlaceMode.Near);

            string how = beside ? "Yuji is close to death, and Choso has come running, entering from the edge nearest him." : "Choso felt that Yuji was hurt, and has come.";
            Find.LetterStack.ReceiveLetter("Choso arrives",
                how + " He is already on his little brother's side: he joins your colony"
                + (painting == null ? "." : ", and he has brought Yuji a Death Painting" + (handed ? " (in Yuji's inventory)." : " (on the ground beside him).")),
                LetterDefOf.PositiveEvent, p);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref choso, "choso", true);
            Scribe_Values.Look(ref noticed, "chosoNoticed");
            Scribe_Values.Look(ref arrived, "chosoArrived");
            Scribe_Values.Look(ref beside, "chosoBeside");
            Scribe_Values.Look(ref arriveTick, "chosoArriveTick", -1);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_ChosoNotices
    {
        public static void Postfix(Pawn __instance, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || ChosoTracker.Instance == null || !Yuji.Is(__instance)) return;
            ChosoTracker.Instance.YujiHurt(__instance, totalDamageDealt);
        }
    }

    public static class Choso_
    {
        public const int PsycastLevel = 10;   // her call 2026-11-11: he's not that strong

        /// <summary>Childhood curse, adulthood sorcerer (her calls). The full Hemosage tree, Piercing Blood (one
        /// target), poisonous blood. Hair, clothes, traits beyond his blood, and age are proposals.</summary>
        public static Pawn Make()
        {
            var p = JujutsuFundamentals.Characters.Make("Choso", "Choso", "", Gender.Male, 28f);
            p.Name = new NameSingle("Choso");
            p.ageTracker.BirthAbsTicks = GenTicks.TicksAbs - 150L * GenDate.TicksPerYear;   // a Death Painting from ~150 years ago
            JujutsuFundamentals.Characters.Skill(p, SkillDefOf.Melee, 14, Passion.Major);
            if (p.story != null)
            {
                p.story.Childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_CurseChildhood") ?? p.story.Childhood;
                p.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_Sorcerer") ?? p.story.Adulthood;
                p.story.traits.allTraits.Clear();
                // Kind, Tough, Brawler (her calls 2026-11-11) and poisonous blood (canon: his blood is toxic)
                foreach (var name in new[] { "Kind", "Tough", "Brawler" })
                {
                    var td = DefDatabase<TraitDef>.GetNamedSilentFail(name);
                    if (td != null) p.story.traits.GainTrait(new Trait(td, 0, true));
                }
                PoisonBlood.EnsureTrait(p);
                if (p.genes != null)
                    foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null || g.def.skinColorOverride != null).ToList())
                        p.genes.RemoveGene(g);
                p.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail("Choso_Hair") ?? p.story.hairDef;
                p.story.HairColor = new Color(0.12f, 0.1f, 0.1f);
                p.story.skinColorOverride = new Color(0.94f, 0.83f, 0.73f);
            }
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = ChosoDefOf.Choso_NoseMark ?? p.style.FaceTattoo;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (ModsConfig.BiotechActive && hemogenic != null && p.genes != null && !p.genes.HasActiveGene(hemogenic))
                p.genes.AddGene(hemogenic, true);
            JujutsuFundamentals.Characters.Psycaster(p, PsycastLevel);
            if (SukunaArc.Hemosage != null) JujutsuFundamentals.Characters.Learn(p, SukunaArc.Hemosage);
            var comp = p.GetComp<VEF.Abilities.CompAbilities>();
            if (comp != null && !comp.HasAbility(ChosoDefOf.Choso_PiercingBlood)) comp.GiveAbility(ChosoDefOf.Choso_PiercingBlood);
            // Cursed Energy Reinforcement, the only Fundamental he uses (her call 2026-11-11)
            JujutsuFundamentals.Characters.Learn(p, JujutsuFundamentals.JJKDefOf.JJK_CursedEnergyFundamentals, JujutsuFundamentals.JJKAbilityDefOf.JJK_Reinforcement);
            JujutsuFundamentals.PassiveSync.Sync(p);
            p.equipment?.DestroyAllEquipment();
            Dress(p);
            Brothers(p, QuestlineTracker.Instance?.yuji);
            p.Notify_DisabledWorkTypesChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            // ideoligion (my pick 2026-11-11, she adjusts after testing): the good guys share one
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", RimWorld.FactionDefOf.PlayerColony, "Collectivist"));
            return p;
        }

        public static void Brothers(Pawn choso, Pawn yuji)
        {
            var def = ChosoBrother.Def;
            if (def == null || choso?.relations == null || yuji?.relations == null || choso.relations.DirectRelationExists(def, yuji)) return;
            choso.relations.AddDirectRelation(def, yuji);
        }

        /// <summary>His own sashed robes (light, baggy, dark purple sashes in an X, at the neck and waist; canon from a
        /// fan wiki, her ask 2026-11-11) and light trousers. RimWorld draws no feet, so no boots.</summary>
        private static void Dress(Pawn p)
        {
            if (p.apparel == null) return;
            p.apparel.DestroyAll();
            var light = new Color(0.97f, 0.97f, 0.97f);   // white (her call 2026-11-11; was cream)
            Wear(p, "Apparel_Pants", "Cloth", light);
            Wear(p, "Choso_Outfit", "Cloth", light);
        }

        private static void Wear(Pawn p, string def, string stuff, Color color)
        {
            var d = DefDatabase<ThingDef>.GetNamedSilentFail(def);
            if (d == null) return;
            var a = (Apparel)ThingMaker.MakeThing(d, d.MadeFromStuff ? DefDatabase<ThingDef>.GetNamedSilentFail(stuff) ?? GenStuff.DefaultStuffFor(d) : null);
            a.SetColor(color, false);
            if (p.apparel.CanWearWithoutDroppingAnything(a.def)) p.apparel.Wear(a, false, true);
        }
    }

    /// <summary>Loyal to Yuji: they are brothers (half brothers in canon, through Kenjaku). A direct relation, since
    /// the game's own sibling relation only comes from shared parents.</summary>
    public static class ChosoBrother
    {
        public static PawnRelationDef Def => DefDatabase<PawnRelationDef>.GetNamedSilentFail("JJK_Brother");
    }

    /// <summary>Choso's Piercing Blood: one straight beam at one target (her call 2026-11-11), the same blood and
    /// poison as Yuji's.</summary>
    public class Ability_PiercingBloodSingle : VEFAbility
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (pawn.Map == null || targets == null || targets.Length == 0) return;
            var t = targets[0].Thing as Pawn;
            BloodBeams.Fire(pawn, new List<LocalTargetInfo> { t != null ? new LocalTargetInfo(t) : new LocalTargetInfo(targets[0].Cell) });
        }
    }
}
