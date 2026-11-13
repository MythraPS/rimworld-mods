using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Charles Bernard's G Warstaff, copied by Yuta (her design 2026-11-11). Passive. Canon: the target's blood is the
    /// ink, and he sees their future. Here: once the user draws a pawn's blood with a melee hit, he reads that pawn for
    /// a while: he hits them more often and dodges their melee attacks more often, and dodges their shots and psycasts.
    /// Proposals: 30 s, +10 s per further bleeding hit, at most 60 s; +25% hit chance, +30% melee dodge chance (capped
    /// at 90%), 40% to dodge each shot or psycast. Not saved: reads are short.
    /// </summary>
    public static class GWarstaff
    {
        public const int First = 1800, More = 600, Max = 3600;
        public const float HitBonus = 0.25f, DodgeBonus = 0.30f, DodgeCap = 0.9f;
        /// <summary>Her call 2026-11-11: he also dodges the read pawn's shots and psycasts (her example: Sukuna's
        /// Dismantle). Proposal: 40% of each. It still only starts from drawing their blood in melee.</summary>
        public const float RangedDodge = 0.40f;

        private static readonly Dictionary<int, Dictionary<int, int>> reads = new Dictionary<int, Dictionary<int, int>>();
        private static Verse.Game game;

        public static bool Knows(Pawn p) => Jujutsu.Knows(p, YutaAbilities.Def(YutaAbilities.GWarstaff));

        public static bool Reading(Pawn reader, Thing target)
        {
            if (reader == null || target == null) return false;
            if (game != Current.Game) { reads.Clear(); game = Current.Game; }
            return reads.TryGetValue(reader.thingIDNumber, out var d) && d.TryGetValue(target.thingIDNumber, out var until)
                   && until > Find.TickManager.TicksGame;
        }

        public static void Drew(Pawn reader, Pawn target)
        {
            if (game != Current.Game) { reads.Clear(); game = Current.Game; }
            int now = Find.TickManager.TicksGame;
            if (!reads.TryGetValue(reader.thingIDNumber, out var d)) reads[reader.thingIDNumber] = d = new Dictionary<int, int>();
            int until = d.TryGetValue(target.thingIDNumber, out var u) && u > now ? Mathf.Min(u + More, now + Max) : now + First;
            d[target.thingIDNumber] = until;
            if (reader.Spawned) MoteMaker.ThrowText(target.DrawPos, target.Map, "Read", new Color(0.8f, 0.15f, 0.2f), 1.5f);
            // a visible marker on the reader, lasting as long as his longest read
            var def = SkyDefOf.JJK_GWarstaffReading;
            if (reader.health == null || def == null) return;
            var h = reader.health.hediffSet.GetFirstHediffOfDef(def) as HediffWithComps ?? reader.health.AddHediff(def) as HediffWithComps;
            var dis = h?.TryGetComp<HediffComp_Disappears>();
            if (dis != null) dis.ticksToDisappear = Mathf.Max(dis.ticksToDisappear, until - now);
        }
    }

    /// <summary>A melee hit that leaves the target bleeding starts (or lengthens) the read.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_GWarstaffBlood
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || __instance.Dead || !(dinfo.Instigator is Pawn reader) || reader == __instance) return;
            var def = dinfo.Def;
            if (def == null || def.isRanged || def.isExplosive || dinfo.Weapon == null) return;   // melee only; psycasts carry no weapon
            if (def.hediff?.injuryProps == null || def.hediff.injuryProps.bleedRate <= 0f) return;
            if (__instance.RaceProps.BloodDef == null || !reader.Spawned || !__instance.Spawned) return;
            if (!reader.Position.InHorDistOf(__instance.Position, 1.9f) || !GWarstaff.Knows(reader)) return;
            GWarstaff.Drew(reader, __instance);
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
    public static class Patch_GWarstaffHit
    {
        public static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref float __result)
        {
            if (GWarstaff.Reading(__instance.CasterPawn, target.Thing)) __result = Mathf.Min(1f, __result + GWarstaff.HitBonus);
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetDodgeChance")]
    public static class Patch_GWarstaffDodge
    {
        public static void Postfix(Verb_MeleeAttack __instance, LocalTargetInfo target, ref float __result)
        {
            if (target.Thing is Pawn reader && GWarstaff.Reading(reader, __instance.CasterPawn))
                __result = Mathf.Min(GWarstaff.DodgeCap, __result + GWarstaff.DodgeBonus);
        }
    }
}
