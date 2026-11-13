using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Her call 2026-11-11: while Rika is fully manifested (and ours to order), Copy and the partial manifestations
    /// (Arm, Bite, Grapple) move to her. They show on Rika's own buttons and are greyed out on Yuta's bar. They are
    /// still Yuta's abilities: his psyfocus, his neural heat, his cooldowns, and his power sets the damage. Range is
    /// measured from Rika.
    /// </summary>
    public static class RikaHands
    {
        public static readonly string[] Moved = { YutaAbilities.RikaArm, YutaAbilities.RikaBite, YutaAbilities.RikaGrapple, YutaAbilities.Copy };

        /// <summary>Set while Rika uses one of Yuta's abilities, so the "use it from Rika" block lets it through.</summary>
        public static bool fromRika;

        /// <summary>Is Rika out and ours, so this Yuta's Rika abilities belong to her right now?</summary>
        public static bool WithRika(Pawn yuta)
        {
            var r = Rika.Current;
            return r != null && Rika.Owner == yuta && Rika.Controllable(r) && r.Map == yuta?.Map;
        }

        public static bool BlockOnYuta(Pawn yuta, out string reason)
        {
            reason = null;
            if (fromRika || !WithRika(yuta)) return false;
            reason = "Rika is manifested: select her to use this.";
            return true;
        }

        public static Ability Get(Pawn yuta, string defName)
        {
            var comp = yuta?.GetComp<CompAbilities>();
            return comp?.LearnedAbilities?.FirstOrDefault(a => a.def?.defName == defName);
        }

        public static IEnumerable<Gizmo> Gizmos(Pawn rika)
        {
            var yuta = Rika.Owner;
            if (yuta == null || !WithRika(yuta)) yield break;
            foreach (var name in Moved)
            {
                var ab = Get(yuta, name);
                if (ab == null) continue;
                yield return Make(rika, yuta, ab);
            }
        }

        private static Command_Target Make(Pawn rika, Pawn yuta, Ability ab)
        {
            var def = ab.def;
            var cmd = new Command_Target
            {
                defaultLabel = def.LabelCap,
                defaultDesc = def.description + $"\n\nUsed by Rika, from where she stands; it costs {yuta.LabelShort}'s psyfocus.",
                icon = ContentFinder<Texture2D>.Get(def.iconPath, false) ?? BaseContent.BadTex,
                targetingParams = Targets(def.defName),
                action = t => Use(rika, yuta, ab, t),
            };
            string reason;
            fromRika = true;
            try
            {
                if (ab.cooldown > Find.TickManager.TicksGame) cmd.Disable($"Ready in {(ab.cooldown - Find.TickManager.TicksGame).ToStringTicksToPeriod()}.");
                else if (!ab.IsEnabledForPawn(out reason)) cmd.Disable(reason);
            }
            finally { fromRika = false; }
            return cmd;
        }

        /// <summary>Same targets as Yuta's buttons (VEF's own field differs between versions, so set here):
        /// Arm hits anything, Copy a person or a finger, Bite and Grapple a living target.</summary>
        private static TargetingParameters Targets(string defName)
        {
            bool arm = defName == YutaAbilities.RikaArm, copy = defName == YutaAbilities.Copy;
            return new TargetingParameters
            {
                canTargetPawns = true,
                canTargetBuildings = arm,
                canTargetItems = arm || copy,
                canTargetSelf = false,
                canTargetLocations = false,
            };
        }

        private static void Use(Pawn rika, Pawn yuta, Ability ab, LocalTargetInfo target)
        {
            var t = target.Thing;
            if (t == null || !t.Spawned || t.Map != rika.Map) return;
            float range = ab.GetRangeForPawn();
            if (range > 0f && !t.Position.InHorDistOf(rika.Position, range))
            {
                Messages.Message("Too far from Rika.", t, MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (ab.def.requireLineOfSight && !GenSight.LineOfSight(rika.Position, t.Position, rika.Map, true))
            {
                Messages.Message("Rika can't see it.", t, MessageTypeDefOf.RejectInput, false);
                return;
            }
            fromRika = true;
            try
            {
                if (!ab.IsEnabledForPawn(out string reason))
                {
                    if (reason != null) Messages.Message(reason, rika, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (ab is Ability_Copy && !CopyRules.CanEat(yuta, t, out string why))
                {
                    if (why != null) Messages.Message(why, t, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                rika.rotationTracker?.FaceTarget(t);
                ab.Cast(new[] { new GlobalTargetInfo(t) });
            }
            finally { fromRika = false; }
        }
    }

    /// <summary>Rika's buttons for Yuta's moved abilities.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_RikaHandsGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            if (__instance == null || __instance != Rika.Current) yield break;
            foreach (var g in RikaHands.Gizmos(__instance)) yield return g;
        }
    }
}
