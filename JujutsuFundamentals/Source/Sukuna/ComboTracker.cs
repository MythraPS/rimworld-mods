using System.Collections.Generic;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Records when each pawn last cast Cleave and Dismantle, so Fire Arrow
    /// can require both hands to already be moving.</summary>
    public static class ComboTracker
    {
        private static readonly Dictionary<int, int> lastCleave    = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> lastDismantle = new Dictionary<int, int>();

        public static void Reset() { lastCleave.Clear(); lastDismantle.Clear(); }

        public static void NoteCleave(Pawn p)
        {
            if (p != null) lastCleave[p.thingIDNumber] = Find.TickManager.TicksGame;
        }

        public static void NoteDismantle(Pawn p)
        {
            if (p != null) lastDismantle[p.thingIDNumber] = Find.TickManager.TicksGame;
        }

        public static bool Ready(Pawn p, int windowTicks)
        {
            if (p == null) return false;
            int now = Find.TickManager.TicksGame;
            return lastCleave.TryGetValue(p.thingIDNumber, out int c)
                && lastDismantle.TryGetValue(p.thingIDNumber, out int d)
                && now - c <= windowTicks && now - d <= windowTicks;
        }
    }

    /// <summary>Dismantle fires a real projectile, so its damage lands when the
    /// projectile arrives rather than at cast time. This queues the scaled numbers
    /// for the impact.</summary>
    public static class SukunaDamage
    {
        public static void Pending(Pawn victim, Pawn caster, float damage, float armorPen)
        {
            if (victim == null || victim.Dead) return;
            victim.TakeDamage(new DamageInfo(
                SukunaDefOf.Sukuna_Slash, damage, armorPen, -1f, caster));
        }
    }
}
