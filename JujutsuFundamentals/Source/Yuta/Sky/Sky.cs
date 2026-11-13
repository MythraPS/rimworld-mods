using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    // Takako Uro's Sky Manipulation on Yuta's path (her design 2026-11-11): Flight, Redirect, Thin Ice-Breaker.
    // Yuta and Uro both have it. Every number here is a proposal.

    [DefOf]
    public static class SkyDefOf
    {
        public static HediffDef JJK_SkyFlight;
        public static HediffDef JJK_SkyRedirect;
        public static HediffDef JJK_ThinIceBroken;
        public static HediffDef JJK_GWarstaffReading;
        static SkyDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SkyDefOf));
    }

    [StaticConstructorOnStartup]
    public static class SkyStartup
    {
        public const string FlightKey = "skyflight";

        static SkyStartup()
        {
            // Flight is one of the shared toggles: armed by its checkbox, psyfocus every second while up (Gojo's Fly rate).
            Toggles.Add(new ToggleSpec
            {
                key = FlightKey, title = "Flight", drain = 0.0008f,   // proposal: Gojo's Fly drain
                hediff = () => SkyDefOf.JJK_SkyFlight, ability = () => YutaAbilities.Def(YutaAbilities.Flight),
                // her call 2026-11-11: Uro's flight costs less (proposal: a quarter); the sky is her own technique
                drainFactor = p => Uro.Is(p) ? 0.25f : 1f,
            });
            GojoPsycasts.GojoFlight.ExtraFliers.Add(SkyFlight.On);
            GojoPsycasts.GojoFlight.ExtraAnyOn.Add(() => SkyFlight.Count > 0);
            Jujutsu.Airborne.Add(SkyFlight.On);
        }
    }

    // ==================================================================== Flight

    /// <summary>While the hediff is up (the toggle pays for it), the pawn flies like Gojo: over walls and water, around
    /// mountains, never under a roof. GojoFlight does the moving.</summary>
    public static class SkyFlight
    {
        public static int Count;
        public static bool On(Pawn p) =>
            p != null && Jujutsu.Has(p, SkyDefOf.JJK_SkyFlight) && (p.psychicEntropy?.CurrentPsyfocus ?? 0f) > 0.001f;
    }

    /// <summary>Only a marker; it counts itself so the path patch can skip work when nobody is flying.</summary>
    public class Hediff_SkyFlight : HediffWithComps
    {
        private bool counted;

        public override void PostAdd(DamageInfo? dinfo) { base.PostAdd(dinfo); Count(); }
        public override void PostRemoved() { base.PostRemoved(); if (counted) { counted = false; SkyFlight.Count--; } }

        private void Count() { if (!counted) { counted = true; SkyFlight.Count++; } }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Count();
        }
    }

    /// <summary>Resets the flier count when a game starts or loads.</summary>
    public class SkyCounterReset : GameComponent
    {
        public SkyCounterReset(Game game) { SkyFlight.Count = 0; }
    }

    // ==================================================================== Redirect

    /// <summary>Redirect: for a short while, shots aimed at the user bend away or go back at whoever fired them
    /// (canon: Yuta turned Ryu's Granite Blast around).</summary>
    public class Ability_SkyRedirect : Ability
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            SkyRedirect.Apply(pawn);
        }
    }

    public static class SkyRedirect
    {
        /// <summary>Proposal: half the shots go back at the shooter, the rest bend away.</summary>
        public const float BackChance = 0.5f;

        public static void Apply(Pawn p)
        {
            if (p?.health == null) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(SkyDefOf.JJK_SkyRedirect);
            if (h != null) p.health.RemoveHediff(h);
            p.health.AddHediff(SkyDefOf.JJK_SkyRedirect);
            if (p.Spawned) FleckMaker.Static(p.Position, p.Map, FleckDefOf.PsycastAreaEffect, 2f);
        }
    }

    /// <summary>The 8-argument Launch is the one every shot goes through (the short one calls it).</summary>
    [HarmonyPatch(typeof(Projectile), nameof(Projectile.Launch),
        new[] { typeof(Thing), typeof(Vector3), typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(ProjectileHitFlags), typeof(bool), typeof(Thing), typeof(ThingDef) })]
    public static class Patch_SkyRedirect
    {
        public static void Prefix(ref Thing launcher, ref Vector3 origin, ref LocalTargetInfo usedTarget, ref LocalTargetInfo intendedTarget,
                                  ref ProjectileHitFlags hitFlags)
        {
            var p = intendedTarget.Thing as Pawn ?? usedTarget.Thing as Pawn;
            if (p == null || launcher == null || launcher == p || !p.Spawned || p.Dead || !launcher.HostileTo(p)) return;
            bool redirect = Jujutsu.Has(p, SkyDefOf.JJK_SkyRedirect);
            // G Warstaff: reading the shooter's future, the target may simply step out of the shot
            bool dodge = !redirect && GWarstaff.Reading(p, launcher) && Rand.Chance(GWarstaff.RangedDodge);
            if (!redirect && !dodge) return;
            var map = p.Map;
            if (redirect && Rand.Chance(SkyRedirect.BackChance) && launcher.Spawned && launcher.Map == map)
            {
                // back the way it came: it now starts at the redirector and flies at the shooter, as her shot
                var shooter = launcher;
                origin = p.DrawPos;
                launcher = p;
                usedTarget = new LocalTargetInfo(shooter);
                intendedTarget = new LocalTargetInfo(shooter);
                hitFlags = ProjectileHitFlags.IntendedTarget;
                MoteMaker.ThrowText(p.DrawPos, map, "Redirected", Yuta.Purple, 1.5f);
            }
            else
            {
                // bent away: it lands on open ground beside and past her
                var dir = (p.DrawPos - origin).normalized;
                var side = new Vector3(dir.z, 0f, -dir.x) * (Rand.Bool ? 1f : -1f);
                var cell = (p.DrawPos + dir * Rand.Range(2f, 5f) + side * Rand.Range(2f, 4f)).ToIntVec3().ClampInsideMap(map);
                usedTarget = new LocalTargetInfo(cell);
                intendedTarget = new LocalTargetInfo(cell);
                hitFlags = ProjectileHitFlags.None;
                MoteMaker.ThrowText(p.DrawPos, map, dodge ? "Dodged" : "Bent away", Yuta.Purple, 1.5f);
            }
        }
    }

    // ==================================================================== Thin Ice-Breaker

    /// <summary>Thin Ice-Breaker: a shockwave that goes through guard. Every enemy caught takes damage that ignores
    /// armour, loses psycast shields and buffs, shield belts break, and for a while their armour doesn't count at all.</summary>
    public class Ability_ThinIceBreaker : Ability
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = targets.FirstOrDefault();
            if (pawn?.Map == null || !t.IsValid) return;
            ThinIce.Shockwave(pawn, t.Cell, GetPowerForPawn());
        }
    }

    public static class ThinIce
    {
        public const float Radius = 3.9f;        // proposal
        public const float Damage = 18f;         // proposal, x psychic sensitivity (the def's power)
        private static DamageDef dmgDef;
        private static FleckDef[] cracks;

        /// <summary>A crack in the space at <paramref name="at"/>, <paramref name="size"/> cells across, at a random angle.</summary>
        public static void Crack(Vector3 at, Map map, float size)
        {
            if (map == null) return;
            if (cracks == null)
                cracks = new[] { "JJK_Fleck_SkyCrackA", "JJK_Fleck_SkyCrackB", "JJK_Fleck_SkyCrackC" }
                    .Select(n => DefDatabase<FleckDef>.GetNamedSilentFail(n)).Where(d => d != null).ToArray();
            if (cracks.Length == 0) return;
            var data = FleckMaker.GetDataStatic(at, map, cracks.RandomElement(), size);
            data.rotation = Rand.Range(0f, 360f);
            map.flecks.CreateFleck(data);
        }

        private static readonly System.Reflection.MethodInfo BeltBreak = AccessTools.Method(typeof(CompShield), "Break");

        public static void Shockwave(Pawn caster, IntVec3 center, float damage)
        {
            var map = caster.Map;
            if (map == null || !center.InBounds(map)) return;
            dmgDef = dmgDef ?? DefDatabase<DamageDef>.GetNamedSilentFail("JJK_CursedEnergy") ?? DamageDefOf.Blunt;
            float r = Radius * Jujutsu.Boost(caster);
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, r * 2f);
            FleckMaker.Static(center, map, FleckDefOf.ExplosionFlash, r);
            // the space around the target cracks like ice: one big break at the centre, smaller ones around it
            Crack(center.ToVector3Shifted(), map, r * 2f);
            for (int i = 0; i < Mathf.Clamp(Mathf.RoundToInt(r), 3, 8); i++)
            {
                var c = center + GenRadial.RadialPattern[Rand.Range(1, GenRadial.NumCellsInRadius(r))];
                if (c.InBounds(map)) Crack(c.ToVector3Shifted(), map, Rand.Range(1.2f, 2.2f));
            }
            for (int i = 0; i < 10; i++)
            {
                var c = center + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(r))];
                if (c.InBounds(map)) FleckMaker.ThrowDustPuffThick(c.ToVector3Shifted(), map, 1.4f, new Color(0.75f, 0.85f, 1f));
            }
            SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(center, map));
            foreach (var v in map.mapPawns.AllPawnsSpawned.ToList())
            {
                if (v == caster || v.Dead || !v.HostileTo(caster) || !v.Position.InHorDistOf(center, r)) continue;
                Break(v, caster);
                var d = new DamageInfo(dmgDef, damage * Jujutsu.Boost(caster), 1f, -1f, caster);
                d.SetIgnoreArmor(true);
                v.TakeDamage(d);
            }
        }

        /// <summary>Strip shields and buffs, break shield belts, and leave the armour useless for a while.</summary>
        public static void Break(Pawn v, Pawn caster)
        {
            if (v.Spawned) Crack(v.DrawPos, v.Map, Rand.Range(1.8f, 2.4f));
            AmplificationStrip.Strip(v, caster);
            if (v.apparel != null && BeltBreak != null)
                foreach (var a in v.apparel.WornApparel)
                {
                    var s = a.TryGetComp<CompShield>();
                    if (s != null && s.ShieldState == ShieldState.Active) BeltBreak.Invoke(s, null);
                }
            if (v.health == null) return;
            var h = v.health.hediffSet.GetFirstHediffOfDef(SkyDefOf.JJK_ThinIceBroken);
            if (h != null) v.health.RemoveHediff(h);
            v.health.AddHediff(SkyDefOf.JJK_ThinIceBroken);
        }
    }

    /// <summary>While Thin Ice-Breaker's hediff lasts, armour doesn't count against anything that hits the pawn.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_ThinIceArmour
    {
        public static void Prefix(Pawn __instance, ref DamageInfo dinfo)
        {
            if (!dinfo.IgnoreArmor && Jujutsu.Has(__instance, SkyDefOf.JJK_ThinIceBroken)) dinfo.SetIgnoreArmor(true);
        }
    }
}
