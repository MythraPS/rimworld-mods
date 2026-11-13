using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;

namespace SukunaPsycasts
{
    /// <summary>
    /// Cleave is no longer aimed. Toggle it on and every melee attack this pawn lands
    /// on a living thing — animals and plants included — carries a Cleave through it.
    /// </summary>
    public class CleaveToggle : GameComponent
    {
        private static HashSet<int> on = new HashSet<int>();
        public CleaveToggle(Game game) { on = new HashSet<int>(); }

        public static bool IsOn(Pawn p) => p != null && on.Contains(p.thingIDNumber);

        public static void Set(Pawn p, bool value)
        {
            if (p == null) return;
            if (value) on.Add(p.thingIDNumber); else on.Remove(p.thingIDNumber);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            var list = new List<int>(on);
            Scribe_Collections.Look(ref list, "sukunaCleaveOn", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                on = list == null ? new HashSet<int>() : new HashSet<int>(list);
        }
    }

    /// <summary>Rides the pawn's own melee swing rather than being cast.</summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_MeleeCleave
    {
        private const float PsyfocusCost = 0.06f;
        private const float EntropyGain  = 20f;

        public static void Postfix(Verb_MeleeAttack __instance, bool __result)
        {
            if (!__result) return;                       // the swing missed
            if (!(__instance.CasterPawn is Pawn caster)) return;
            if (!CleaveToggle.IsOn(caster)) return;

            var target = __instance.CurrentTarget.Thing;
            if (target == null || target.Destroyed || target == caster) return;

            // Living things only — pawns, animals and plants. Not walls or furniture;
            // Dismantle is the tool for those.
            if (!(target is Pawn) && !(target is Corpse)
                && target.def.category != ThingCategory.Plant) return;

            var tracker = caster.psychicEntropy;
            if (tracker == null || tracker.CurrentPsyfocus < PsyfocusCost) return;
            if (tracker.WouldOverflowEntropy(EntropyGain)) return;

            tracker.OffsetPsyfocusDirectly(-PsyfocusCost);
            tracker.TryAddEntropy(EntropyGain, caster, false, false);
            Ability_Cleave.Strike(caster, target);
            ComboTracker.NoteCleave(caster);
        }
    }
}
