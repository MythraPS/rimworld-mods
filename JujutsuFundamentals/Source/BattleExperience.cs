using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;


namespace JujutsuFundamentals
{
    /// <summary>
    /// Battle experience (her call 2026-11-11): real fights also raise psycast experience, for every psycaster.
    /// VPE itself only gives experience for psyfocus spent: 1 XP per 1% of the bar (times its own setting), and
    /// level 1 -> 2 takes 100 XP, each level after x1.15.
    ///
    /// "Real" = the other side is hostile to the psycaster (raiders, manhunters, hostile mechs and entities) and was
    /// still standing. Social fights, hunting, sparring and hitting downed or captured enemies give nothing.
    /// Numbers are proposals, all times the "Battle experience" setting (0 = off):
    ///   dealing damage: 0.25 XP per point, taking damage: 0.25 XP per point, each hit capped at 8 XP;
    ///   a kill: 10 XP x the victim's body size x (1 + their psycast level / 10).
    /// Support pawns gain a trickle too (BattleSupportExperience).
    /// Yuji's and Modulo Yuji's double experience applies on top (it patches GainExperience).
    /// </summary>
    public static class BattleExperience
    {
        public const float PerDamageDealt = 0.25f, PerDamageTaken = 0.25f, HitCap = 8f, KillBase = 10f;

        public static float Factor => JujutsuMod.Settings?.battleXp ?? 1f;

        /// <summary>A real enemy: hostile, and up (not downed, not a prisoner).</summary>
        public static bool RealEnemy(Pawn of, Pawn enemy) =>
            of != null && enemy != null && of != enemy && !enemy.Downed && !enemy.IsPrisoner && enemy.HostileTo(of);

        public static void Give(Pawn p, float xp)
        {
            if (p == null || p.Dead || xp <= 0f || Factor <= 0f) return;
            var psy = VanillaPsycastsExpanded.PsycastUtility.Psycasts(p);
            if (psy == null) return;
            psy.GainExperience(xp * Factor, true);
        }

        public static int PsycastLevel(Pawn p) => VanillaPsycastsExpanded.PsycastUtility.Psycasts(p)?.level ?? 0;
    }

    /// <summary>
    /// Support (her call 2026-11-11): psycasters who take part in a fight without dealing or taking damage still learn
    /// from it. Every 250 ticks while a real enemy (hostile, standing, not a prisoner) is on the map, a psycaster who is
    /// up and doing their part gets 1 XP (proposal: about 10 XP an in-game hour) times the setting. Doing their part =
    /// drafted, within 25 cells of a real enemy, or tending, rescuing or putting out fires.
    /// </summary>
    public class BattleSupportExperience : GameComponent
    {
        public const int Interval = 250;
        public const float PerInterval = 1f, NearRange = 25f;

        private static readonly System.Collections.Generic.List<Pawn> enemies = new System.Collections.Generic.List<Pawn>();

        public BattleSupportExperience(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % Interval != 113 || BattleExperience.Factor <= 0f) return;
            foreach (var map in Find.Maps) TickMap(map);
        }

        private static void TickMap(Map map)
        {
            var all = map.mapPawns.AllPawnsSpawned;
            foreach (var p in all)
            {
                if (p.Dead || p.Downed || p.IsPrisoner || !p.RaceProps.Humanlike || VanillaPsycastsExpanded.PsycastUtility.Psycasts(p) == null) continue;
                enemies.Clear();
                // only enemies actually threatening (not dormant mech clusters or sleeping ancient dangers)
                foreach (var e in all)
                    if (BattleExperience.RealEnemy(p, e) && (p.Faction == null || GenHostility.IsActiveThreatTo(e, p.Faction, false, false))) enemies.Add(e);
                if (enemies.Count == 0) continue;   // no battle for this pawn here
                if (Helping(p, enemies)) BattleExperience.Give(p, PerInterval);
            }
            enemies.Clear();
        }

        private static bool Helping(Pawn p, System.Collections.Generic.List<Pawn> foes)
        {
            if (p.Drafted) return true;
            var job = p.CurJobDef;
            if (job != null && (job == JobDefOf.TendPatient || job == JobDefOf.Rescue || job == JobDefOf.BeatFire
                                || job == JobDefOf.TendEntity)) return true;
            foreach (var e in foes) if (e.Position.InHorDistOf(p.Position, NearRange)) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_BattleExperienceHit
    {
        public static void Prefix(Pawn __instance, out bool __state) => __state = !__instance.Downed;   // up when hit

        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt, bool __state)
        {
            if (!__state || totalDamageDealt <= 0f || !(dinfo.Instigator is Pawn attacker)) return;
            var victim = __instance;
            if (!attacker.HostileTo(victim)) return;
            float dealt = Mathf.Min(totalDamageDealt * BattleExperience.PerDamageDealt, BattleExperience.HitCap);
            float taken = Mathf.Min(totalDamageDealt * BattleExperience.PerDamageTaken, BattleExperience.HitCap);
            // the attacker earns it if the victim was a real enemy (up when hit); the victim earns it if the attacker is one
            if (!victim.IsPrisoner) BattleExperience.Give(attacker, dealt);
            if (BattleExperience.RealEnemy(victim, attacker)) BattleExperience.Give(victim, taken);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_BattleExperienceKill
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, out Pawn __state)
        {
            __state = null;
            var killer = dinfo?.Instigator as Pawn;
            if (killer != null && !__instance.Dead && BattleExperience.RealEnemy(killer, __instance)) __state = killer;
        }

        public static void Postfix(Pawn __instance, Pawn __state)
        {
            if (__state == null || !__instance.Dead) return;
            float size = Mathf.Max(0.2f, __instance.BodySize);
            BattleExperience.Give(__state, BattleExperience.KillBase * size * (1f + BattleExperience.PsycastLevel(__instance) / 10f));
        }
    }
}
