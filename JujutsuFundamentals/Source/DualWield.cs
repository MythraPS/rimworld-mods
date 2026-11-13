using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Dual wielding (her call 2026-11-11: awakened Maki wields Dragon-Bone and the Split Soul Katana together). RimWorld
    /// has one weapon hand, so: she holds one of the pair and carries the other (inventory, or as a Simple Sidearms
    /// sidearm); every melee hit she lands with one is followed by a strike from the other on the same target, with that
    /// weapon's own damage and its own cursed-tool effect (the katana ignores armour; Dragon-Bone stores and releases).
    /// Proposal: the follow-up hits for 60% of the off-hand's damage. The off-hand is drawn in her other hand (Patch_DualWieldDraw).
    /// </summary>
    public static class DualWield
    {
        public const float OffHandFactor = 0.6f;

        /// <summary>Who can dual wield, and the pairs they wield (both ways round).</summary>
        public static readonly List<Func<Pawn, bool>> Wielders = new List<Func<Pawn, bool>> { MakiForms.IsAwakened };   // awakened Maki only
        public class Pair { public Func<ThingDef> a, b; }
        public static readonly List<Pair> Pairs = new List<Pair>
        {
            new Pair { a = () => JJKToolDefOf.JJK_DragonBone, b = () => JJKToolDefOf.JJK_SplitSoulKatana },
        };

        private static bool striking;

        public static Thing OffHand(Pawn p)
        {
            var main = p?.equipment?.Primary;
            if (main == null || p.inventory == null || !Jujutsu.Any(Wielders, p)) return null;
            foreach (var pair in Pairs)
            {
                ThingDef a = pair.a(), b = pair.b(), other = main.def == a ? b : main.def == b ? a : null;
                if (other == null) continue;
                var t = p.inventory.innerContainer.FirstOrDefault(x => x.def == other);
                if (t != null) return t;
            }
            return null;
        }

        public static void FollowUp(Pawn attacker, Thing target)
        {
            if (striking || !(target is Pawn victim) || victim.Dead || !victim.Spawned) return;
            var off = OffHand(attacker);
            var tool = off?.def.tools?.OrderByDescending(t => t.power).FirstOrDefault();
            if (tool == null) return;
            var dmgDef = tool.Maneuvers.FirstOrDefault()?.verb?.meleeDamageDef ?? DamageDefOf.Cut;
            float amount = tool.power * OffHandFactor * attacker.GetStatValue(StatDefOf.MeleeDamageFactor);
            float ap = tool.armorPenetration >= 0f ? tool.armorPenetration : tool.power * 0.015f;
            var dinfo = new DamageInfo(dmgDef, amount, ap, -1f, attacker, null, off.def);
            striking = true;
            BlackFlashRules.meleeDepth++;   // counts as melee: cursed-tool effects and Black Flash rules apply
            try { victim.TakeDamage(dinfo); }
            finally { BlackFlashRules.meleeDepth = Mathf.Max(0, BlackFlashRules.meleeDepth - 1); striking = false; }
            if (victim.Spawned) FleckMaker.ThrowMicroSparks(victim.DrawPos, victim.Map);
        }

        /// <summary>The second weapon of her pair, carried, when she has none (Maki's starting kit).</summary>
        public static void GiveOffHand(Pawn p, ThingDef def)
        {
            if (p?.inventory == null || def == null || p.inventory.innerContainer.Any(x => x.def == def)) return;
            var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
            p.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(def, stuff));
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget")]
    public static class Patch_DualWieldFollowUp
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            if (__instance.CasterPawn is Pawn p && target.Thing != null) DualWield.FollowUp(p, target.Thing);
        }
    }
}

