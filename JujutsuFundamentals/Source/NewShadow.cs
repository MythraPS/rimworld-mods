using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Simple Domain (her calls 2026-11-10): a circle around the user that follows psychic sensitivity.
    /// The user stands still on the ground. Everyone of the user's side inside it is shielded as by
    /// Hollow Wicker Basket, and the user's attacks on anything inside it are faster. With Evening Moon
    /// learned, the blade also strikes enemies coming in and cuts down every hostile projectile crossing it.
    /// </summary>
    public class Hediff_SimpleDomain : HediffWithComps
    {
        public const float BaseRadius = 2.9f;
        public const float MaxRadius = 9.9f;
        public const float AttackSpeed = 0.6f;   // cooldown and aim time x0.6 on targets inside

        public static readonly HashSet<Hediff_SimpleDomain> Active = new HashSet<Hediff_SimpleDomain>();

        private float radius = -1f;
        private HashSet<int> inside = new HashSet<int>();
        /// <summary>Who's inside isn't saved: after a load the first look only notes them, so loading doesn't
        /// count as everyone coming in (a free Evening Moon strike on each enemy).</summary>
        private bool justLoaded;

        public float Radius
        {
            get
            {
                if (radius < 0f) radius = Mathf.Clamp(BaseRadius * Jujutsu.Sensitivity(pawn) * Jujutsu.Boost(pawn), 1.5f, MaxRadius * Jujutsu.RaisedFactor);
                return radius;
            }
        }

        public bool EveningMoon => Jujutsu.Knows(pawn, JJKAbilityDefOf.JJK_EveningMoon);

        public bool Covers(IntVec3 c) => pawn.Spawned && c.InHorDistOf(pawn.Position, Radius);
        public bool Covers(Vector3 v) => pawn.Spawned && (v.ToIntVec3() - pawn.Position).LengthHorizontal <= Radius;

        public override void PostAdd(DamageInfo? dinfo) { base.PostAdd(dinfo); Active.Add(this); }
        public override void PostRemoved() { base.PostRemoved(); Active.Remove(this); }

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            Active.Add(this);   // after loading a save
            if (pawn.IsHashIntervalTick(60, delta)) radius = -1f;
            if (pawn.IsHashIntervalTick(10, delta)) StandStill.Check(pawn, "simpledomain");
            if (pawn.Spawned && pawn.IsHashIntervalTick(6, delta)) Watch();
        }

        /// <summary>Evening Moon: strike each enemy as it comes into the circle.</summary>
        private void Watch()
        {
            var now = new HashSet<int>();
            foreach (var t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Radius, true))
            {
                if (!(t is Pawn other) || other == pawn || other.Dead || other.Downed || !other.HostileTo(pawn)) continue;
                now.Add(other.thingIDNumber);
                if (!justLoaded && !inside.Contains(other.thingIDNumber) && EveningMoon) EveningMoonStrike.Strike(pawn, other);
            }
            inside = now;
            justLoaded = false;
        }

        public override string LabelInBrackets => "radius " + Radius.ToString("0.0") + (EveningMoon ? ", evening moon" : "");

        public static Hediff_SimpleDomain Of(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(JJKDefOf.JJK_SimpleDomain) as Hediff_SimpleDomain;

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) { Active.Add(this); justLoaded = true; }
        }
    }

    /// <summary>Simple Domain is drawn as a smooth circle on the ground for as long as it is up (her call
    /// 2026-11-10), though the cells it covers are the game's usual blocky disc.</summary>
    [StaticConstructorOnStartup]   // holds Material fields: the game wants these made on the main thread
    public class SimpleDomainDrawer : MapComponent
    {
        private static Material mat, fill;
        private static Material Mat => mat ?? (mat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, new Color(0.6f, 0.8f, 1f, 0.8f)));
        /// <summary>The inside: translucent light blue (her call 2026-11-10), a soft-edged disc made once.</summary>
        private static Material Fill
        {
            get
            {
                if (fill != null) return fill;
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        byte a = (byte)(255 * Mathf.Clamp01((1f - r) * n / 4f));
                        px[y * n + x] = new Color32(255, 255, 255, a);
                    }
                tex.SetPixels32(px);
                tex.Apply();
                fill = MaterialPool.MatFrom(tex, ShaderDatabase.Transparent, new Color(0.6f, 0.82f, 1f, 0.22f));
                return fill;
            }
        }

        public SimpleDomainDrawer(Map map) : base(map) { }

        public override void MapComponentTick()
        {
            if (map == Find.Maps.FirstOrDefault()) EveningMoonStrike.RunQueue();   // once per tick, not once per map
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || Hediff_SimpleDomain.Active.Count == 0) return;
            foreach (var sd in Hediff_SimpleDomain.Active)
            {
                var p = sd.pawn;
                if (p == null || !p.Spawned || p.Map != map) continue;
                var c = p.Position.ToVector3Shifted();
                var f = c; f.y = AltitudeLayer.FloorEmplacement.AltitudeFor();
                float size = sd.Radius * 2f;
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(f, Quaternion.identity, new Vector3(size, 1f, size)), Fill, 0);
                c.y = AltitudeLayer.MetaOverlays.AltitudeFor();
                GenDraw.DrawCircleOutline(c, sd.Radius, Mat);
            }
        }
    }

    /// <summary>Faster attacks on targets inside the user's own Simple Domain.</summary>
    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldownTicks))]
    public static class Patch_SimpleDomain_Cooldown
    {
        public static void Postfix(Verb ownerVerb, Pawn attacker, ref int __result)
        {
            var sd = Hediff_SimpleDomain.Of(attacker);
            if (sd != null && ownerVerb != null && ownerVerb.CurrentTarget.IsValid && sd.Covers(ownerVerb.CurrentTarget.Cell))
                __result = Mathf.RoundToInt(__result * Hediff_SimpleDomain.AttackSpeed);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.WarmupTime), MethodType.Getter)]
    public static class Patch_SimpleDomain_Warmup
    {
        public static void Postfix(Verb __instance, ref float __result)
        {
            if (!(__instance.caster is Pawn p)) return;
            var sd = Hediff_SimpleDomain.Of(p);
            if (sd != null && __instance.CurrentTarget.IsValid && sd.Covers(__instance.CurrentTarget.Cell))
                __result *= Hediff_SimpleDomain.AttackSpeed;
        }
    }

    /// <summary>Evening Moon Sword Drawing: inside Simple Domain, the blade strikes whatever comes in.</summary>
    public static class EveningMoonStrike
    {
        private static readonly Dictionary<int, int> lastStruck = new Dictionary<int, int>();
        private static readonly List<(Pawn user, Pawn target)> queued = new List<(Pawn, Pawn)>();

        public static void Queue(Pawn user, Pawn target) => queued.Add((user, target));

        /// <summary>A new or loaded game starts with nothing queued (StaticReset).</summary>
        public static void Reset() { queued.Clear(); lastStruck.Clear(); }

        /// <summary>Runs the queued counter-strikes, once per tick (SimpleDomainDrawer).</summary>
        public static void RunQueue()
        {
            if (queued.Count == 0) return;
            var now = queued.ToList();
            queued.Clear();
            foreach (var q in now)
                if (q.user != null && q.target != null && q.user.Spawned && q.target.Spawned && !q.target.Dead && !q.user.Dead
                    && q.user.Map == q.target.Map)
                    Strike(q.user, q.target);
        }

        public static void Strike(Pawn user, Pawn target)
        {
            if (user.Downed || user.stances?.stunner?.Stunned == true) return;
            int now = Find.TickManager.TicksGame;
            if (lastStruck.TryGetValue(target.thingIDNumber, out var t) && now - t < 30) return;
            lastStruck[target.thingIDNumber] = now;

            var verb = user.meleeVerbs?.TryGetMeleeVerb(target);
            if (verb == null) return;
            float dmg = verb.verbProps.AdjustedMeleeDamageAmount(verb, user);
            float ap = verb.verbProps.AdjustedArmorPenetration(verb, user);
            var dinfo = new DamageInfo(verb.GetDamageDef(), dmg, ap, -1f, user, null, verb.EquipmentSource?.def,
                                       DamageInfo.SourceCategory.ThingOrUnknown, target);
            if (verb.HediffCompSource != null) dinfo.SetWeaponHediff(verb.HediffCompSource.Def);
            dinfo.SetTool(verb.tool);
            BlackFlashRules.meleeDepth++;   // a sword stroke: Black Flash and the coated weapon apply
            try { target.TakeDamage(dinfo); }
            finally { BlackFlashRules.meleeDepth = Mathf.Max(0, BlackFlashRules.meleeDepth - 1); }
            if (target.Spawned)
            {
                FleckMaker.ThrowMicroSparks(target.DrawPos, target.Map);
                MoteMaker.ThrowText(target.DrawPos + new Vector3(0f, 0f, 0.4f), target.Map, "Evening moon", new Color(0.75f, 0.85f, 1f), 1.5f);
            }
        }

        /// <summary>The Simple Domain with Evening Moon that covers this cell, against this enemy.</summary>
        public static Hediff_SimpleDomain Guard(IntVec3 cell, Map map, Thing enemy)
        {
            foreach (var sd in Hediff_SimpleDomain.Active)
            {
                var u = sd.pawn;
                if (u == null || u.Map != map || !u.Spawned || u.Downed || !sd.EveningMoon) continue;
                if (enemy != null && !enemy.HostileTo(u)) continue;
                if (sd.Covers(cell)) return sd;
            }
            return null;
        }
    }

    /// <summary>An enemy attacking anyone inside the circle is struck first.</summary>
    [HarmonyPatch(typeof(Verb_MeleeAttack), "TryCastShot")]
    public static class Patch_EveningMoon_Counter
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Verb_MeleeAttack __instance)
        {
            if (!(__instance.caster is Pawn attacker) || !attacker.Spawned) return;
            var target = __instance.CurrentTarget;
            if (!target.IsValid) return;
            var sd = EveningMoonStrike.Guard(target.Cell, attacker.Map, attacker);
            // struck on the next tick, not inside the attacker's own swing: killing it mid-swing would leave
            // the game's attack code working on a dead pawn (2026-11-10, NRE in Verb.TryCastNextBurstShot)
            if (sd != null && sd.pawn != attacker) EveningMoonStrike.Queue(sd.pawn, attacker);
        }
    }

    /// <summary>Every hostile projectile crossing a circle with Evening Moon is cut down (her call: any and all).</summary>
    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_EveningMoon_Projectiles
    {
        public static bool Prefix(Projectile __instance)
        {
            if (Hediff_SimpleDomain.Active.Count == 0 || !__instance.Spawned) return true;
            var launcher = __instance.Launcher;
            if (launcher == null) return true;
            var pos = __instance.ExactPosition;
            var sd = EveningMoonStrike.Guard(pos.ToIntVec3(), __instance.Map, launcher);
            if (sd == null || !sd.Covers(pos)) return true;
            var map = __instance.Map;
            FleckMaker.ThrowMicroSparks(pos, map);
            MoteMaker.ThrowText(pos, map, "Cut", new Color(0.75f, 0.85f, 1f), 1.2f);
            PsycastContext.Forget(__instance);
            __instance.Destroy();
            return false;
        }
    }

    /// <summary>Batto Sword Drawing: a short attack-speed boost (her call 2026-11-10). Needs a melee
    /// weapon in hand, or Hazy Moon's blade.</summary>
    public class Ability_Batto : Ability
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (!Jujutsu.HoldsMeleeWeapon(pawn) && !Jujutsu.Has(pawn, JJKDefOf.JJK_HazyMoon))
            {
                reason = "Needs a melee weapon in hand, or Hazy Moon's blade.";
                return false;
            }
            return base.IsEnabledForPawn(out reason);
        }
    }

    /// <summary>
    /// Hazy Moon (her calls 2026-11-10): a toggled blade of cursed energy, even with no melee weapon.
    /// While it's up it replaces every other melee attack, a gun's butt included; the gun stays equipped.
    /// Its damage follows the user's power. Simple Sidearms must not swap a sidearm in for it.
    /// </summary>
    public static class HazyMoon
    {
        public static bool On(Pawn p) => Jujutsu.Has(p, JJKDefOf.JJK_HazyMoon);

        /// <summary>A held melee weapon coated by Hazy Moon hits this much harder (x power; proposal).</summary>
        public const float CoatedWeaponBonus = 0.25f;

        public static void Scale(Pawn attacker, ref DamageInfo dinfo)
        {
            if (dinfo.WeaponLinkedHediff == JJKDefOf.JJK_HazyMoon) { dinfo.SetAmount(dinfo.Amount * Jujutsu.Power(attacker) * Jujutsu.Boost(attacker)); return; }
            if (On(attacker) && dinfo.Weapon != null && dinfo.Weapon.IsMeleeWeapon && BlackFlashRules.InMelee)
                dinfo.SetAmount(dinfo.Amount * (1f + CoatedWeaponBonus * Jujutsu.Power(attacker) * Jujutsu.Boost(attacker)));
        }
    }

    /// <summary>Hazy Moon's blade drawn in the pawn's hands (her ask 2026-11-10): a light blue katana, placed and
    /// aimed exactly as the game draws a held weapon. Only when no weapon is held; a gun stays the one drawn.</summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
    public static class Patch_HazyMoon_Draw
    {
        private static Thing blade;
        private static Thing Blade => blade ?? (blade = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("JJK_HazyMoonBlade")));

        public static void Postfix(Pawn pawn, Vector3 drawPos, Rot4 facing, PawnRenderFlags flags)
        {
            if (pawn == null || pawn.Dead || pawn.equipment?.Primary != null || !HazyMoon.On(pawn)) return;
            if (pawn.CurJob?.def?.neverShowWeapon == true) return;
            float dist = pawn.ageTracker?.CurLifeStage?.equipmentDrawDistanceFactor ?? 1f;
            var busy = pawn.stances?.curStance as Stance_Busy;
            if (!flags.HasFlag(PawnRenderFlags.NeverAimWeapon) && busy != null && !busy.neverAimWeapon && busy.focusTarg.IsValid)
            {
                var targ = busy.focusTarg.HasThing ? busy.focusTarg.Thing.DrawPos : busy.focusTarg.Cell.ToVector3Shifted();
                float angle = (targ - pawn.DrawPos).MagnitudeHorizontalSquared() > 0.001f ? (targ - pawn.DrawPos).AngleFlat() : 0f;
                PawnRenderUtility.DrawEquipmentAiming(Blade, drawPos + new Vector3(0f, 0f, 0.4f).RotatedBy(angle) * dist, angle);
                return;
            }
            if (!PawnRenderUtility.CarryWeaponOpenly(pawn)) return;
            if (facing == Rot4.South) PawnRenderUtility.DrawEquipmentAiming(Blade, drawPos + new Vector3(0f, 0f, -0.22f) * dist, 143f);
            else if (facing == Rot4.North) PawnRenderUtility.DrawEquipmentAiming(Blade, drawPos + new Vector3(0f, -0.0366f, -0.11f) * dist, 143f);
            else if (facing == Rot4.East) PawnRenderUtility.DrawEquipmentAiming(Blade, drawPos + new Vector3(0.2f, 0f, -0.22f) * dist, 143f);
            else PawnRenderUtility.DrawEquipmentAiming(Blade, drawPos + new Vector3(-0.2f, 0f, -0.22f) * dist, 217f);
        }
    }

    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.GetUpdatedAvailableVerbsList))]
    public static class Patch_HazyMoon_OnlyBlade
    {
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Pawn_MeleeVerbs), "pawn");
        private static Pawn PawnRef(Pawn_MeleeVerbs x) => (Pawn)PawnField.GetValue(x);

        public static void Postfix(Pawn_MeleeVerbs __instance, List<VerbEntry> __result)
        {
            var pawn = PawnRef(__instance);
            if (__result == null || !HazyMoon.On(pawn)) return;
            if (Jujutsu.HoldsMeleeWeapon(pawn)) { __result.RemoveAll(IsBlade); return; }   // the sword is used, coated (her call 2026-11-10)
            if (__result.Any(IsBlade)) __result.RemoveAll(e => !IsBlade(e));
        }

        private static bool IsBlade(VerbEntry e) => e.verb?.HediffCompSource?.Def == JJKDefOf.JJK_HazyMoon;
    }

    /// <summary>Simple Sidearms swaps to a melee sidearm when an enemy closes in. With Hazy Moon's
    /// blade up, those swaps are skipped, so the gun stays in hand and the blade does the cutting.</summary>
    public static class SidearmsCompat
    {
        public static void Patch(Harmony h)
        {
            var t = AccessTools.TypeByName("PeteTimesSix.SimpleSidearms.Utilities.WeaponAssingment");
            if (t == null) return;
            var skip = new HarmonyMethod(typeof(SidearmsCompat), nameof(Skip));
            foreach (var name in new[] { "chooseOptimalMeleeForAttack", "tryCQCWeaponSwapToMelee", "doCQC" })
            {
                var m = AccessTools.Method(t, name);
                if (m == null) { Log.Warning("[JJK Core] Simple Sidearms: " + name + " not found."); continue; }
                h.Patch(m, prefix: skip);
            }
            Log.Message("[JJK Core] Simple Sidearms found: Hazy Moon keeps it from swapping weapons.");
        }

        // __result only exists on tryCQCWeaponSwapToMelee (bool); Harmony leaves it out on the void ones.
        public static bool Skip(Pawn pawn)
        {
            return !HazyMoon.On(pawn);
        }
    }
}
