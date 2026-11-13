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
    /// <summary>Poisonous blood (her call 2026-11-06), a trait Yuji has: it can't hurt the one who has
    /// it, and poisons anyone else it gets into. Out: his Hemosage abilities - Blood Shard on a hit, and
    /// the bloodmist from Blood Spew, Sanguine Spear, Corpse Explosion and Bloodstorm. In: anyone who
    /// drinks it - a sanguophage biting him, Hemodrain or Word of Offering cast on him. Never lethal:
    /// it builds up per dose and fades within a day. (Piercing Blood: Scatter keeps its own three-day
    /// knockout.) The Hemosage hooks find their methods by name, so without that mod they do nothing.</summary>
    public static class PoisonBlood
    {
        public const float ShardDose = 0.3f, MistDose = 0.35f, DrinkDose = 0.6f;

        private static TraitDef trait;
        public static TraitDef TraitDefOfIt { get { if (trait == null) trait = DefDatabase<TraitDef>.GetNamedSilentFail("Sukuna_PoisonBlood"); return trait; } }
        public static void EnsureTrait(Pawn p)
        {
            if (TraitDefOfIt != null && p?.story?.traits != null && !p.story.traits.HasTrait(TraitDefOfIt))
                p.story.traits.GainTrait(new Trait(TraitDefOfIt, 0, true));
        }

        private static HediffDef def;
        public static HediffDef Def { get { if (def == null) def = DefDatabase<HediffDef>.GetNamedSilentFail("Sukuna_YujiBlood"); return def; } }

        /// <summary>Yuji, while one of his blood effects is landing (null otherwise).</summary>
        public static Pawn source;
        /// <summary>Maps whose current bloodstorm Yuji called down.</summary>
        public static readonly HashSet<int> storms = new HashSet<int>();

        /// <summary>Has poisonous blood: the trait (Yuji, and anyone else given it), or Yuji himself.</summary>
        public static bool IsYuji(Pawn p) => p != null && ((TraitDefOfIt != null && p.story?.traits?.HasTrait(TraitDefOfIt) == true) || Yuji.Is(p) || ModuloYuji.Is(p));

        public static void Poison(Pawn p, float dose)
        {
            if (Def == null || p == null || p.Dead || p.health == null || !p.RaceProps.IsFlesh || IsYuji(p)) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(Def);
            if (h == null)
            {
                h = HediffMaker.MakeHediff(Def, p);
                h.Severity = dose;
                p.health.AddHediff(h);
            }
            else h.Severity = Mathf.Min(Def.maxSeverity, h.Severity + dose);
        }

        public static MethodBase Find(string type, string method)
        {
            var t = AccessTools.TypeByName(type);
            return t == null ? null : AccessTools.Method(t, method);
        }
    }

    /// <summary>Bloodmist landing while Yuji's blood is in the air: poison too.</summary>
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.AddHediff),
        new[] { typeof(Hediff), typeof(BodyPartRecord), typeof(DamageInfo?), typeof(DamageWorker.DamageResult) })]
    public static class Patch_YujiBloodMist
    {
        public static void Postfix(Pawn ___pawn, Hediff hediff)
        {
            if (PoisonBlood.source != null && hediff?.def?.defName == "VPEH_Bloodmist")
                PoisonBlood.Poison(___pawn, PoisonBlood.MistDose);
        }
    }

    // ---- the moments Yuji's blood goes out: each sets PoisonBlood.source while it runs ----

    [HarmonyPatch]
    public static class Patch_YujiBloodSpew
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.BloodSpew", "DoDamage");
        public static void Prefix(Projectile __instance, out Pawn __state)
        {
            __state = PoisonBlood.source;
            if (__instance.Launcher is Pawn p && PoisonBlood.IsYuji(p)) PoisonBlood.source = p;
        }
        public static void Finalizer(Pawn __state) => PoisonBlood.source = __state;
    }

    [HarmonyPatch]
    public static class Patch_YujiSanguineSpear
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.SanguineSpear", "Impact");
        public static void Prefix(Projectile __instance, Thing hitThing, out Pawn __state)
        {
            __state = PoisonBlood.source;
            if (__instance.Launcher is Pawn p && PoisonBlood.IsYuji(p))
            {
                PoisonBlood.source = p;
                if (hitThing is Pawn hit) PoisonBlood.Poison(hit, PoisonBlood.ShardDose);    // the spear itself
            }
        }
        public static void Finalizer(Pawn __state) => PoisonBlood.source = __state;
    }

    [HarmonyPatch]
    public static class Patch_YujiCorpseExplosion
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.Ability_CorpseExplosion", "Cast");
        public static void Prefix(VEF.Abilities.Ability __instance, out Pawn __state)
        {
            __state = PoisonBlood.source;
            if (PoisonBlood.IsYuji(__instance.pawn)) PoisonBlood.source = __instance.pawn;
        }
        public static void Finalizer(Pawn __state) => PoisonBlood.source = __state;
    }

    [HarmonyPatch]
    public static class Patch_YujiBloodShard
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.BloodShard", "Impact");
        public static void Prefix(Projectile __instance, Thing hitThing)
        {
            if (hitThing is Pawn hit && __instance.Launcher is Pawn p && PoisonBlood.IsYuji(p))
                PoisonBlood.Poison(hit, PoisonBlood.ShardDose);
        }
    }

    /// <summary>Yuji calls a bloodstorm: remember the map, so the storm's mist poisons there.</summary>
    [HarmonyPatch]
    public static class Patch_YujiBloodstormCast
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.Ability_Bloodstorm", "Cast");
        public static void Postfix(VEF.Abilities.Ability __instance)
        {
            var map = __instance.pawn?.Map;
            if (map == null) return;
            if (PoisonBlood.IsYuji(__instance.pawn)) PoisonBlood.storms.Add(map.uniqueID);
            else PoisonBlood.storms.Remove(map.uniqueID);      // someone else's storm now
        }
    }

    [HarmonyPatch]
    public static class Patch_YujiBloodstormTick
    {
        public static bool Prepare() => TargetMethod() != null;
        public static MethodBase TargetMethod() => PoisonBlood.Find("VPEHemosage.WeatherOverlay_Bloodstorm", "TickOverlay");
        public static void Prefix(Map map, out Pawn __state)
        {
            __state = PoisonBlood.source;
            if (map == null || !PoisonBlood.storms.Contains(map.uniqueID)) return;
            if (map.weatherManager.curWeather?.defName != "VPEH_Bloodstorm") { PoisonBlood.storms.Remove(map.uniqueID); return; }
            // any Yuji will do as the source: it only marks the mist as his
            foreach (var p in map.mapPawns.AllPawnsSpawned) if (PoisonBlood.IsYuji(p)) { PoisonBlood.source = p; return; }
            PoisonBlood.source = __state ?? Find.WorldPawns.AllPawnsAlive.FirstOrDefault(PoisonBlood.IsYuji);
        }
        public static void Finalizer(Pawn __state) => PoisonBlood.source = __state;
    }

    // ---- blood going the other way: whoever drinks Yuji's blood ----

    /// <summary>A sanguophage biting him.</summary>
    [HarmonyPatch(typeof(SanguophageUtility), nameof(SanguophageUtility.DoBite))]
    public static class Patch_YujiBloodBitten
    {
        public static void Postfix(Pawn biter, Pawn victim)
        {
            if (PoisonBlood.IsYuji(victim)) PoisonBlood.Poison(biter, PoisonBlood.DrinkDose);
        }
    }

    /// <summary>Hemodrain or Word of Offering cast on him: the caster takes in his blood.</summary>
    [HarmonyPatch]
    public static class Patch_YujiBloodDrained
    {
        public static bool Prepare() => TargetMethods().Any();
        public static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var t in new[] { "VPEHemosage.Ability_Hemodrain", "VPEHemosage.Ability_WordofOffering" })
            {
                var m = PoisonBlood.Find(t, "Cast");
                if (m != null) yield return m;
            }
        }
        public static void Postfix(VEF.Abilities.Ability __instance, GlobalTargetInfo[] targets)
        {
            if (targets != null && targets.Length > 0 && targets[0].Thing is Pawn victim && PoisonBlood.IsYuji(victim))
                PoisonBlood.Poison(__instance.pawn, PoisonBlood.DrinkDose);
        }
    }
}
