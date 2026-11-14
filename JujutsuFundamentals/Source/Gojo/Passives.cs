using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEF.Hediffs;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace GojoPsycasts
{
    public enum RedMode { Orb, Beam }

    public class HediffCompProperties_Limitless : HediffCompProperties
    {
        public HediffCompProperties_Limitless() => compClass = typeof(HediffComp_Limitless);
    }

    /// <summary>
    /// The Six Eyes hediff. Keeps the passive nodes' hediffs matched to the tree,
    /// lets the eyes see through walls, and carries the mode switches for Red and
    /// Purple and the far teleport, which are one tree node each but more than one
    /// way to cast.
    /// </summary>
    public class HediffComp_Limitless : HediffComp
    {
        public RedMode red = RedMode.Orb;
        /// <summary>Hollow Purple with the full incantation (her call 2026-11-06): longer warmup, 200% Purple.</summary>
        public bool chant = true;
        private int acc;

        public const float SightRadius = 24.9f;

        /// <summary>Psyfocus back per second at psycast level 20: 60% of a full
        /// twenty-finger Sukuna's 0.0201 (her call 2026-10-31: less than Sukuna, but
        /// Six Eyes halves every cost, so he lasts longer). Scaled by level/20, so a
        /// young Six Eyes child gets little.</summary>
        public const float RegenPerSecond = 0.012f;

        public static HediffComp_Limitless Of(Pawn p) =>
            ((p?.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_Limitless)
              ?? (GojoDefOf.Gojo_LimitlessInnate != null ? p?.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_LimitlessInnate) : null)) as HediffWithComps)
                ?.TryGetComp<HediffComp_Limitless>();

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            acc += delta;
            if (acc < 120) return;
            acc = 0;
            var pawn = Pawn;
            if (pawn == null || pawn.Dead) return;
            bool sixEyes = Gojo.HasSixEyes(pawn), limitless = Gojo.HasLimitless(pawn);
            // a Six Eyes hediff on someone without the Six Eyes any more (gene removed, editor): it goes
            if (parent.def == GojoDefOf.Gojo_Limitless && !sixEyes) { pawn.health.RemoveHediff(parent); return; }
            if (sixEyes) Regen(pawn, 2f);   // the Six Eyes' efficiency
            var comp = pawn.GetComp<CompAbilities>();
            if (comp != null)
            {
                Match(pawn, comp, GojoAbilityDefOf.Gojo_Infinity, GojoDefOf.Gojo_Infinity);
                Match(pawn, comp, GojoAbilityDefOf.Gojo_BlueFist, GojoDefOf.Gojo_BlueFist);
                // Black Flash and Reinforcement are the shared ones now (Cursed Energy Fundamentals, 2026-11-10)
                if (limitless && !comp.HasAbility(GojoAbilityDefOf.Gojo_Fly)) comp.GiveAbility(GojoAbilityDefOf.Gojo_Fly);   // Fly is Limitless
                // Pull comes with Blue, Push with Red (her call 2026-11-04)
                if (comp.HasAbility(GojoAbilityDefOf.Gojo_Blue) && !comp.HasAbility(GojoAbilityDefOf.Gojo_Pull))
                    comp.GiveAbility(GojoAbilityDefOf.Gojo_Pull);
                if (comp.HasAbility(GojoAbilityDefOf.Gojo_Red) && !comp.HasAbility(GojoAbilityDefOf.Gojo_Push))
                    comp.GiveAbility(GojoAbilityDefOf.Gojo_Push);
                // Red: Beam comes with Red too (her call 2026-11-11: its own button, not a switch on Red)
                if (comp.HasAbility(GojoAbilityDefOf.Gojo_Red) && GojoAbilityDefOf.Gojo_RedBeam != null && !comp.HasAbility(GojoAbilityDefOf.Gojo_RedBeam))
                    comp.GiveAbility(GojoAbilityDefOf.Gojo_RedBeam);
                // RCT is a toggle now (2026-10-31); a save from before has it on permanently
                if (!GojoToggles.IsOn(pawn, GojoToggles.RCT))
                {
                    var rct = pawn.health.hediffSet.GetFirstHediffOfDef(GojoDefOf.Gojo_ReverseCursedTechnique);
                    if (rct != null) pawn.health.RemoveHediff(rct);
                }
            }
            if (sixEyes) SeeThroughWalls(pawn);
            BlindfoldToApparel(pawn);
        }

        /// <summary>Saves from before 2026-10-31 drew the blindfold as a face tattoo
        /// with a toggle button. Swap it for the clothing version, once.</summary>
        private static void BlindfoldToApparel(Pawn pawn)
        {
            var style = pawn.style;
            if (style == null || (style.FaceTattoo != GojoDefOf.Gojo_Blindfold && style.FaceTattoo != GojoDefOf.Gojo_Eyes)) return;
            // 2026-11-03: the painted-eyes tattoo is gone too; the Six Eyes hediff draws the eyes on any head
            if (style.FaceTattoo == GojoDefOf.Gojo_Blindfold) Satoru.WearBlindfold(pawn);
            style.FaceTattoo = TattooDefOf.NoTattoo_Face;
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }

        private static void Regen(Pawn pawn, float seconds)
        {
            var tr = pawn.psychicEntropy;
            var psy = Gojo.Psycasts(pawn, false);
            if (tr == null || psy == null || tr.CurrentPsyfocus >= 1f) return;
            float level = Mathf.Clamp(psy.level, 1, 20) / 20f;
            tr.OffsetPsyfocusDirectly(RegenPerSecond * level * seconds);
        }

        public override string CompTipStringExtra => "Psyfocus returns on its own.";

        private static void Match(Pawn pawn, CompAbilities comp, VEFAbilityDef node, HediffDef passive)
        {
            bool learned = comp.HasAbility(node);
            var present = pawn.health.hediffSet.GetFirstHediffOfDef(passive);
            if (learned && present == null) pawn.health.AddHediff(passive);
            else if (!learned && present != null) pawn.health.RemoveHediff(present);
        }

        /// <summary>Only for the player's Gojo: the fog is the player's knowledge.</summary>
        private static void SeeThroughWalls(Pawn pawn)
        {
            if (!pawn.Spawned || pawn.Faction != Faction.OfPlayer) return;
            var map = pawn.Map;
            foreach (var c in GenRadial.RadialCellsAround(pawn.Position, SightRadius, true))
                if (c.InBounds(map) && map.fogGrid.IsFogged(c)) map.fogGrid.Unfog(c);
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            var pawn = Pawn;
            if (pawn == null || pawn.Faction != Faction.OfPlayer) yield break;

            if (Gojo.Knows(pawn, GojoAbilityDefOf.Gojo_Purple))
                yield return new Command_Toggle
                {
                    defaultLabel = "Purple: Chant",
                    defaultDesc = "On: Gojo says the full incantation before Hollow Purple, as in the manga (about five seconds), and it comes out at 200%: "
                                + "twice as wide and half again as far.\nOff: the quick Purple, no chant.",
                    icon = GojoAbilityDefOf.Gojo_Purple.icon,
                    Order = 33f,
                    isActive = () => chant,
                    toggleAction = () => chant = !chant,
                };

            if (Gojo.Knows(pawn, GojoAbilityDefOf.Gojo_Teleport))
            {
                var cmd = new Command_Action
                {
                    defaultLabel = "Teleport: Far",
                    defaultDesc = "Teleport to any tile on the planet. Where there is a map, Gojo appears on it; anywhere else he travels on as a caravan of one.",
                    icon = GojoAbilityDefOf.Gojo_Teleport.icon,
                    Order = 35f,
                    action = () => Teleport.BeginFar(pawn),
                };
                if (!Teleport.CanAffordFar(pawn, out string why)) cmd.Disable(why);
                yield return cmd;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref red, "gojoRedMode", RedMode.Orb);
            Scribe_Values.Look(ref chant, "gojoPurpleChant", true);
        }
    }

    /// <summary>
    /// Infinity. VEF's shield stops the projectile or blow (HediffComp_Shield in
    /// VEF.dll: PreApplyDamage and ShieldActive are its only virtual hooks); this
    /// adds the price, a little neural heat per stopped attack, and the limit:
    /// once heat reaches its maximum the shield is down and attacks land, and it
    /// stays down until heat has cooled to <see cref="BackUpAt"/> of the maximum
    /// (2026-11-03: heat cools at once, so "only at max" lasted a split second).
    /// </summary>
    public class HediffComp_Infinity : HediffComp_Shield
    {
        public const float HeatPerBlock = 1.5f;
        public const float BackUpAt = 0.5f;
        private bool down;
        public const float PsyfocusBackAt = 0.05f;
        private bool noFocus;

        public override bool ShieldActive
        {
            get
            {
                // No energy pool (bug report 2026-11-14): VEF's shield also had one, 400 energy at 0.25 a point of damage,
                // so 1600 damage at once (or quickly) broke it and let the rest through whatever his psyfocus and heat.
                // Heat and psyfocus are Infinity's only limits; this also clears the pool in older saves.
                useEnergy = false;
                var tr = Pawn?.psychicEntropy;
                if (tr != null)
                {
                    if (tr.EntropyValue >= tr.MaxEntropy) down = true;
                    else if (down && tr.EntropyValue < tr.MaxEntropy * BackUpAt) down = false;
                    // out of psyfocus (her call 2026-11-03): down until a little has come back
                    if (tr.CurrentPsyfocus <= 0.001f) noFocus = true;
                    else if (noFocus && tr.CurrentPsyfocus >= PsyfocusBackAt) noFocus = false;
                }
                return base.ShieldActive && !down && !noFocus;
            }
        }

        /// <summary>Extra neural heat per point of damage for a stopped hit from Sukuna or Yuji (her call
        /// 2026-11-07: their techniques hit hard enough to strain Infinity).</summary>
        public const float HeatPerSukunaDamage = 0.1f;

        /// <summary>Extra neural heat per point of damage for any other stopped hit (her call 2026-11-14, from a bug report:
        /// a giant blow should strain Infinity more than a pebble). The number is a proposal.</summary>
        public const float HeatPerDamage = 0.05f;

        /// <summary>Neural heat for one stopped hit, before the Six Eyes halve it. amount is the damage before
        /// the shield zeroes it.</summary>
        public static float HeatFor(DamageDef def, float amount, Thing instigator)
        {
            bool theirs = (def != null && def.defName.StartsWith("Sukuna_")) || SukunaSide(instigator as Pawn);
            return HeatPerBlock + (theirs ? HeatPerSukunaDamage : HeatPerDamage) * Mathf.Max(0f, amount);
        }

        private static System.Func<Pawn, bool> isSukuna, isYuji, isModulo;
        private static bool lookedSide;

        /// <summary>Sukuna, Yuji or Modulo Yuji from the Sukuna mod (found by name; false without it).</summary>
        private static bool SukunaSide(Pawn p)
        {
            if (p == null) return false;
            if (!lookedSide)
            {
                lookedSide = true;
                isSukuna = Find("SukunaPsycasts.SukunaBehaviour", "IsSukuna");
                isYuji = Find("SukunaPsycasts.Yuji", "Is");
                isModulo = Find("SukunaPsycasts.ModuloYuji", "Is");
            }
            return (isSukuna != null && isSukuna(p)) || (isYuji != null && isYuji(p)) || (isModulo != null && isModulo(p));
        }

        private static System.Func<Pawn, bool> Find(string type, string method)
        {
            var t = HarmonyLib.AccessTools.TypeByName(type);
            var m = t == null ? null : HarmonyLib.AccessTools.Method(t, method, new[] { typeof(Pawn) });
            return m == null ? null : (System.Func<Pawn, bool>)System.Delegate.CreateDelegate(typeof(System.Func<Pawn, bool>), m);
        }

        private static System.Reflection.FieldInfo sureHit;
        private static bool lookedSureHit;

        /// <summary>From the Sukuna mod (her call 2026-11-07): World Cutting Slash and a Domain
        /// Expansion's sure hit go through Infinity. Benevolent Shrine wounds the soul directly, so it
        /// never reaches here.</summary>
        public static bool GetsThrough(DamageInfo dinfo)
        {
            if (dinfo.Def?.defName == "Sukuna_WorldSlash") return true;
            if (!lookedSureHit)
            {
                lookedSureHit = true;
                sureHit = HarmonyLib.AccessTools.Field(HarmonyLib.AccessTools.TypeByName("SukunaPsycasts.SureHit"), "Active");
            }
            return sureHit != null && (bool)sureHit.GetValue(null);
        }

        // Infinity is invisible (her call 2026-11-07): no shield circle around him at all
        public override void DrawAt(Vector3 drawPos) { }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref down, "gojoInfinityDown", false);
            Scribe_Values.Look(ref noFocus, "gojoInfinityNoFocus", false);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (GetsThrough(dinfo)) return;
            float amount = dinfo.Amount;   // before the shield sets it to 0 (2026-11-14: read after, so the damage part never counted)
            base.PreApplyDamage(ref dinfo, ref absorbed);
            if (!absorbed || dinfo.Instigator == Pawn) return;
            Pawn?.psychicEntropy?.TryAddEntropy(HeatFor(dinfo.Def, amount, dinfo.Instigator), Pawn, scale: true, overLimit: true);
        }
    }

    public class HediffCompProperties_BlackFlash : HediffCompProperties
    {
        public float baseChance = 0.06f;
        public float damageMultiplier = 2.5f;
        public float armorPenetrationBonus = 0.50f;
        public float armorPenetrationBonusMax = 1.00f;
        public float armorPenetrationCeiling = 1.90f;
        public float lifestealFraction = 0.30f;
        public float psyfocusRestored = 0.05f;
        public float entropyPurged = 12f;
        public HediffCompProperties_BlackFlash() => compClass = typeof(HediffComp_BlackFlash);
    }

    public class HediffComp_BlackFlash : HediffComp
    {
        public HediffCompProperties_BlackFlash Props => (HediffCompProperties_BlackFlash)props;
        public float ChanceFor(Pawn p) => Mathf.Clamp01(Props.baseChance * Gojo.Power(p));
        public float ArmorPenBonusFor(Pawn p) => Mathf.Min(Props.armorPenetrationBonus * Gojo.Power(p), Props.armorPenetrationBonusMax);
        public float PsyfocusFor(Pawn p) => Props.psyfocusRestored * Gojo.Power(p);
        public float EntropyPurgeFor(Pawn p) => Props.entropyPurged * Gojo.Power(p);
        public float LifestealFor(Pawn p) => Props.lifestealFraction * Gojo.Power(p);
    }

    public class HediffCompProperties_ReverseCursedTechnique : HediffCompProperties
    {
        public float selfHealPerTickAtFullPsyfocus = 0.09f;
        public float allyHealFraction = 0.35f;
        public float allyRadius = 3.9f;
        public float psyfocusPerTick = 0.00006f;
        public float entropyPerTick = 0.025f;
        public HediffCompProperties_ReverseCursedTechnique() => compClass = typeof(HediffComp_ReverseCursedTechnique);
    }

    /// <summary>Sukuna's reverse cursed technique with Gojo's numbers: speed follows
    /// the psyfocus pool, stops at zero psyfocus or maximum neural heat.</summary>
    public class HediffComp_ReverseCursedTechnique : HediffComp
    {
        private const int Interval = 60;
        private int acc;
        public HediffCompProperties_ReverseCursedTechnique Props => (HediffCompProperties_ReverseCursedTechnique)props;
        public bool Running { get; private set; }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            acc += delta;
            if (acc < Interval) return;
            int steps = acc / Interval;
            acc %= Interval;
            var pawn = Pawn;
            Running = false;
            if (pawn == null || pawn.Dead || pawn.Map == null) return;
            var tr = pawn.psychicEntropy;
            if (tr == null) return;
            float psyfocus = tr.CurrentPsyfocus;
            if (psyfocus <= 0.001f || tr.EntropyValue >= tr.MaxEntropy) return;

            var wounds = Wounds(pawn);
            var lost = wounds.Count == 0 ? NextMissing(pawn) : null;
            if (wounds.Count == 0 && lost == null && !AlliesHurt(pawn)) return;
            Running = true;
            float amount = Props.selfHealPerTickAtFullPsyfocus * psyfocus * Gojo.Power(pawn) * steps;
            // Wounds first; once they are closed, lost parts grow back (her call 2026-11-02),
            // the part closest to the body first, bringing its children with it.
            if (lost != null)
            {
                regrow += amount;
                if (regrow >= lost.Part.def.GetMaxHealth(pawn) * 1.5f)
                {
                    regrow = 0f;
                    var part = lost.Part;
                    pawn.health.RestorePart(part, null, true);
                    if (pawn.Spawned) MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, part.LabelCap + " regrown", 3f);
                }
            }
            else HealWorst(wounds, amount);
            foreach (var t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.allyRadius, true))
                if (t is Pawn o && o != pawn && !o.Dead && o.Faction == pawn.Faction)
                    HealWorst(Wounds(o), amount * Props.allyHealFraction);
            // poison makes healing cost more (her call 2026-11-04)
            float cost = Poison.RctCostFactor(pawn);
            tr.OffsetPsyfocusDirectly(-Props.psyfocusPerTick * steps * cost);
            tr.TryAddEntropy(Props.entropyPerTick * steps * cost, pawn, scale: false, overLimit: false);
        }

        private float regrow;

        private static Hediff_MissingPart NextMissing(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>().Where(h => h.Part != null)
             .OrderBy(h => { int d = 0; for (var x = h.Part.parent; x != null; x = x.parent) d++; return d; })
             .FirstOrDefault();

        private bool AlliesHurt(Pawn pawn)
        {
            foreach (var t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.allyRadius, true))
                if (t is Pawn o && o != pawn && !o.Dead && o.Faction == pawn.Faction && Wounds(o).Count > 0) return true;
            return false;
        }

        private static List<Hediff_Injury> Wounds(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Where(h => h.Visible && !h.IsPermanent())
             .OrderByDescending(h => h.Severity).ToList();

        private static void HealWorst(List<Hediff_Injury> wounds, float amount)
        {
            foreach (var w in wounds)
            {
                if (amount <= 0f) break;
                float take = Mathf.Min(amount, w.Severity);
                w.Heal(take);
                amount -= take;
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                string s = Running ? "Repairing." : "Idle: no wounds, or out of psyfocus, or neural heat at its limit.";
                float c = Poison.RctCostFactor(Pawn);
                return c > 1f ? s + $"\nPoisoned: healing costs x{c:0.0}." : s;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref acc, "gojoRctAcc", 0);
            Scribe_Values.Look(ref regrow, "gojoRctRegrow", 0f);
        }
    }

    /// <summary>
    /// Pawn.PreApplyDamage sees melee and psycast damage before armour is rolled:
    ///   1. Gojo is immune to his own techniques.
    ///   2. Blue Fist: his unarmed blows pull the target into them, for more damage
    ///      and a stagger.
    ///   3. Black Flash.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_PreApplyDamage
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (dinfo.Instigator == __instance && (dinfo.Def == GojoDefOf.Gojo_Force || dinfo.Def == GojoDefOf.Gojo_Erase))
            {
                absorbed = true;
                return false;   // vanilla would set absorbed back to false
            }
            if (!(dinfo.Instigator is Pawn attacker) || attacker == __instance) return true;
            var set = attacker.health?.hediffSet;
            if (set == null) return true;

            bool unarmed = dinfo.Weapon == null || dinfo.Weapon == ThingDefOf.Human || dinfo.Weapon.race != null;
            if (unarmed && dinfo.Def.isRanged == false && dinfo.Def != GojoDefOf.Gojo_Force && dinfo.Def != GojoDefOf.Gojo_Erase
                && set.HasHediff(GojoDefOf.Gojo_BlueFist))
            {
                dinfo.SetAmount(dinfo.Amount * (1.5f + 0.25f * Gojo.Power(attacker)));
                __instance.stances?.stagger?.StaggerFor(95);
                Fx.Fleck(GojoDefOf.Gojo_Fleck_BlueSwirl, __instance.DrawPos, __instance.Map, 1.2f);
            }

            // Black Flash: the shared one (JujutsuFundamentals.BlackFlashHit) since 2026-11-10
            return true;
        }

        /// <summary>Black Flash lifesteal (her call 2026-11-02): heals this much, worst injury first, instead of every wound.</summary>
        public static void Lifesteal(Pawn p, float amount)
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

    /// <summary>The Black Flash roll and what it does, for any target: a pawn (Pawn.PreApplyDamage) or a wall,
    /// door or any other object (Thing.TakeDamage below). It depends on the striker, not on the target's
    /// cursed energy (her note 2026-11-07).</summary>
    public static class BlackFlashHit
    {
        public static void Try(Pawn attacker, Thing target, ref DamageInfo dinfo)
        {
            var comp = (attacker.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_BlackFlash) as HediffWithComps)?.TryGetComp<HediffComp_BlackFlash>();
            if (comp == null || !BlackFlashRules.Allowed(dinfo)
                || !Rand.Chance(BlackFlashRules.Scale(comp.ChanceFor(attacker), attacker))) return;

            float dealt = dinfo.Amount * comp.Props.damageMultiplier;
            dinfo.SetAmount(dealt);
            try
            {
                ref float ap = ref AccessTools.StructFieldRefAccess<DamageInfo, float>(ref dinfo, "armorPenetrationInt");
                ap = Mathf.Min(ap + comp.ArmorPenBonusFor(attacker), comp.Props.armorPenetrationCeiling);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Satoru Gojo] Black Flash could not write armour penetration: " + e.Message, 0x60702);
            }
            Patch_PreApplyDamage.Lifesteal(attacker, dealt * comp.LifestealFor(attacker));
            attacker.psychicEntropy?.OffsetPsyfocusDirectly(comp.PsyfocusFor(attacker));
            attacker.psychicEntropy?.TryAddEntropy(-comp.EntropyPurgeFor(attacker), attacker, false, false);
            Fx.BlackSparks(target);
            BlackFlashFx.Landed(attacker, target);
        }
    }

    /// <summary>Black Flash on objects: walls, doors, furniture (Pawns go through Pawn.PreApplyDamage).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_ThingTakeDamage_BlackFlash
    {
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (__instance is Pawn || !(dinfo.Instigator is Pawn attacker) || !__instance.def.useHitPoints) return;
            // Black Flash: the shared one (JujutsuFundamentals.BlackFlashHit) since 2026-11-10
        }
    }
}
