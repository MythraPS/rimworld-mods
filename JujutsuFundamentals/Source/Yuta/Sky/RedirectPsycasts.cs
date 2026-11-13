using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Redirect against psycasts (her call 2026-11-11): a hostile psycast aimed at a redirecting pawn, or a beam whose
    /// line crosses them (Granite Blast, Cursed Energy Blast, blood beams, Purple...), has the same 50% to go back at its
    /// caster; otherwise it bends away. G Warstaff gives the same escape, as a 40% dodge, against the pawn being read.
    ///
    /// Every VEF Ability's Cast is wrapped here (as PsycastContext does, after it: Priority.Last). "Back" casts the
    /// caster's own technique from the redirector at the caster: the ability's pawn is swapped for the length of the
    /// Cast, and whatever psyfocus and neural heat that cast takes is handed back to the redirector and charged to the
    /// caster instead. "Away" moves a beam's or a spot's aim off to the side; a psycast that can only take a pawn fizzles.
    /// Shot-type abilities (Ability_ShootProjectile: Dismantle, rapid fire) are left to the projectile hook, one shot at a time.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SkyPsycastRedirect
    {
        /// <summary>Abilities that fire along a line (by class name), so crossing it counts as being aimed at.
        /// Other parts can add theirs.</summary>
        public static readonly HashSet<string> LineAbilities = new HashSet<string>
        {
            "Ability_CursedEnergyBlast", "Ability_GraniteBlast", "Ability_Purple", "Ability_PiercingBloodSingle",
            "Ability_PiercingBloodScatter", "Ability_WorldCuttingSlash",
        };
        public const float LineHalfWidth = 1.2f;   // proposal: how close to the line counts as in it

        private static readonly HashSet<Ability> active = new HashSet<Ability>();
        private static readonly FieldInfo TargetingField = AccessTools.Field(typeof(VEF.Abilities.AbilityDef), "targetingParameters");
        private static readonly FieldInfo PsyfocusField = AccessTools.Field(typeof(Pawn_PsychicEntropyTracker), "currentPsyfocus");
        private static readonly FieldInfo EntropyField = AccessTools.Field(typeof(Pawn_PsychicEntropyTracker), "currentEntropy");

        public class State
        {
            public bool added;
            public Pawn caster, redirector;
            public float psyfocus, entropy;
        }

        static SkyPsycastRedirect()
        {
            var h = new Harmony("mythraps.jujutsu.sky");
            var pre = new HarmonyMethod(typeof(SkyPsycastRedirect), nameof(Prefix)) { priority = Priority.Last };
            var fin = new HarmonyMethod(typeof(SkyPsycastRedirect), nameof(Finalizer));
            var types = new List<Type> { typeof(Ability) };
            types.AddRange(GenTypes.AllSubclasses(typeof(Ability)));
            foreach (var t in types)
            {
                var m = t.GetMethod("Cast", BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly, null,
                                    new[] { typeof(GlobalTargetInfo[]) }, null);
                if (m == null || m.IsAbstract) continue;
                try { h.Patch(m, prefix: pre, finalizer: fin); }
                catch (Exception e) { Log.Warning($"[JJK Yuta] Redirect could not watch {t.FullName}.Cast: {e.Message}"); }
            }
        }

        /// <summary>Runs a cast that was already checked (Copy paying once a part is picked) without checking it again.</summary>
        public static void Unwatched(Ability a, Action act)
        {
            bool added = active.Add(a);
            try { act(); }
            finally { if (added) active.Remove(a); }
        }

        public static bool Prefix(Ability __instance, GlobalTargetInfo[] __0, ref State __state)
        {
            if (__instance == null || __0 == null || __0.Length == 0 || active.Contains(__instance)) return true;   // inner base.Cast calls
            active.Add(__instance);
            __state = new State { added = true };
            var caster = __instance.pawn;
            var map = caster?.Map;
            if (map == null || !caster.Spawned || __instance is Ability_ShootProjectile) return true;
            bool line = LineAbilities.Contains(__instance.GetType().Name);
            float range = line ? Mathf.Max(1f, __instance.GetRangeForPawn()) : 0f;
            var pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                var r = pawns[i];
                if (r == caster || r.Dead || !r.HostileTo(caster)) continue;
                bool redirect = Jujutsu.Has(r, SkyDefOf.JJK_SkyRedirect);
                bool reads = GWarstaff.Reading(r, caster);
                if (!redirect && !reads) continue;
                bool aimed = false;
                for (int k = 0; k < __0.Length; k++)
                    if (__0[k].Thing == r || (!__0[k].HasThing && __0[k].Cell == r.Position)) aimed = true;
                if (!aimed && !(line && OnLine(caster, __0[0].Cell, range, r))) continue;
                if (!redirect && !Rand.Chance(GWarstaff.RangedDodge)) continue;   // the read: a dodge, or nothing
                // back needs a psycaster to cast it from; anyone else can only bend it away
                bool canSendBack = r.health.hediffSet.hediffs.Any(h => h is VanillaPsycastsExpanded.Hediff_PsycastAbilities);
                if (redirect && canSendBack && Rand.Chance(SkyRedirect.BackChance)) return Back(__instance, __0, caster, r, line, __state);
                return Away(__instance, __0, caster, r, line, redirect ? "Bent away" : "Dodged");
            }
            return true;
        }

        public static void Finalizer(Ability __instance, State __state)
        {
            if (__state == null) return;
            if (__state.added) active.Remove(__instance);
            var r = __state.redirector;
            if (r == null) return;
            __instance.pawn = __state.caster;
            // the cast was paid as if the redirector had cast it: hand that back, and charge the caster instead
            var rt = r.psychicEntropy; var ct = __state.caster?.psychicEntropy;
            if (rt == null || PsyfocusField == null || EntropyField == null) return;
            float spent = __state.psyfocus - rt.CurrentPsyfocus, heat = rt.EntropyValue - __state.entropy;
            PsyfocusField.SetValue(rt, __state.psyfocus);
            EntropyField.SetValue(rt, __state.entropy);
            if (ct == null) return;
            if (spent > 0f) ct.OffsetPsyfocusDirectly(-spent);
            if (heat > 0f) ct.TryAddEntropy(heat, __state.caster, false, true);
        }

        private static bool Back(Ability a, GlobalTargetInfo[] targets, Pawn caster, Pawn r, bool line, State st)
        {
            bool any = false;
            for (int k = 0; k < targets.Length; k++)
                if (targets[k].Thing == r || (!targets[k].HasThing && targets[k].Cell == r.Position)) { targets[k] = new GlobalTargetInfo(caster); any = true; }
            if (!any && line) targets[0] = new GlobalTargetInfo(caster);
            st.caster = caster;
            st.redirector = r;
            st.psyfocus = r.psychicEntropy?.CurrentPsyfocus ?? 0f;
            st.entropy = r.psychicEntropy?.EntropyValue ?? 0f;
            a.pawn = r;
            MoteMaker.ThrowText(r.DrawPos, r.Map, "Redirected", Yuta.Purple, 1.5f);
            return true;
        }

        private static bool Away(Ability a, GlobalTargetInfo[] targets, Pawn caster, Pawn r, bool line, string text)
        {
            var map = r.Map;
            MoteMaker.ThrowText(r.DrawPos, map, text, Yuta.Purple, 1.5f);
            if (line)
            {
                // swing the line off her: the same length, turned 35 degrees to one side about the caster
                var from = caster.DrawPos; var to = targets[0].Cell.ToVector3Shifted();
                var dir = to - from; dir.y = 0f;
                var turned = Quaternion.AngleAxis(Rand.Bool ? 35f : -35f, Vector3.up) * dir;
                targets[0] = new GlobalTargetInfo((from + turned).ToIntVec3().ClampInsideMap(map), map);
                return true;
            }
            var tp = TargetingField?.GetValue(a.def) as TargetingParameters;   // not public in every VEF build
            bool cells = tp != null && tp.canTargetLocations;
            if (!cells) { Spent(a, targets); return false; }   // a technique that needs a pawn to land on: it fizzles
            for (int k = 0; k < targets.Length; k++)
            {
                if (targets[k].Thing != r && (targets[k].HasThing || targets[k].Cell != r.Position)) continue;
                var off = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f)).normalized * Rand.Range(3f, 5f);
                targets[k] = new GlobalTargetInfo((r.DrawPos + off).ToIntVec3().ClampInsideMap(map), map);
            }
            return true;
        }

        /// <summary>A fizzled psycast still costs what it costs (her call 2026-11-12): psyfocus, neural heat, hemogen and
        /// its cooldown, without anything landing.</summary>
        private static void Spent(Ability a, GlobalTargetInfo[] targets)
        {
            var p = a.pawn;
            if (p == null) return;
            a.cooldown = Find.TickManager.TicksGame + a.GetCooldownForPawn();
            var psy = a.def.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>();
            var h = p.health?.hediffSet?.hediffs.OfType<VanillaPsycastsExpanded.Hediff_PsycastAbilities>().FirstOrDefault();
            if (psy != null && h != null) h.UseAbility(psy.GetPsyfocusUsedByPawn(p), psy.GetEntropyUsedByPawn(p));
            if (a.def.modExtensions == null) return;
            foreach (var e in a.def.modExtensions)
                if (e is AbilityExtension_AbilityMod m && e.GetType().Name == "AbilityExtension_HemogenCost")
                    try { m.Cast(targets, a); } catch (Exception ex) { Log.Warning($"[JJK Yuta] fizzle hemogen cost: {ex.Message}"); }
        }

        /// <summary>Is <paramref name="r"/> on the line from the caster toward <paramref name="aim"/>, out to the range?</summary>
        private static bool OnLine(Pawn caster, IntVec3 aim, float range, Pawn r)
        {
            var a = caster.DrawPos; var b = aim.ToVector3Shifted();
            var d = new Vector2(b.x - a.x, b.z - a.z);
            float len = d.magnitude;
            if (len < 0.01f) return false;
            d /= len;
            float reach = Mathf.Max(len, range);
            var v = new Vector2(r.DrawPos.x - a.x, r.DrawPos.z - a.z);
            float t = Vector2.Dot(v, d);
            if (t <= 0.5f || t > reach) return false;
            return Mathf.Abs(v.x * d.y - v.y * d.x) <= LineHalfWidth;
        }
    }
}
