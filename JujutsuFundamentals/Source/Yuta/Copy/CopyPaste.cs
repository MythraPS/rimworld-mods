using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Copy (her rules 2026-11-11): only with Rika fully manifested. She eats part of someone who is down or held
    /// (stunned; her Grapple is a stun), and Yuta gains some of their techniques: a finger or toe 1, a hand, foot or
    /// ear 2, an arm, leg, eye or any organ but the brain and heart 3, and the head, brain or heart all of them (which
    /// kills). Picked at random from what he doesn't have, given directly (their paths stay closed to him). Never
    /// Yuta's own; Shrine only from Yuji's body, or from a Sukuna finger Rika eats, which gives Shrine as a vessel's
    /// finger would but with no vessel and no takeover, ever.
    /// </summary>
    public class Ability_Copy : Ability
    {
        /// <summary>How close the target must be to Rika or Yuta (proposal).</summary>
        public const float Reach = 3.9f;

        public override bool IsEnabledForPawn(out string reason)
        {
            // born with Copy (her rules 2026-11-12): no Rika, no ring
            if (Innate.CopiesAlone(pawn)) return base.IsEnabledForPawn(out reason);
            if (!Rika.Manifested(pawn) || !RikaRing.Connected(pawn)) { reason = "Needs Yuta connected to Rika through his ring."; return false; }
            if (RikaHands.BlockOnYuta(pawn, out reason)) return false;
            return base.IsEnabledForPawn(out reason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages)) return false;
            if (CopyRules.CanEat(pawn, target.Thing, out string why)) return true;
            if (showMessages && why != null) Messages.Message(why, target.Thing, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        /// <summary>Psyfocus, neural heat and cooldown (VEF's and VPE's own cast).</summary>
        private void Pay(GlobalTargetInfo[] targets) => SkyPsycastRedirect.Unwatched(this, () => base.Cast(targets));

        /// <summary>Paid only once a part is picked (her call 2026-11-12): closing the menu costs nothing.</summary>
        public override void Cast(GlobalTargetInfo[] targets)
        {
            var thing = targets?.FirstOrDefault().Thing;
            if (!CopyRules.CanEat(pawn, thing, out string why))
            {
                if (why != null) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (CopyRules.IsFinger(thing)) { Pay(targets); CopyRules.EatFinger(pawn, thing); return; }
            var victim = (Pawn)thing;
            var options = CopyRules.Choices(pawn, victim);
            if (options.Count == 0) { Messages.Message("Nothing left of them for Rika to eat.", victim, MessageTypeDefOf.RejectInput, false); return; }
            if (pawn.Faction == Faction.OfPlayer)
            {
                var saved = targets.ToArray();
                Find.WindowStack.Add(new FloatMenu(options.Select(o => new FloatMenuOption(o.label, () =>
                {
                    // things may have moved on while the menu was open
                    if (cooldown > Find.TickManager.TicksGame) return;
                    if (!CopyRules.CanEat(pawn, victim, out string late))
                    {
                        if (late != null) Messages.Message(late, pawn, MessageTypeDefOf.RejectInput, false);
                        return;
                    }
                    Pay(saved);
                    o.act();
                })).ToList()));
            }
            else { Pay(targets); options.OrderByDescending(o => o.worth).First().act(); }
        }
    }

    public static class CopyRules
    {
        public const int All = 99;

        public struct Choice { public string label; public int worth; public System.Action act; }

        public static bool IsFinger(Thing t) => t != null && t.TryGetComp<SukunaPsycasts.CompCursedFinger>() != null;

        public static bool CanEat(Pawn yuta, Thing t, out string why)
        {
            why = null;
            if (t == null) return false;
            if (Innate.CopiesAlone(yuta))
            {
                // their own Copy: part of a downed or held psycaster, within reach of them
                if (!t.Spawned || t.Map != yuta.Map || !t.Position.InHorDistOf(yuta.Position, Ability_Copy.Reach)) { why = "Too far away."; return false; }
                if (!(t is Pawn v) || v.Dead || v == yuta) { why = "Only part of a psycaster can be copied from."; return false; }
                if (!CopyAlone.Psycaster(v)) { why = $"{v.LabelShortCap} isn't a psycaster."; return false; }
                if (!v.Downed && !(v.stances?.stunner?.Stunned ?? false)) { why = "They must be down or held first."; return false; }
                return true;
            }
            var rika = Rika.Current;
            if (!Rika.Manifested(yuta) || !RikaRing.Connected(yuta)) { why = "Needs Yuta connected to Rika through his ring."; return false; }
            bool near = t.Spawned && t.Map == yuta.Map
                        && (t.Position.InHorDistOf(yuta.Position, Ability_Copy.Reach) || (rika != null && t.Position.InHorDistOf(rika.Position, Ability_Copy.Reach)));
            if (!near) { why = "Too far from Rika and Yuta."; return false; }
            if (IsFinger(t)) return true;
            if (!(t is Pawn p) || p.Dead || p == yuta || p == rika) { why = "Rika can only eat part of a person, or a Sukuna finger."; return false; }
            if (!p.Downed && !(p.stances?.stunner?.Stunned ?? false)) { why = "They must be down or held first."; return false; }
            return true;
        }

        /// <summary>What a part is worth: 0 = Rika doesn't take it.</summary>
        public static int Worth(BodyPartRecord r)
        {
            switch (r.def.defName)
            {
                case "Finger": case "Toe": return 1;
                case "Hand": case "Foot": case "Ear": return 2;
                case "Arm": case "Leg": case "Eye": case "Lung": case "Kidney": case "Liver": case "Stomach": return 3;
                case "Head": case "Brain": case "Heart": return All;
            }
            // other races' organs: anything inside that breathes, filters blood or digests
            if (r.depth == BodyPartDepth.Inside && r.def.tags != null && r.def.tags.Any(tag =>
                    tag.defName == "BreathingSource" || tag.defName.StartsWith("BloodFiltration") || tag.defName == "MetabolismSource"))
                return 3;
            return 0;
        }

        public static List<Choice> Choices(Pawn yuta, Pawn victim)
        {
            var list = new List<Choice>();
            int have = Copyable(yuta, victim).Count;
            foreach (var g in victim.health.hediffSet.GetNotMissingParts().Where(r => Worth(r) > 0).GroupBy(r => r.def))
            {
                var part = g.First();
                int w = Worth(part);
                int n = Mathf.Min(w, have);
                string what = w == All ? $"all of their techniques ({have}); this kills them" : $"{n} of their {have} techniques";
                list.Add(new Choice
                {
                    label = $"{g.Key.LabelCap}: {what}",
                    worth = w,
                    act = () => Eat(yuta, victim, part, w),
                });
            }
            return list.OrderBy(c => c.worth).ToList();
        }

        public static bool IsShrine(VEFAbilityDef d) =>
            d?.GetModExtension<AbilityExtension_Psycast>()?.path == SukunaPsycasts.SukunaDefOf.Sukuna_Path && SukunaPsycasts.SukunaDefOf.Sukuna_Path != null;

        public static List<VEFAbilityDef> Copyable(Pawn yuta, Pawn victim)
        {
            var tc = victim?.GetComp<CompAbilities>();
            var yc = yuta?.GetComp<CompAbilities>();
            if (tc == null || yc == null) return new List<VEFAbilityDef>();
            bool yuji = SukunaPsycasts.Yuji.Is(victim) || SukunaPsycasts.ModuloYuji.Is(victim);
            return tc.LearnedAbilities.Select(a => a.def).Where(d => d != null).Distinct()
                     .Where(d => !yc.HasAbility(d) && !YutaAbilities.IsYutaOnly(d) && (yuji || !IsShrine(d))).ToList();
        }

        public static void Eat(Pawn yuta, Pawn victim, BodyPartRecord part, int worth)
        {
            if (victim == null || victim.Dead || part == null || !victim.health.hediffSet.GetNotMissingParts().Contains(part)) return;
            var gain = Copyable(yuta, victim).InRandomOrder().Take(worth == All ? int.MaxValue : worth).ToList();
            bool alone = Innate.CopiesAlone(yuta);
            var eater = alone ? yuta : (Rika.Current ?? yuta);
            var map = victim.Map;
            if (map != null)
            {
                FilthMaker.TryMakeFilth(victim.Position, map, victim.RaceProps.BloodDef ?? ThingDefOf.Filth_Blood, 3);
                MoteMaker.ThrowText(victim.DrawPos, map, $"{(alone ? yuta.LabelShortCap : "Rika")} eats the {part.Label}", Yuta.Purple, 3f);
            }
            var miss = (Hediff_MissingPart)HediffMaker.MakeHediff(HediffDefOf.MissingBodyPart, victim, part);
            miss.lastInjury = HediffDefOf.Bite;
            miss.IsFresh = true;
            victim.health.AddHediff(miss, part);
            if (worth == All && !victim.Dead)
                victim.Kill(new DamageInfo(DamageDefOf.Bite, 999f, 999f, -1f, eater, part));
            Learn(yuta, gain, victim);
        }

        public static void Learn(Pawn yuta, List<VEFAbilityDef> gain, Pawn from, string fromName = null)
        {
            var yc = yuta.GetComp<CompAbilities>();
            if (yc == null) return;
            var got = new List<string>();
            foreach (var d in gain)
            {
                yc.GiveAbility(d);
                if (yc.HasAbility(d)) got.Add(d.LabelCap);
            }
            PassiveSync.Sync(yuta);
            string who = from?.LabelShort ?? fromName ?? "them";
            if (got.Count == 0) Messages.Message($"There was nothing of {who}'s left for {yuta.LabelShort} to copy.", yuta, MessageTypeDefOf.NeutralEvent, false);
            else Messages.Message($"{yuta.LabelShortCap} copies from {who}: {GenText.ToCommaList(got, true)}.", yuta, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>Rika eats a Sukuna finger: one more psycast level for Yuta, committed to the next Shrine node like a
        /// vessel's finger (stats once Shrine is full). No vessel, no takeover. Yuta holds the finger from then on
        /// (her call 2026-11-11): FingersHeld counts it in the twenty-finger census, and he drops it if he dies.</summary>
        public static void EatFinger(Pawn yuta, Thing finger)
        {
            var map = finger.Map; var at = finger.Position;
            if (finger.stackCount > 1) finger.SplitOff(1).Destroy(); else finger.Destroy();
            FingersHeld.Add(1);
            if (map != null) MoteMaker.ThrowText(at.ToVector3Shifted(), map, "Rika eats the finger", Yuta.Purple, 3f);

            yuta.ChangePsylinkLevel(1, false);
            var psy = Characters.Psycaster(yuta, 1);
            if (psy == null) return;
            var shrine = SukunaPsycasts.SukunaDefOf.Sukuna_Path;
            // the path is opened only while the finger is spent, so he can't put his own points into Shrine
            bool opened = shrine != null && !psy.unlockedPaths.Contains(shrine);
            if (opened) psy.unlockedPaths.Add(shrine);
            try
            {
                int before = psy.points;
                psy.ChangeLevel(1);
                SukunaPsycasts.FingerProgression.GrantMorbidFocus(psy);
                SukunaPsycasts.FingerProgression.SpendOnShrine(yuta, psy, before);
            }
            finally { if (opened) psy.unlockedPaths.Remove(shrine); }
            PassiveSync.Sync(yuta);
        }
    }

    /// <summary>
    /// Paste (hers, not canon, 2026-11-11). Needs Reverse Cursed Technique learned first, though it's on another tree.
    /// Yuta gives one of his techniques away: he loses it and the point he spent on it, and the other pawn gains it.
    /// The other pawn needs psychic sensitivity and psycast level 1. Never what's his alone (Copy, Paste, healing
    /// others, Rika, his domain, Die).
    /// </summary>
    public class Ability_Paste : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (!Jujutsu.Knows(pawn, JJKAbilityDefOf.JJK_ReverseCursedTechnique))
            { reason = "Needs Reverse Cursed Technique (Cursed Energy Fundamentals) first."; return false; }
            if (PasteRules.Giftable(pawn).Count == 0) { reason = "No technique he can give away."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages)) return false;
            if (PasteRules.CanReceive(pawn, target.Pawn, out string why)) return true;
            if (showMessages && why != null) Messages.Message(why, target.Thing, MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var to = targets?.FirstOrDefault().Thing as Pawn;
            if (!PasteRules.CanReceive(pawn, to, out string why))
            {
                if (why != null) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            var gifts = PasteRules.Giftable(pawn);
            if (gifts.Count == 0) return;
            if (pawn.Faction == Faction.OfPlayer)
            {
                var me = pawn;
                Find.WindowStack.Add(new FloatMenu(gifts.Select(d => new FloatMenuOption(d.LabelCap, () => PasteRules.Give(me, to, d))).ToList()));
            }
            else PasteRules.Give(pawn, to, gifts.RandomElement());
        }
    }

    public static class PasteRules
    {
        private static readonly System.Reflection.FieldInfo Learned = AccessTools.Field(typeof(CompAbilities), "learnedAbilities");
        private static readonly System.Reflection.FieldInfo ToTick = AccessTools.Field(typeof(CompAbilities), "abilitiesToTick");
        private static readonly System.Reflection.FieldInfo ToTickInterval = AccessTools.Field(typeof(CompAbilities), "abilitiesToTickInterval");

        public static List<VEFAbilityDef> Giftable(Pawn yuta)
        {
            var yc = yuta?.GetComp<CompAbilities>();
            if (yc == null) return new List<VEFAbilityDef>();
            return yc.LearnedAbilities.Select(a => a.def).Where(d => d != null && !YutaAbilities.IsYutaOnly(d) && !JacobsLadder.Is(d)).Distinct().ToList();   // nor the Angel's
        }

        public static bool CanReceive(Pawn yuta, Pawn to, out string why)
        {
            why = null;
            if (to == null || to.Dead || to == yuta) { why = "Paste needs another person."; return false; }
            if (to.GetComp<CompAbilities>() == null || !to.RaceProps.Humanlike) { why = "They can't hold a technique."; return false; }
            if (to.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f) { why = $"{to.LabelShort} has no psychic sensitivity."; return false; }
            if (to.GetPsylinkLevel() < 1) { why = $"{to.LabelShort} needs psycast level 1 first."; return false; }
            return true;
        }

        public static void Give(Pawn yuta, Pawn to, VEFAbilityDef def)
        {
            var yc = yuta?.GetComp<CompAbilities>();
            var tc = to?.GetComp<CompAbilities>();
            if (yc == null || tc == null || def == null || !yc.HasAbility(def)) return;
            if (tc.HasAbility(def)) { Messages.Message($"{to.LabelShort} already knows {def.label}.", to, MessageTypeDefOf.RejectInput, false); return; }
            tc.GiveAbility(def);
            if (!tc.HasAbility(def))
            {
                Messages.Message($"{to.LabelShort} can't take {def.label}. Yuta keeps it.", to, MessageTypeDefOf.RejectInput, false);
                return;
            }
            Forget(yc, def);
            PassiveSync.Sync(yuta);
            PassiveSync.Sync(to);
            Messages.Message($"Yuta gives {def.label} to {to.LabelShort}.", to, MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>Takes an ability off a pawn: out of the learned list and VEF's tick lists. No point is refunded.</summary>
        public static void Forget(CompAbilities comp, VEFAbilityDef def)
        {
            foreach (var f in new[] { Learned, ToTick, ToTickInterval })
            {
                if (f == null) continue;
                if (f.GetValue(comp) is System.Collections.IList list)
                    for (int i = list.Count - 1; i >= 0; i--)
                        if (list[i] is Ability a && a.def == def) list.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// The Sukuna fingers Rika has eaten are held inside Yuta (her call 2026-11-11), not destroyed. The census
    /// (FingerCensus.Count: loose fingers plus each vessel's) can't see them, so a postfix adds them while he's alive.
    /// They are not a vessel's fingers: Scaling.FingersEaten stays 0 for him, so nothing about takeovers, Shrine's
    /// scaling or the four-armed form reads them. When he dies they drop at his body, as loose fingers again.
    /// </summary>
    public static class FingersHeld
    {
        public static int Count => YutaCopyState.Instance?.fingersHeld ?? 0;

        public static void Add(int n) { if (YutaCopyState.Instance != null) YutaCopyState.Instance.fingersHeld += n; }

        public static void Drop(Pawn yuta)
        {
            var st = YutaCopyState.Instance;
            if (st == null || st.fingersHeld <= 0) return;
            var def = SukunaPsycasts.SukunaThingDefOf.Sukuna_Finger;
            if (def == null) return;
            int n = st.fingersHeld;
            st.fingersHeld = 0;
            var corpse = yuta.Corpse;
            Map map = corpse?.MapHeld ?? yuta.MapHeld;
            IntVec3 at = corpse != null ? corpse.PositionHeld : yuta.PositionHeld;
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = n;
            if (map != null && at.IsValid && GenPlace.TryPlaceThing(thing, at, map, ThingPlaceMode.Near))
            {
                Messages.Message(n == 1 ? "The Sukuna finger Rika ate falls from Yuta's body." : $"The {n} Sukuna fingers Rika ate fall from Yuta's body.",
                                 new TargetInfo(at, map), MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            var caravan = yuta.GetCaravan() ?? (corpse?.ParentHolder as Caravan);
            if (caravan != null) { CaravanInventoryUtility.GiveThing(caravan, thing); return; }
            thing.Destroy();   // no map and no caravan: they go back into the world pool (the census stops counting them)
        }
    }

    [HarmonyPatch(typeof(SukunaPsycasts.FingerCensus), nameof(SukunaPsycasts.FingerCensus.Count))]
    public static class Patch_CensusHeldByYuta
    {
        public static void Postfix(ref int __result)
        {
            var y = YutaTracker.Instance?.yuta;
            if (y != null && !y.Dead) __result += FingersHeld.Count;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_YutaDropsFingers
    {
        public static void Postfix(Pawn __instance)
        {
            if (__instance != null && __instance.Dead && Yuta.Is(__instance)) FingersHeld.Drop(__instance);
        }
    }
}
