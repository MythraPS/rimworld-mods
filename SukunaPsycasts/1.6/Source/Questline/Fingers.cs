using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using VEF.Abilities;
using VanillaPsycastsExpanded;

namespace SukunaPsycasts
{
    /// <summary>
    /// There are twenty fingers and never more. Counted live rather than tracked, so
    /// nothing can drift: loose fingers on any map or in any container, fingers in
    /// caravans, fingers waiting in quest sites and trader stock, and fingers inside
    /// living vessels. A vessel who is forgotten by the world (a raider who fled and
    /// was cleaned up) takes theirs back into the pool.
    /// </summary>
    public static class FingerCensus
    {
        public const int Total = 20;

        /// <summary>Fingers Yuji cut out of Sukuna are destroyed and never come back.</summary>
        public static int Remaining() => Mathf.Max(0, Total - Count() - (SukunaArc.Instance?.fingersDestroyed ?? 0));

        public static int Count()
        {
            int n = CountLoose(SukunaThingDefOf.Sukuna_Finger);
            foreach (var p in PawnsFinder.AllMapsWorldAndTemporary_Alive)
                n += Scaling.FingersEaten(p);
            return n;
        }

        /// <summary>Every <paramref name="def"/> lying anywhere: maps, containers, caravans, quest
        /// sites and trader stock.</summary>
        public static int CountLoose(ThingDef def)
        {
            int n = 0;
            var buf = new List<Thing>();

            foreach (var map in Find.Maps)
            {
                buf.Clear();
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(def), buf, true, null, false);
                n += buf.Sum(t => t.stackCount);
            }
            foreach (var c in Find.WorldObjects.Caravans)
                n += CaravanInventoryUtility.AllInventoryItems(c).Where(t => t.def == def).Sum(t => t.stackCount);
            foreach (var site in Find.WorldObjects.Sites)
                foreach (var part in site.parts)
                    if (part.things != null)
                        n += part.things.Where(t => t.def == def).Sum(t => t.stackCount);
            foreach (var s in Find.WorldObjects.Settlements)
                if (s.trader?.StockListForReading != null)
                    n += s.trader.StockListForReading.Where(t => t.def == def).Sum(t => t.stackCount);
            return n;
        }
    }

    /// <summary>A quest stash holds several at once (her call 2026-11-02: one at a time felt sluggish), never
    /// more than are still out in the world; finds by chance can still be single.</summary>
    public static class QuestStash
    {
        public static readonly IntRange PerQuest = new IntRange(2, 4);

        public static void Put(RimWorld.QuestGen.Slate slate, ThingDef def, int remaining)
        {
            int n = Mathf.Clamp(PerQuest.RandomInRange, 1, Mathf.Max(1, remaining));
            if (n <= 1) slate.Set("itemStashSingleThing", def);
            else slate.Set("itemStashThings", Enumerable.Repeat(def, n).ToList());
        }
    }

    public static class VesselMaker
    {
        /// <summary>Feed an NPC <paramref name="fingers"/> fingers with no messages and
        /// no takeover roll, then switch on what they learned so the AI fights with it.</summary>
        public static void Make(Pawn p, int fingers)
        {
            bool was = CompCursedFinger.Silent;
            CompCursedFinger.Silent = true;
            try
            {
                for (int i = 0; i < fingers; i++) CompCursedFinger.Consume(p, rollTakeover: false);
            }
            finally { CompCursedFinger.Silent = was; }
            ArmForAI(p);
        }

        /// <summary>Non-player vessels do not click toggles, so turn on every sustained
        /// technique they know and fill their psyfocus.</summary>
        public static void ArmForAI(Pawn p)
        {
            if (p == null || p.Faction == Faction.OfPlayer) return;
            var comp = p.GetComp<CompAbilities>();
            if (comp == null) return;

            p.psychicEntropy?.OffsetPsyfocusDirectly(1f);
            CleaveToggle.Set(p, true);
            if (comp.HasAbility(SukunaAbilityDefOf.Sukuna_SlashDefenseThorns))
                SustainedToggles.Set(p, "thorns", SukunaDefOf.Sukuna_ThornsShield, "Thorns", true);
            if (comp.HasAbility(SukunaAbilityDefOf.Sukuna_SlashDefensePseudoInfinity))
                SustainedToggles.Set(p, "infinity", SukunaDefOf.Sukuna_PseudoInfinity, "Raise cursed energy output", true);
            if (comp.HasAbility(SukunaAbilityDefOf.Sukuna_ReverseCursedTechnique))
                SustainedToggles.Set(p, "rct", SukunaDefOf.Sukuna_ReverseCursedTechnique, "Reverse cursed technique", true);
        }
    }

    /// <summary>What the King of Curses looks like when he has a body: spiky pink hair (the short King
    /// hair in his four-armed form), his own pale skin whatever body he took, no hat, no shirt, no weapon
    /// (her notes 2026-11-01, 11-06, 11-07; Kamutoke left the mod 2026-11-08 for a later Cursed Tools mod).</summary>
    public static class SukunaLook
    {
        public static void Apply(Pawn p) => Apply(p, p != null && FourArmed.Is(p));

        /// <summary>When he takes a body the top tears off: everything covering the torso drops at half its
        /// durability (her call 2026-11-02). Off the map it is simply lost.</summary>
        public static void TearOffTop(Pawn p)
        {
            if (p?.apparel == null) return;
            foreach (var a in p.apparel.WornApparel
                         .Where(a => a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Torso)
                                     && !a.def.apparel.layers.Contains(ApparelLayerDefOf.Belt)).ToList())
            {
                a.HitPoints = Mathf.Max(1, a.HitPoints - a.MaxHitPoints / 2);
                if (p.Spawned) p.apparel.TryDrop(a, out _, p.PositionHeld, false);
                else { p.apparel.Remove(a); a.Destroy(); }
            }
        }

        /// <summary>No hats (her note 2026-11-06: he spawned in one): anything on the head comes off
        /// the same way the top does, whole.</summary>
        public static void TakeOffHat(Pawn p)
        {
            if (p?.apparel == null) return;
            foreach (var a in p.apparel.WornApparel.Where(a => a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.FullHead)
                                                               || a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.UpperHead)
                                                               || a.def.apparel.layers.Contains(ApparelLayerDefOf.Overhead)).ToList())
            {
                if (p.Spawned) p.apparel.TryDrop(a, out _, p.PositionHeld, false);
                else { p.apparel.Remove(a); a.Destroy(); }
            }
        }

        /// <summary>No guns or borrowed blades (her note 2026-11-07): he fights with his own hands and
        /// techniques.</summary>
        public static void DropWeapon(Pawn p)
        {
            var w = p?.equipment?.Primary;
            if (w == null) return;
            if (p.Spawned) p.equipment.TryDropEquipment(w, out _, p.PositionHeld, false);
            else p.equipment.DestroyEquipment(w);
        }

        /// <summary>Sukuna is a human man whatever body he took (her note 2026-11-07: he came out a female
        /// Yttakin with pink fur). Xenotype goes to Baseliner: all its genes go except the skin tone (the
        /// game's own SetXenotype only clears xenogenes, and Yttakin fur is an endogene). A woman's body
        /// and head become a man's; the Kind trait goes, which the King of Curses is not.</summary>
        public static void HumanMaleBody(Pawn p)
        {
            if (p?.genes != null && ModsConfig.BiotechActive)
            {
                foreach (var g in p.genes.Endogenes.Where(g => g.def.endogeneCategory != EndogeneCategory.Melanin).ToList())
                    p.genes.RemoveGene(g);
                if (p.genes.Xenotype != XenotypeDefOf.Baseliner || p.genes.Xenogenes.Count > 0)
                    p.genes.SetXenotype(XenotypeDefOf.Baseliner);
            }
            if (p.gender != Gender.Male) p.gender = Gender.Male;
            if (p.story.headType == null || p.story.headType.gender == Gender.Female)
                p.story.headType = DefDatabase<HeadTypeDef>.GetNamedSilentFail("Male_AverageNormal") ?? p.story.headType;
            if (p.story.bodyType != BodyTypeDefOf.Hulk && p.story.bodyType != BodyTypeDefOf.Male)
                p.story.bodyType = BodyTypeDefOf.Male;
            var kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
            var t = kind == null ? null : p.story.traits?.GetTrait(kind);
            if (t != null) p.story.traits.RemoveTrait(t);
        }

        public static void Apply(Pawn p, bool king)
        {
            if (p?.story == null) return;
            p.story.hairDef = king ? SukunaQuestDefOf.Sukuna_KingHair : SukunaQuestDefOf.Sukuna_VesselHair;
            // his own pale skin and pink hair whatever body he took (her note 2026-11-07)
            p.story.HairColor = new Color(0.93f, 0.55f, 0.62f);
            p.story.skinColorOverride = Yuji.Skin;
            DropWeapon(p);
            HumanMaleBody(p);
            if (p.style != null)
            {
                // the true form wears the mask over his right eyes (her note 2026-11-06)
                p.style.FaceTattoo = king && SukunaQuestDefOf.Sukuna_KingMarkings != null ? SukunaQuestDefOf.Sukuna_KingMarkings : SukunaQuestDefOf.Sukuna_Markings;
                p.style.beardDef = BeardDefOf.NoBeard;
            }
            // his body markings (her note 2026-11-06: more tattoos)
            if (SukunaDefOf.Sukuna_CurseMarks != null && p.health != null && !p.health.hediffSet.HasHediff(SukunaDefOf.Sukuna_CurseMarks))
                p.health.AddHediff(SukunaDefOf.Sukuna_CurseMarks);
            // Shirtless (her call 2026-11-02): anything over the chest comes off, and he keeps something on his legs.
            if (p.apparel != null)
            {
                TearOffTop(p);
                TakeOffHat(p);
                bool legs = p.apparel.WornApparel.Any(a => a.def.apparel.bodyPartGroups.Contains(BodyPartGroupDefOf.Legs));
                if (!legs) Yuji.Wear(p, "Apparel_Pants", new Color(0.95f, 0.94f, 0.9f));
            }
            p.Drawer?.renderer?.SetAllGraphicsDirty();
        }
    }

    /// <summary>The rare finger rumour that is just a trader with one in the stock.</summary>
    public static class FingerTrader
    {
        public static bool TryArrive(Map map) => TryArrive(map, SukunaThingDefOf.Sukuna_Finger,
            "is carrying something wrapped and bound that is warm to the touch: a cursed finger, for sale.");

        public static bool TryArrive(Map map, ThingDef item, string what)
        {
            var faction = Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && !f.HostileTo(Faction.OfPlayer)
                            && f.def.caravanTraderKinds != null && f.def.caravanTraderKinds.Count > 0)
                .RandomElementWithFallback();
            if (faction == null) return false;

            var before = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
            var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.Misc, map);
            parms.faction = faction;
            parms.forced = true;
            if (!IncidentDefOf.TraderCaravanArrival.Worker.TryExecute(parms)) return false;

            var trader = map.mapPawns.AllPawnsSpawned
                .FirstOrDefault(p => !before.Contains(p) && p.Faction == faction && p.trader?.traderKind != null);
            if (trader == null) return true;
            trader.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(item));
            Messages.Message($"{trader.LabelShortCap} {what}", trader, MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }
}