namespace JujutsuFundamentals
{
    /// <summary>
    /// The off-hand weapon drawn in her other hand (her report 2026-11-11: only Dragon-Bone showed). Mirrors vanilla's
    /// own rules in PawnRenderUtility.DrawEquipmentAndApparelExtras (read from the 1.6 IL): aiming at a target, both
    /// blades point at it, the off-hand a little to the side; carried openly (drafted), it's held crossed the other way
    /// (217 degrees where the main one is at 143), behind the body when facing east or west.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    public static class Patch_DualWieldDraw
    {
        private static readonly System.Reflection.FieldInfo North = AccessTools.Field(typeof(PawnRenderUtility), "EqLocNorth"),
            East = AccessTools.Field(typeof(PawnRenderUtility), "EqLocEast"), South = AccessTools.Field(typeof(PawnRenderUtility), "EqLocSouth"),
            West = AccessTools.Field(typeof(PawnRenderUtility), "EqLocWest");

        private static Vector3 Loc(System.Reflection.FieldInfo f, Vector3 fallback) => f?.GetValue(null) is Vector3 v ? v : fallback;

        // the original moves drawPos while aiming; keep the pawn's own
        public static void Prefix(Vector3 drawPos, out Vector3 __state) => __state = drawPos;

        public static void Postfix(Pawn pawn, Rot4 facing, PawnRenderFlags flags, Vector3 __state)
        {
            if (pawn?.equipment?.Primary == null) return;
            var off = DualWield.OffHand(pawn);
            if (off == null || off.Graphic == null) return;
            if (pawn.CurJob?.def?.neverShowWeapon == true) return;
            float factor = pawn.ageTracker?.CurLifeStage?.equipmentDrawDistanceFactor ?? 1f;
            var busy = pawn.stances?.curStance as Stance_Busy;
            if ((flags & PawnRenderFlags.NeverAimWeapon) == 0 && busy != null && !busy.neverAimWeapon && busy.focusTarg.IsValid)
            {
                var at = busy.focusTarg.HasThing ? busy.focusTarg.Thing.DrawPos : busy.focusTarg.Cell.ToVector3Shifted();
                float aim = 0f;
                if ((at - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f) aim = (at - pawn.DrawPos).AngleFlat();
                var verb = pawn.CurrentEffectiveVerb;
                if (verb?.AimAngleOverride != null) aim = verb.AimAngleOverride.Value;
                float side = aim < 180f ? -28f : 28f;             // fanned to the other side of the main blade
                var pos = __state + new Vector3(0f, -0.002f, 0.4f + off.def.equippedDistanceOffset).RotatedBy(aim + side) * factor;
                PawnRenderUtility.DrawEquipmentAiming(off, pos, aim + side);
                return;
            }
            if (!PawnRenderUtility.CarryWeaponOpenly(pawn)) return;
            Vector3 p = __state; float angle = 217f;
            switch (facing.AsInt)
            {
                case 0: p += Loc(North, new Vector3(0f, 0f, -0.11f)) * factor; break;
                case 1: p += (Loc(East, new Vector3(0.2f, 0.0367f, -0.22f)) + new Vector3(-0.14f, 0f, 0f)) * factor; p.y -= 0.06f; angle = 143f; break;
                case 2: p += Loc(South, new Vector3(0f, 0.0367f, -0.22f)) * factor; break;
                default: p += (Loc(West, new Vector3(-0.2f, 0.0367f, -0.22f)) + new Vector3(0.14f, 0f, 0f)) * factor; p.y -= 0.06f; angle = 217f; break;
            }
            PawnRenderUtility.DrawEquipmentAiming(off, p, angle);
        }
    }

    /// <summary>
    /// Awakened Maki's burn scars in the health tab (her call 2026-11-11: "covered in scars"; canon, Jogo's fire in
    /// Shibuya). Permanent, painless burns spread over her torso, shoulders, arms, legs, neck and head; nothing on the
    /// eyes, ears, brain, hands, feet or jaw, and tiny (1-2 damage each), so they barely touch any capacity.
    /// </summary>
    public static class MakiScars
    {
        public static HediffDef BurnDef => DefDatabase<HediffDef>.GetNamedSilentFail("Burn");
        private static readonly string[] SpreadParts = { "Torso", "Shoulder", "Arm", "Leg", "Neck", "Head" };
        private static readonly int[] SpreadCounts = { 3, 2, 4, 2, 1, 2 };

        public static void Give(Pawn p)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null) return;
            if (BurnDef == null) return;
            for (int k = 0; k < SpreadParts.Length; k++)
            {
                string name = SpreadParts[k]; int count = SpreadCounts[k];
                var parts = hs.GetNotMissingParts().Where(r => r.def.defName == name).ToList();
                if (parts.Count == 0) continue;
                for (int i = 0; i < count; i++)
                {
                    var part = parts[i % parts.Count];
                    var burn = (Hediff_Injury)HediffMaker.MakeHediff(MakiScars.BurnDef, p, part);
                    burn.Severity = Rand.Range(1f, 2f);
                    var perm = burn.TryGetComp<HediffComp_GetsPermanent>();
                    if (perm != null) { perm.IsPermanent = true; perm.SetPainCategory(PainCategory.Painless); }
                    p.health.AddHediff(burn, part);
                }
            }
        }
    }
}
