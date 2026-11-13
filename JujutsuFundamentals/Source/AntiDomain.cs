using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VEF.Abilities;
using VEF.Hediffs;

using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>A Domain Expansion's sure hit. A domain sets Active around the damage it deals, and asks
    /// Blocked(victim) before anything else it does to a pawn (Gojo's stun). Anti-domain techniques block it.</summary>
    public static class SureHit
    {
        public static bool Active;

        /// <summary>True if this pawn is shielded from a domain's sure hit: Hollow Wicker Basket, inside a
        /// Simple Domain (own or an ally's), or Domain Amplification on.</summary>
        public static bool Blocked(Pawn victim) =>
            victim != null && (Protection.Shielded(victim, null) || Jujutsu.Has(victim, JJKDefOf.JJK_DomainAmplification)
                                || ZeroCursedEnergy.Is(victim));   // zero cursed energy: a sure hit can't lock on (canon)
    }

    /// <summary>
    /// Hollow Wicker Basket and Simple Domain (her calls 2026-11-10; narrowed 2026-11-11 to sure hits only, as in canon):
    /// a domain's sure hit doesn't affect the pawn. Hostile psycasts get through unless Evening Moon cuts them (Parried). The Basket covers its user; a Simple
    /// Domain covers everyone of the user's side (their faction and allied factions) inside its circle.
    /// </summary>
    public static class Protection
    {
        /// <summary>Is <paramref name="victim"/> inside an Evening Moon circle (a Simple Domain whose user knows Evening
        /// Moon) against this hostile caster? Her call 2026-11-11: it cuts down every projectile, psycasts included.
        /// That is the only thing that stops a hostile psycast now; Hollow Wicker Basket and Simple Domain alone only stop sure hits.</summary>
        public static bool Parried(Pawn victim, Thing caster)
        {
            if (victim == null || caster == null || caster == victim || !victim.Spawned) return false;
            if (!(caster is Pawn cp) || !cp.HostileTo(victim)) return false;
            var sd = EveningMoonStrike.Guard(victim.Position, victim.Map, caster);
            return sd != null && sd.pawn != caster;
        }

        /// <summary>Is <paramref name="victim"/> covered against <paramref name="caster"/>? A null caster means a domain.</summary>
        public static bool Shielded(Pawn victim, Thing caster)
        {
            if (victim == null || caster == victim) return false;
            if (caster is Pawn cp && cp.Faction != null && victim.Faction == cp.Faction) return false;   // allies' psycasts get through
            if (Jujutsu.Has(victim, JJKDefOf.JJK_HollowWickerBasket)) return true;
            if (!victim.Spawned) return false;
            foreach (var sd in Hediff_SimpleDomain.Active)
            {
                var user = sd.pawn;
                if (user == null || user.Map != victim.Map || !user.Spawned) continue;
                // the user's side: their own faction and allied factions (her call 2026-11-12)
                if (user != victim && (user.HostileTo(victim) || (user.Faction != null && user.Faction != victim.Faction
                    && (victim.Faction == null || user.Faction.RelationKindWith(victim.Faction) != FactionRelationKind.Ally)))) continue;
                if (victim.Position.InHorDistOf(user.Position, sd.Radius)) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Who is casting a psycast right now. Every Cast override of every VEF ability is wrapped at startup,
    /// and projectiles and explosions made during a cast carry the caster to where they land, so an area
    /// psycast's damage, stuns, harmful hediffs and mental states can be blocked on shielded pawns.
    /// </summary>
    public static class PsycastContext
    {
        private static readonly List<Thing> casters = new List<Thing>();
        private static readonly Dictionary<int, Thing> carriers = new Dictionary<int, Thing>();

        public static Thing Caster => casters.Count > 0 ? casters[casters.Count - 1] : null;

        public static void Push(Thing caster) => casters.Add(caster);
        public static void Pop() { if (casters.Count > 0) casters.RemoveAt(casters.Count - 1); }

        public static void Carry(Thing t)
        {
            var c = Caster;
            if (c != null && t != null) carriers[t.thingIDNumber] = c;
        }

        public static Thing CarriedBy(Thing t) => t != null && carriers.TryGetValue(t.thingIDNumber, out var c) ? c : null;
        public static void Forget(Thing t) { if (t != null) carriers.Remove(t.thingIDNumber); }

        /// <summary>Her call 2026-11-11 (canon: anti-domain techniques only stop sure hits; Kusakabe parried
        /// Dismantle with Evening Moon, ch. 254): hostile psycasts are stopped only by an Evening Moon circle.</summary>
        public static bool BlocksOn(Pawn victim) => Caster != null && Protection.Parried(victim, Caster);

        public static void PatchCasts(Harmony h)
        {
            var pre = new HarmonyMethod(typeof(PsycastContext), nameof(CastPrefix));
            var fin = new HarmonyMethod(typeof(PsycastContext), nameof(CastFinalizer));
            int n = 0;
            var types = new List<Type> { typeof(Ability) };
            types.AddRange(GenTypes.AllSubclasses(typeof(Ability)));
            foreach (var t in types)
            {
                var m = t.GetMethod("Cast", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null,
                                    new[] { typeof(RimWorld.Planet.GlobalTargetInfo[]) }, null);
                if (m == null || m.IsAbstract) continue;
                try { h.Patch(m, prefix: pre, finalizer: fin); n++; }
                catch (Exception e) { Log.Warning($"[JJK Core] could not watch {t.FullName}.Cast: {e.Message}"); }
            }
            if (Prefs.DevMode) Log.Message($"[JJK Core] watching {n} psycast Cast methods.");
        }

        public static void CastPrefix(Ability __instance) => Push(__instance.pawn);
        public static void CastFinalizer() => Pop();

        /// <summary>A new or loaded game starts with nothing carried (StaticReset).</summary>
        public static void Reset() { casters.Clear(); carriers.Clear(); }

        /// <summary>Bullets and VEF's ability projectiles call base Projectile.Impact first and deal their damage after it
        /// returns, so a patch on the base method alone pops the caster before the hit (checked in the 1.6 IL). Every
        /// Impact override is wrapped instead; nested wraps just push the same caster twice.</summary>
        public static void PatchImpacts(Harmony h)
        {
            var pre = new HarmonyMethod(typeof(Patch_ProjectileImpact_Context), nameof(Patch_ProjectileImpact_Context.Prefix));
            var fin = new HarmonyMethod(typeof(Patch_ProjectileImpact_Context), nameof(Patch_ProjectileImpact_Context.Finalizer));
            var types = new List<Type> { typeof(Projectile) };
            types.AddRange(GenTypes.AllSubclasses(typeof(Projectile)));
            foreach (var t in types)
            {
                if (t.ContainsGenericParameters) continue;
                var m = t.GetMethod("Impact", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                                    null, new[] { typeof(Thing), typeof(bool) }, null);
                if (m == null || m.IsAbstract) continue;
                try { h.Patch(m, prefix: pre, finalizer: fin); }
                catch (Exception e) { Log.Warning($"[JJK Core] could not watch {t.FullName}.Impact: {e.Message}"); }
            }
        }
    }

    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch),
        new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })]
    public static class Patch_ProjectileLaunch_Carry
    {
        public static void Postfix(Projectile __instance) => PsycastContext.Carry(__instance);
    }

    // patched on Projectile.Impact and every override of it by PsycastContext.PatchImpacts
    public static class Patch_ProjectileImpact_Context
    {
        public static void Prefix(Projectile __instance, out bool __state)
        {
            var c = PsycastContext.CarriedBy(__instance);
            __state = c != null;
            if (__state) PsycastContext.Push(c);
        }

        public static void Finalizer(Projectile __instance, bool __state)
        {
            if (__state) PsycastContext.Pop();
            PsycastContext.Forget(__instance);
        }
    }

    [HarmonyPatch(typeof(Explosion), nameof(Explosion.StartExplosion))]
    public static class Patch_ExplosionStart_Carry
    {
        public static void Prefix(Explosion __instance) => PsycastContext.Carry(__instance);
    }

    [HarmonyPatch(typeof(Explosion), "AffectCell")]
    public static class Patch_ExplosionCell_Context
    {
        public static void Prefix(Explosion __instance, out bool __state)
        {
            var c = PsycastContext.CarriedBy(__instance);
            __state = c != null;
            if (__state) PsycastContext.Push(c);
        }

        public static void Finalizer(bool __state) { if (__state) PsycastContext.Pop(); }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
    public static class Patch_DeSpawn_Forget
    {
        public static void Postfix(Thing __instance) { if (__instance is Explosion) PsycastContext.Forget(__instance); }
    }

    // Patch_CanHitTarget_Shield removed 2026-11-11 (her call): Hollow Wicker Basket and Simple Domain no longer stop
    // hostile psycasts from targeting anyone. An Evening Moon circle lets them be cast and cuts them down (BlocksOn).

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_AddHediff_Shield
    {
        public static bool Prefix(Hediff hediff) =>
            !(hediff?.def != null && hediff.def.isBad && !(hediff is Hediff_Injury) && PsycastContext.BlocksOn(hediff.pawn));
    }

    [HarmonyPatch(typeof(StunHandler), nameof(StunHandler.StunFor))]
    public static class Patch_StunFor_Shield
    {
        public static bool Prefix(StunHandler __instance) =>
            !(__instance.parent is Pawn p && (PsycastContext.BlocksOn(p) || (SureHit.Active && SureHit.Blocked(p))));
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_MentalState_Shield
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(MentalStateHandler), "pawn");
        private static Pawn PawnRef(MentalStateHandler x) => (Pawn)PawnField.GetValue(x);

        public static bool Prefix(MentalStateHandler __instance, ref bool __result)
        {
            if (!PsycastContext.BlocksOn(PawnRef(__instance))) return true;
            __result = false;
            return false;
        }
    }

    /// <summary>Every hit on a pawn: shields first (sure hits, shielded psycasts), then the attacker's
    /// own techniques (Domain Amplification's strip, the coated weapon, Black Flash).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_PreApplyDamage
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            var attacker = dinfo.Instigator as Pawn;
            if ((SureHit.Active && SureHit.Blocked(__instance)) || (dinfo.Instigator != __instance && PsycastContext.BlocksOn(__instance)))
            {
                if (!(SureHit.Active && SureHit.Blocked(__instance)) && __instance.Spawned)
                    MoteMaker.ThrowText(__instance.DrawPos, __instance.Map, "Cut", new Color(0.75f, 0.85f, 1f), 1.2f);   // Evening Moon
                absorbed = true;
                return false;
            }
            Gauntlets.Guard(__instance, ref dinfo);   // gauntlets held up take some hits on the arms
            CursedTools.OnTaken(__instance, dinfo);   // Dragon-Bone stores part of each hit its wielder takes
            if (attacker == null || attacker == __instance) return true;
            CursedTools.OnHit(attacker, __instance, ref dinfo);
            if (BlackFlashRules.InMelee && Jujutsu.Has(attacker, JJKDefOf.JJK_DomainAmplification))
                AmplificationStrip.Strip(__instance, attacker);
            Hediff_Reinforcement.CoatWeapon(attacker, ref dinfo);
            HazyMoon.Scale(attacker, ref dinfo);
            BlackFlashHit.Try(attacker, __instance, ref dinfo);
            return true;
        }
    }

    /// <summary>Hollow Wicker Basket and Simple Domain need the user standing still on the ground
    /// (her calls 2026-11-10). Moving, or being lifted off the ground, drops the technique.</summary>
    public static class StandStill
    {
        public static string Blocked(Pawn p)
        {
            if (!Jujutsu.OnGround(p)) return "Must be on the ground.";
            if (p.Downed) return "Can't while downed.";
            return null;
        }

        /// <summary>Stop where they are, so the technique doesn't drop on the next step.</summary>
        public static void Begin(Pawn p)
        {
            if (p.pather != null && p.pather.Moving) p.pather.StopDead();
            if (p.jobs?.curJob != null && p.jobs.curJob.def != JobDefOf.Wait_Combat && p.Drafted)
                p.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
        }

        /// <summary>Called from the techniques' hediffs every few ticks.</summary>
        public static void Check(Pawn p, string key)
        {
            if (p == null || !Toggles.IsOn(p, key)) return;
            if ((p.pather != null && p.pather.Moving) || !Jujutsu.OnGround(p) || p.Downed)
                Toggles.Set(p, key, false);
        }
    }

    /// <summary>Hollow Wicker Basket: no attacking while the hands hold the sign.</summary>
    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn),
        new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    public static class Patch_Basket_NoAttack
    {
        public static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!(__instance.caster is Pawn p) || !(__instance is Verb_MeleeAttack || __instance is Verb_LaunchProjectile)) return true;
            if (!Jujutsu.Has(p, JJKDefOf.JJK_HollowWickerBasket)) return true;
            __result = false;
            return false;
        }
    }

    public class Hediff_HollowWickerBasket : HediffWithComps
    {
        public override void PostAdd(DamageInfo? dinfo) { base.PostAdd(dinfo); WickerBasketFx.Register(pawn); }

        public override void PostRemoved() { base.PostRemoved(); WickerBasketFx.Dropped(pawn); }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            WickerBasketFx.Register(pawn);   // after a load
            if (!pawn.IsHashIntervalTick(10, delta)) return;
            // up when Simple Domain was learned: its button is hidden from then on, so it turns into Simple Domain
            // here (her call: the Basket turns into it) instead of staying up with no way to drop it
            if (Toggles.IsOn(pawn, "basket") && Jujutsu.Knows(pawn, JJKAbilityDefOf.JJK_SimpleDomain))
            {
                Toggles.Set(pawn, "simpledomain", true, quiet: true);
                Toggles.Set(pawn, "basket", false, quiet: true);
                return;
            }
            StandStill.Check(pawn, "basket");
        }
    }

    /// <summary>Domain Amplification: melee hits strip psycast buffs and shields; no psycasts of your own.</summary>
    public class Hediff_DomainAmplification : HediffWithComps { }

    public static class AmplificationStrip
    {
        /// <summary>Hediffs that psycasts put on (not harmful), from every psycast's hediff extension.</summary>
        public static readonly HashSet<HediffDef> Buffs = new HashSet<HediffDef>();

        public static void CollectBuffs()
        {
            foreach (var def in DefDatabase<VEF.Abilities.AbilityDef>.AllDefs)
            {
                if (def.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>() == null || def.modExtensions == null) continue;
                foreach (var ext in def.modExtensions.OfType<AbilityExtension_Hediff>())
                    if (ext.hediff != null && !ext.hediff.isBad && !Ours(ext.hediff)) Buffs.Add(ext.hediff);
            }
        }

        private static readonly MethodInfo ShieldBreak = AccessTools.Method(typeof(HediffComp_Shield), "Break");

        private static bool Ours(HediffDef d) => d.defName.StartsWith("JJK_");

        public static bool Strip(Pawn victim, Pawn attacker)
        {
            var set = victim.health?.hediffSet;
            if (set == null) return false;
            bool any = false;
            foreach (var h in set.hediffs.ToList())
            {
                if (Buffs.Contains(h.def)) { victim.health.RemoveHediff(h); any = true; continue; }
                if (h is HediffWithComps hc)
                    foreach (var c in hc.comps.OfType<HediffComp_Shield>())
                        if (c.ShieldActive) { ShieldBreak.Invoke(c, null); any = true; }
            }
            var r = Hediff_Reinforcement.Of(victim);
            if (r != null && r.Raised) { Toggles.Set(victim, "reinforcement", false, quiet: true); any = true; }
            foreach (var a in Jujutsu.AmplificationStrips) a(victim, attacker);
            if (any && victim.Spawned)
                MoteMaker.ThrowText(victim.DrawPos, victim.Map, "Neutralized", new Color(0.55f, 0.75f, 1f), 2f);
            return any;
        }
    }

    /// <summary>With Domain Amplification on, no psycasts of your own, except where a mod says otherwise
    /// (her call: only Sukuna keeps his cursed technique).</summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.IsEnabledForPawn))]
    public static class Patch_Amplification_NoCasting
    {
        public static void Postfix(Ability __instance, ref bool __result, ref string reason)
        {
            if (!__result || __instance is Ability_Toggle || __instance.pawn == null) return;
            if (__instance.def.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>() == null) return;
            if (!Jujutsu.Has(__instance.pawn, JJKDefOf.JJK_DomainAmplification)) return;
            if (Jujutsu.Any(Jujutsu.AmplificationExempt, __instance.pawn)) return;
            __result = false;
            reason = "Domain amplification is on.";
        }
    }
}
