using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Choso's and Modulo Yuji's Corpse Explosion (her call 2026-11-11): instead of a corpse, they pick a spot, and
    /// every pool of blood in the area (radius 2.9, the ability's own) bursts, hurting anything near it except their
    /// allies (their own faction and allied factions). The blood is used up. Everyone else keeps Hemosage's version.
    /// Proposal: each pool bursts 1.5 cells wide for 6 + 2 x its thickness (8 to 16), armour penetration 0.3; one
    /// thing takes at most 40 a cast.
    /// </summary>
    public static class BloodBurst
    {
        public const float BurstRadius = 1.5f, ArmourPen = 0.3f, CapPerThing = 40f;

        public static bool Uses(Pawn p) => ChosoTracker.Is(p) || ModuloYuji.Is(p) || Yuji.Is(p)
                                            || JujutsuFundamentals.Innate.Marked(p, JujutsuFundamentals.Innate.Blood);   // Yuji too (her call 2026-11-11)

        private static HashSet<ThingDef> blood;
        public static bool IsBlood(Thing t)
        {
            if (blood == null)
            {
                blood = new HashSet<ThingDef>();
                foreach (var d in DefDatabase<ThingDef>.AllDefs)
                {
                    if (d.race?.BloodDef != null) blood.Add(d.race.BloodDef);
                    if (d.IsFilth && d.defName.StartsWith("Filth_Blood")) blood.Add(d);
                }
            }
            return t != null && t.def.IsFilth && blood.Contains(t.def);
        }

        public static bool Ally(Pawn caster, Thing t)
        {
            if (t == caster) return true;
            var f = t.Faction; var mine = caster.Faction;
            if (f == null || mine == null) return false;
            return f == mine || f.RelationKindWith(mine) == FactionRelationKind.Ally;
        }

        public static void Burst(Pawn caster, IntVec3 center, float radius)
        {
            var map = caster?.Map;
            if (map == null || !center.InBounds(map)) return;
            var pools = GenRadial.RadialCellsAround(center, radius, true)
                .Where(c => c.InBounds(map))
                .SelectMany(c => c.GetThingList(map).Where(IsBlood))
                .ToList();
            if (pools.Count == 0)
            {
                Messages.Message("No blood there to burst.", new TargetInfo(center, map), MessageTypeDefOf.RejectInput, false);
                return;
            }
            var dealt = new Dictionary<Thing, float>();
            foreach (var pool in pools)
            {
                var at = pool.Position;
                int thick = (pool as Filth)?.thickness ?? 1;
                float dmg = 6f + 2f * thick;
                pool.Destroy();
                FleckMaker.Static(at, map, FleckDefOf.ExplosionFlash, 2f);
                for (int i = 0; i < 2; i++)
                    FleckMaker.ThrowDustPuffThick(at.ToVector3Shifted() + new Vector3(Rand.Range(-0.4f, 0.4f), 0f, Rand.Range(-0.4f, 0.4f)), map, 1.2f, new Color(0.55f, 0.04f, 0.04f));
                var victims = GenRadial.RadialCellsAround(at, BurstRadius, true)
                    .Where(c => c.InBounds(map))
                    .SelectMany(c => c.GetThingList(map))
                    .Where(t => !(t is Filth) && !(t is Mote) && (t.def.useHitPoints || t is Pawn))
                    .Distinct().ToList();
                foreach (var t in victims)
                {
                    if (t.Destroyed || Ally(caster, t)) continue;
                    dealt.TryGetValue(t, out float had);
                    float amount = Mathf.Min(dmg, CapPerThing - had);
                    if (amount <= 0f) continue;
                    dealt[t] = had + amount;
                    t.TakeDamage(new DamageInfo(DamageDefOf.Bomb, amount, ArmourPen, -1f, caster));
                }
            }
        }
    }

    /// <summary>VEF's own Ability.Cast (psyfocus, hemogen, cooldown, experience), skipping Hemosage's corpse code.</summary>
    [HarmonyPatch(typeof(VEF.Abilities.Ability), nameof(VEF.Abilities.Ability.Cast), new[] { typeof(GlobalTargetInfo[]) })]
    public static class AbilityBaseCast
    {
        [HarmonyReversePatch]
        public static void Cast(VEF.Abilities.Ability instance, GlobalTargetInfo[] targets) => throw new NotImplementedException("reverse patch stub");
    }

    [HarmonyPatch]
    public static class Patch_BloodBurstCast
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.Ability_CorpseExplosion", "Cast");
        public static bool Prefix(VEF.Abilities.Ability __instance, GlobalTargetInfo[] targets)
        {
            var p = __instance.pawn;
            if (!BloodBurst.Uses(p)) return true;
            AbilityBaseCast.Cast(__instance, targets);
            var t = targets != null && targets.Length > 0 ? targets[0] : GlobalTargetInfo.Invalid;
            if (t.IsValid) BloodBurst.Burst(p, t.Cell, __instance.def.radius > 0f ? __instance.def.radius : 2.9f);
            return false;
        }
    }

    /// <summary>They aim at a spot, not a corpse.</summary>
    [HarmonyPatch]
    public static class Patch_BloodBurstTarget
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.Ability_CorpseExplosion", "ValidateTarget");
        public static bool Prefix(VEF.Abilities.Ability __instance, LocalTargetInfo target, ref bool __result)
        {
            if (!BloodBurst.Uses(__instance.pawn)) return true;
            __result = target.IsValid && __instance.pawn?.Map != null && target.Cell.InBounds(__instance.pawn.Map);
            return false;
        }
    }

    /// <summary>For them the button is Supernova (her call 2026-11-11: rename it; canon's nearest is Choso's Supernova,
    /// compressed blood orbs burst outward), with its own icon. Other Hemosage users keep Corpse Explosion.</summary>
    [HarmonyPatch(typeof(VEF.Abilities.Ability), nameof(VEF.Abilities.Ability.GetGizmo))]
    [StaticConstructorOnStartup]
    public static class Patch_SupernovaGizmo
    {
        private static UnityEngine.Texture2D icon;

        public static void Postfix(VEF.Abilities.Ability __instance, Gizmo __result)
        {
            if (!(__result is Command cmd) || __instance?.def?.defName != "VPEH_CorpseExplosion" || !BloodBurst.Uses(__instance.pawn)) return;
            if (icon == null) icon = ContentFinder<UnityEngine.Texture2D>.Get("Abilities/Jujutsu/Supernova", false);
            cmd.defaultLabel = "Supernova";
            cmd.defaultDesc = "Blood manipulation. Pick a spot: every pool of blood within the area bursts, hurting anything near it "
                              + "that isn't an ally (their own faction and allied factions). The blood is used up. Costs hemogen.";
            if (icon != null) cmd.icon = icon;
        }
    }
}
