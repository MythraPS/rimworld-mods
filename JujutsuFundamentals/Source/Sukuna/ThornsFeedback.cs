using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using VEF.Hediffs;

namespace SukunaPsycasts
{
    /// <summary>
    /// Thorns (her ask 2026-11-08: she couldn't tell it worked). VEF's shield cuts the melee attacker
    /// with no picture and no log line; for Thorns this does the same cut, plus Cleave's slash on the
    /// attacker and a combat log entry.
    /// </summary>
    [HarmonyPatch(typeof(HediffComp_Shield), "ApplyDamage")]
    public static class Patch_ThornsFeedback
    {
        public static bool Prefix(HediffComp_Shield __instance, DamageInfo dinfo)
        {
            if (__instance.parent?.def != SukunaDefOf.Sukuna_ThornsShield) return true;
            var attacker = dinfo.Instigator;
            if (attacker == null || attacker.Destroyed) return false;
            var props = (HediffCompProperties_Shield)__instance.props;
            // Next tick, not now: cut in the middle of its own swing, the attacker's orders were reset while
            // it still held the attack and it restarted its job ten times in a tick (her log 2026-11-09).
            ThornsQueue.Add(attacker, __instance.Pawn, props);
            return false;
        }
    }

    public class ThornsQueue : GameComponent
    {
        private struct Cut { public Thing attacker; public Pawn owner; public HediffCompProperties_Shield props; }
        private static readonly List<Cut> queue = new List<Cut>();

        public ThornsQueue(Game game) => queue.Clear();

        public static void Add(Thing attacker, Pawn owner, HediffCompProperties_Shield props) =>
            queue.Add(new Cut { attacker = attacker, owner = owner, props = props });

        public override void GameComponentTick()
        {
            if (queue.Count == 0) return;
            var now = queue.ToArray();
            queue.Clear();
            foreach (var c in now)
            {
                var attacker = c.attacker;
                if (attacker == null || attacker.Destroyed || !attacker.Spawned) continue;
                // same cut as VEF makes (no instigator, so it can't set off Black Flash or immunity rules)
                var cut = new DamageInfo(c.props.damageType, c.props.damageAmount, c.props.armorPenetration, -1f, null);
                for (int i = 0; i < 2; i++) SlashUtility.SlashFleck(attacker);
                var result = attacker.TakeDamage(cut);
                if (attacker is Pawn victim && Find.BattleLog != null)
                {
                    var entry = new BattleLogEntry_DamageTaken(victim, SukunaDefOf.Sukuna_DamageEvent_Thorns, c.owner);
                    Find.BattleLog.Add(entry);
                    result?.AssociateWithLog(entry);
                }
            }
        }
    }
}
