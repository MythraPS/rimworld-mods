using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    internal static class DismantleHit
    {
        internal static void HitDismantle(Pawn caster, Thing target)
        {
            if (target == null || target.Destroyed) return;
            // x1.5 against anything holding no cursed energy of its own — and plants
            // are pure matter, so the cut goes through them hardest.
            float mult = Ability_Dismantle.NoCursedEnergy(target) ? 1.5f : 1f;
            if (target.def.category == ThingCategory.Plant) mult = 6f;
            float dmg = Scaling.Damage(caster, 12f) * mult * Scaling.AbilityBoost(caster);
            float ap  = Scaling.ArmorPen(caster, Scaling.DismantleBaseAP,
                                          Scaling.DismantleCapAP, dmg);
            target.TakeDamage(new DamageInfo(SukunaDefOf.Sukuna_Dismantle, dmg, ap, -1f, caster));
        }

    }

    /// <summary>
    /// The path gate. PsycasterPathDef has exactly five lock fields — requiredGene,
    /// requiredMeme, requiredFocus, requiredMechanitor, requiredBackstoriesAny — and
    /// no requiredHediff, so this cannot be done in XML. CanPawnUnlock is public, so
    /// one postfix does it.
    /// </summary>
    [HarmonyPatch(typeof(PsycasterPathDef), nameof(PsycasterPathDef.CanPawnUnlock))]
    public static class Patch_CanPawnUnlock
    {
        public static void Postfix(PsycasterPathDef __instance, Pawn pawn, ref bool __result)
        {
            if (__instance != SukunaDefOf.Sukuna_Path) return;
            // No Shrine without at least one finger swallowed (her call 2026-11-01); the imprint stays
            // after the fingers leave, so Yuji keeps learning it once Sukuna is out of him.
            __result = Scaling.HasVessel(pawn) || ModuloYuji.Is(pawn)
                       || pawn?.health?.hediffSet?.HasHediff(SukunaArcDefOf.Sukuna_ShrineImprint) == true
                       || JujutsuFundamentals.Innate.Marked(pawn, JujutsuFundamentals.Innate.Shrine);   // born with it (2026-11-12)
        }
    }

    /// <summary>
    /// Caster immunity plus the Black Flash proc. Pawn.PreApplyDamage is the one
    /// place that sees BOTH melee and psycast damage, and it runs before
    /// ArmorUtility.GetPostArmorDamage — which is the only place penetration is read.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_PreApplyDamage
    {
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            // 0. Yuji's reverse cursed technique regrows what the Shrine cuts almost as it is cut
            //    (her call 2026-11-01: strong enough to stand in Malevolent Shrine).
            if ((dinfo.Def == SukunaDefOf.Sukuna_Slash || dinfo.Def == SukunaDefOf.Sukuna_Dismantle) && Yuji.Is(__instance)
                && __instance.health.hediffSet.HasHediff(JujutsuFundamentals.JJKDefOf.JJK_ReverseCursedTechnique))
                dinfo.SetAmount(dinfo.Amount * YujiRct.SlashTaken);

            // 1. Caster immunity: our own damage never touches the one who made it, nor fire they started.
            if (dinfo.Instigator == __instance && (IsOurs(dinfo.Def) || OwnFire.Is(__instance, dinfo)))
            {
                absorbed = true;
                return false;   // vanilla would set absorbed back to false
            }

            // 1b. Fire started by your own Fire Arrow has the FIRE as its instigator,
            //     not you, so the check above never caught it — which is how a caster
            //     burned to death in their own furnace. Cover heat outright while the
            //     blast window is open.
            if (SlashUtility.HasFurnaceImmunity(__instance)
                && (dinfo.Def == SukunaDefOf.Sukuna_Flame
                    || dinfo.Def == DamageDefOf.Flame
                    || dinfo.Def == DamageDefOf.Burn))
            {
                // Put them out too, or they keep burning after the window closes.
                var fire = __instance.GetAttachment(ThingDefOf.Fire) as Fire;
                fire?.Destroy();
                absorbed = true;
                return false;   // vanilla would set absorbed back to false
            }

            // 2. Black Flash.
            // 2. Black Flash: the shared one (JujutsuFundamentals.BlackFlashHit) since 2026-11-10.
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

        private static bool IsOurs(DamageDef def) =>
            def == SukunaDefOf.Sukuna_Slash
            || def == SukunaDefOf.Sukuna_Dismantle
            || def == SukunaDefOf.Sukuna_WorldSlash
            || def == SukunaDefOf.Sukuna_Flame;
    }

    /// <summary>
    /// Damage arrives WITH the slash, not at cast time. Dismantle's scaled damage and
    /// Fire Arrow's single detonation both land here, on impact.
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "Impact")]
    public static class Patch_ProjectileImpact
    {
        public static void Prefix(Projectile __instance, Thing hitThing)
        {
            var caster = __instance.Launcher as Pawn;
            if (caster == null) return;

            if (__instance.def == SukunaThingDefOf.Sukuna_DismantleSlash)
            {
                // A slash aimed at the ground still cuts whatever is lying there.
                // hitThing is null for a cell hit, which is why loose items and
                // plants were being targeted but never damaged.
                if (hitThing == null)
                {
                    var cell = __instance.Position;
                    var map = __instance.Map;
                    if (map == null || !cell.InBounds(map)) return;
                    foreach (var t in cell.GetThingList(map).ToList())
                    {
                        if (t == caster || t.Destroyed) continue;
                        if (t.def.category != ThingCategory.Item
                            && t.def.category != ThingCategory.Plant
                            && t.def.category != ThingCategory.Building) continue;
                        DismantleHit.HitDismantle(caster, t);
                    }
                    SlashUtility.SlashFleckAngled(cell.ToVector3Shifted(), map,
                        (__instance.Position - caster.Position).ToVector3().AngleFlat());
                    return;
                }
                if (hitThing.Destroyed) return;
                if (hitThing is Corpse corpse) { SlashUtility.ButcherCorpse(corpse, caster); return; }

                if (hitThing.Map != null)
                    SlashUtility.SlashFleckAngled(hitThing.DrawPos, hitThing.Map,
                        (hitThing.Position - caster.Position).ToVector3().AngleFlat());
                DismantleHit.HitDismantle(caster, hitThing);
                if (hitThing is Pawn hp) SlashUtility.NotifyAttacked(hp, caster);
            }
            else if (__instance.def == SukunaThingDefOf.Sukuna_FireArrow)
            {
                Ability_FireArrow.Detonate(caster, __instance.Position, __instance.Map, Ability_FireArrow.BlastRadius(caster));
            }
        }
    }

    /// <summary>Records Cleave and Dismantle casts (ComboTracker; nothing gates on it now).</summary>
    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.Cast), new Type[] { typeof(GlobalTargetInfo[]) })]
    public static class Patch_AbilityCast
    {
        public static void Postfix(VEFAbility __instance)
        {
            var caster = __instance.pawn;
            if (caster == null) return;
            if (__instance.def == SukunaAbilityDefOf.Sukuna_Cleave)    ComboTracker.NoteCleave(caster);
            if (__instance.def == SukunaAbilityDefOf.Sukuna_Dismantle) ComboTracker.NoteDismantle(caster);
        }
    }

    /// <summary>A vessel who dies drops every finger they ate. Fingers are never
    /// destroyed — they move on to whoever picks them up.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_PawnKill
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, out (int fingers, bool byYuji) __state)
        {
            var killer = dinfo?.Instigator as Pawn;
            __state = (Scaling.FingersEaten(__instance),
                       SukunaBehaviour.IsSukuna(__instance) && killer != null && killer != __instance && Yuji.Is(killer));
        }

        public static void Postfix(Pawn __instance, (int fingers, bool byYuji) __state)
        {
            if (__state.fingers <= 0) return;
            var vessel = __instance.health?.hediffSet?.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);

            // Yuji finishing off the King destroys the fingers with him (her call 2026-11-02).
            if (__state.byYuji)
            {
                if (vessel != null) __instance.health.RemoveHediff(vessel);
                SukunaArc.YujiKilled(__instance, __state.fingers);
                return;
            }

            var map = __instance.MapHeld;
            var cell = __instance.PositionHeld;
            if (map == null || !cell.IsValid) return;

            for (int i = 0; i < __state.fingers; i++)
            {
                var finger = ThingMaker.MakeThing(SukunaThingDefOf.Sukuna_Finger);
                GenPlace.TryPlaceThing(finger, cell, map, ThingPlaceMode.Near);
            }

            // The fingers left the body, so the body no longer has them. Without this
            // a resurrected vessel kept all twenty AND the twenty on the ground.
            if (vessel != null) __instance.health.RemoveHediff(vessel);
        }
    }

    /// <summary>The Black Flash roll and what it does, for any target: a pawn (Pawn.PreApplyDamage) or a wall,
    /// door or any other object (Thing.TakeDamage below). It depends on the striker, not on the target's
    /// cursed energy (her note 2026-11-07).</summary>
    public static class BlackFlashHit
    {
        public static void Try(Pawn attacker, Thing target, ref DamageInfo dinfo)
        {
            var hediff = attacker.health?.hediffSet?.GetFirstHediffOfDef(SukunaDefOf.Sukuna_BlackFlash);
            var comp = (hediff as HediffWithComps)?.TryGetComp<HediffComp_BlackFlash>();
            // Yuji has it whether or not he learned it, and far more often than anyone (her call 2026-11-02).
            if (comp == null && Yuji.Is(attacker)) comp = YujiBlackFlash.Comp(attacker);
            if (comp == null || !BlackFlashRules.Allowed(dinfo)) return;
            float bfChance = YujiBlackFlash.Chance(attacker, comp);
            if (bfChance < 1f) bfChance = BlackFlashRules.Scale(bfChance, attacker);   // Modulo Yuji's "always" toggle stays always
            if (!Rand.Chance(bfChance)) return;

            float dealt = dinfo.Amount * comp.Props.damageMultiplier;
            dinfo.SetAmount(dealt);

            // armorPenetrationInt is PRIVATE with no public setter, so it has to be
            // written by reference. Flat bonus, applied on top of the per-ability cap
            // and then clamped just short of the 2.0 that means "complete".
            try
            {
                ref float ap = ref AccessTools.StructFieldRefAccess<DamageInfo, float>(
                                   ref dinfo, "armorPenetrationInt");
                ap = Mathf.Min(ap + comp.ArmorPenBonusFor(attacker),
                               comp.Props.armorPenetrationCeiling);
            }
            catch (Exception e)
            {
                Log.WarningOnce("[Ryomen Sukuna] Black Flash could not write armour "
                                + "penetration: " + e.Message, 0x5B1A55);
            }

            // Lifesteal, psyfocus back, neural heat purged — all scale with sensitivity.
            Patch_PreApplyDamage.Lifesteal(attacker, dealt * comp.LifestealFor(attacker));
            attacker.psychicEntropy?.OffsetPsyfocusDirectly(comp.PsyfocusFor(attacker));
            if (attacker.psychicEntropy != null)
                attacker.psychicEntropy.TryAddEntropy(-comp.EntropyPurgeFor(attacker),
                                                      attacker, false, false);

            // Black sparks spreading out of the impact.
            SlashUtility.BlackSparks(target);
            if (target is Pawn victim) BlackFlashLog.Mark(victim);
            BlackFlashFx.Landed(attacker, target);
        }
    }

    /// <summary>Black Flash on objects: walls, doors, furniture (Pawns go through Pawn.PreApplyDamage).</summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_ThingTakeDamage_BlackFlash
    {
        public const float ModuloWallFactor = 6f;
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (__instance is Pawn || !(dinfo.Instigator is Pawn attacker) || !__instance.def.useHitPoints) return;
            // Modulo Yuji's blows go through walls (her note 2026-11-07: too many hits, even with Black Flash)
            if (__instance is Building && !dinfo.Def.isRanged && !dinfo.Def.isExplosive && ModuloYuji.Is(attacker))
                dinfo.SetAmount(dinfo.Amount * ModuloWallFactor);
        }
    }
}
