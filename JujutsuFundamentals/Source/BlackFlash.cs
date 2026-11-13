using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JujutsuFundamentals
{
    public class HediffCompProperties_BlackFlash : HediffCompProperties
    {
        public float baseChance = 0.03f;
        public float damageMultiplier = 2.5f;
        public float armorPenetrationBonus = 0.50f;
        public float armorPenetrationBonusMax = 1.00f;
        public float armorPenetrationCeiling = 1.90f;
        public float lifestealFraction = 0.30f;
        public float psyfocusRestored = 0.05f;
        public float entropyPurged = 12f;

        public HediffCompProperties_BlackFlash() => compClass = typeof(HediffComp_BlackFlash);
    }

    /// <summary>Passive. Holds the numbers; the roll happens in BlackFlashHit.</summary>
    public class HediffComp_BlackFlash : HediffComp
    {
        public HediffCompProperties_BlackFlash Props => (HediffCompProperties_BlackFlash)props;
    }

    /// <summary>The Black Flash settings and chance rules (from Sukuna's and Gojo's versions).</summary>
    public static class BlackFlashRules
    {
        /// <summary>True while a melee attack is landing (set around Verb_MeleeAttack.TryCastShot).</summary>
        public static int meleeDepth;
        public static bool InMelee => meleeDepth > 0;

        public static float Multiplier => JujutsuMod.Settings?.blackFlashChance ?? 1f;

        public static bool Allowed(DamageInfo dinfo)
        {
            if (Multiplier <= 0f) return false;
            if (dinfo.Weapon != null && dinfo.Weapon.IsRangedWeapon && !InMelee) return false;
            if (InMelee) return true;
            return JujutsuMod.Settings?.blackFlashAbilities ?? false;
        }

        /// <summary>Rare at first, more often as the psycaster grows: x0.25 at psycast level 1, x0.6 at 10,
        /// x1 at 20, x1.5 at 30. No psylink counts as level 1.</summary>
        public static readonly SimpleCurve LevelFactor = new SimpleCurve
        {
            new CurvePoint(1f, 0.25f), new CurvePoint(10f, 0.6f), new CurvePoint(20f, 1f), new CurvePoint(30f, 1.5f)
        };

        public static int PsycastLevel(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 1;
            foreach (var h in set.hediffs)
                if (h is VanillaPsycastsExpanded.Hediff_PsycastAbilities psy) return Mathf.Max(1, psy.level);
            return 1;
        }

        public static float Chance(Pawn p, HediffCompProperties_BlackFlash props)
        {
            if (JujutsuDebug.AlwaysBlackFlash) return 1f;
            foreach (var f in Jujutsu.BlackFlashChanceOverride)
            {
                float forced = f(p);
                if (forced >= 0f) return forced;
            }
            float c = props.baseChance * Jujutsu.Power(p) * Jujutsu.Product(Jujutsu.BlackFlashChanceFactors, p);
            return Mathf.Clamp01(c * Multiplier * LevelFactor.Evaluate(PsycastLevel(p)) * (BlackFlashFx.InZone(p) ? BlackFlashFx.ZoneChance : 1f));
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_BlackFlashMeleeContext
    {
        public static void Prefix() => BlackFlashRules.meleeDepth++;
        public static void Finalizer() => BlackFlashRules.meleeDepth = Mathf.Max(0, BlackFlashRules.meleeDepth - 1);
    }

    /// <summary>What a Black Flash looks and sounds like: its own impact sound, a black-and-red burst,
    /// "Black flash!" over the target, and a few seconds "in the zone" for the one who landed it.</summary>
    public static class BlackFlashFx
    {
        public const float ZoneChance = 1.5f;
        private static SoundDef sound; private static FleckDef burst;
        private static SoundDef Sound => sound ?? (sound = DefDatabase<SoundDef>.GetNamedSilentFail("JJK_Snd_BlackFlash"));
        private static FleckDef Burst => burst ?? (burst = DefDatabase<FleckDef>.GetNamedSilentFail("JJK_Fleck_BlackFlashBurst"));

        public static bool InZone(Pawn p) => Jujutsu.Has(p, JJKDefOf.JJK_BlackFlashZone);

        public static void Landed(Pawn attacker, Thing victim)
        {
            var map = victim?.Map;
            if (map == null) return;
            Sound?.PlayOneShot(new TargetInfo(victim.Position, map));
            if (Burst != null)
            {
                var data = FleckMaker.GetDataStatic(victim.DrawPos, map, Burst, 1.6f);
                data.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(data);
            }
            MoteMaker.ThrowText(victim.DrawPos + new Vector3(0f, 0f, 0.6f), map, "Black flash!", new Color(0.9f, 0.12f, 0.15f), 2.5f);
            if (attacker != null && !attacker.Dead && JJKDefOf.JJK_BlackFlashZone != null)
            {
                var h = attacker.health.hediffSet.GetFirstHediffOfDef(JJKDefOf.JJK_BlackFlashZone);
                if (h != null) attacker.health.RemoveHediff(h);   // a fresh 10 seconds
                attacker.health.AddHediff(JJKDefOf.JJK_BlackFlashZone);
            }
        }
    }

    /// <summary>The Black Flash roll, for any target: a pawn (Pawn.PreApplyDamage) or an object (Thing.TakeDamage).</summary>
    public static class BlackFlashHit
    {
        /// <summary>Fired after a Black Flash lands, for mods that add their own touches (Sukuna's black sparks).</summary>
        public static readonly List<Action<Pawn, Thing>> Landed = new List<Action<Pawn, Thing>>();

        private static HediffCompProperties_BlackFlash defaults;
        private static HediffCompProperties_BlackFlash Defaults =>
            defaults ?? (defaults = JJKDefOf.JJK_BlackFlash?.CompProps<HediffCompProperties_BlackFlash>());

        public static HediffCompProperties_BlackFlash PropsFor(Pawn p)
        {
            var hediff = p.health?.hediffSet?.GetFirstHediffOfDef(JJKDefOf.JJK_BlackFlash) as HediffWithComps;
            var comp = hediff?.TryGetComp<HediffComp_BlackFlash>();
            if (comp != null) return comp.Props;
            return Jujutsu.Any(Jujutsu.BlackFlashInnate, p) ? Defaults : null;
        }

        public static void Try(Pawn attacker, Thing target, ref DamageInfo dinfo)
        {
            if (ZeroCursedEnergy.Is(attacker)) return;   // no cursed energy, no Black Flash (Yuji before his first finger)
            var props = PropsFor(attacker);
            if (props == null || !BlackFlashRules.Allowed(dinfo)) return;
            if (!Rand.Chance(BlackFlashRules.Chance(attacker, props))) return;

            float power = Jujutsu.Power(attacker);
            float dealt = dinfo.Amount * props.damageMultiplier;
            dinfo.SetAmount(dealt);
            try
            {
                // armorPenetrationInt is private with no setter: written by reference, flat bonus, kept short of 2.0 ("complete")
                ref float ap = ref AccessTools.StructFieldRefAccess<DamageInfo, float>(ref dinfo, "armorPenetrationInt");
                ap = Mathf.Min(ap + Mathf.Min(props.armorPenetrationBonus * power, props.armorPenetrationBonusMax), props.armorPenetrationCeiling);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[JJK Core] Black Flash could not write armour penetration: " + e.Message, 0x4A4B01);
            }
            Heal.Worst(attacker, dealt * props.lifestealFraction * power);
            attacker.psychicEntropy?.OffsetPsyfocusDirectly(props.psyfocusRestored * power);
            attacker.psychicEntropy?.TryAddEntropy(-props.entropyPurged * power, attacker, false, false);
            BlackFlashFx.Landed(attacker, target);
            foreach (var a in Landed) a(attacker, target);
        }
    }

    public static class Heal
    {
        /// <summary>Heals this much, worst injury first.</summary>
        public static void Worst(Pawn p, float amount)
        {
            var hs = p?.health?.hediffSet;
            if (hs == null || amount <= 0f) return;
            var wounds = new List<Hediff_Injury>();
            hs.GetHediffs(ref wounds, h => !h.IsPermanent());
            wounds.Sort((a, b) => b.Severity.CompareTo(a.Severity));
            foreach (var w in wounds)
            {
                if (amount <= 0f) break;
                float d = Mathf.Min(amount, w.Severity);
                w.Heal(d);
                amount -= d;
            }
        }
    }

    /// <summary>Black Flash on walls, doors and other objects (pawns go through Pawn.PreApplyDamage).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_ThingTakeDamage_BlackFlash
    {
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (__instance is Pawn || !(dinfo.Instigator is Pawn attacker) || !__instance.def.useHitPoints) return;
            BlackFlashHit.Try(attacker, __instance, ref dinfo);
        }
    }
}
