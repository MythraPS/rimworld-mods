using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// A vessel does not sit down and meditate to get the technique back. Psyfocus
    /// returns on its own, faster the more of him they carry.
    /// </summary>
    public class HediffCompProperties_PassivePsyfocus : HediffCompProperties
    {
        /// <summary>Fraction of the pool returned per in-game day at one finger.</summary>
        public float regenPerDayBase = 0.35f;

        /// <summary>Quadratic term on finger count. This is what makes a full vessel
        /// self-sustaining: the sustained techniques cost a flat rate to hold, so a
        /// linear return can never catch them, and a twenty-finger vessel is supposed
        /// to be able to stand there with all three up and still fill the bar.</summary>
        public float regenPerFingerSq = 0.13f;

        public HediffCompProperties_PassivePsyfocus()
            => compClass = typeof(HediffComp_PassivePsyfocus);
    }

    public class HediffComp_PassivePsyfocus : HediffComp
    {
        private const int Interval = 60;
        private int acc;

        public HediffCompProperties_PassivePsyfocus Props =>
            (HediffCompProperties_PassivePsyfocus)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            acc += delta;
            if (acc < Interval) return;
            int steps = acc / Interval;
            acc %= Interval;

            var pawn = Pawn;
            var tracker = pawn?.psychicEntropy;
            if (tracker == null || pawn.Dead) return;
            if (tracker.CurrentPsyfocus >= 1f) return;

            // Scales with fingers eaten, and the curve is deliberately steep at the
            // top end.
            //
            // The three sustained techniques cost a fixed 0.0067 of the pool per
            // second between them (thorns 0.0015, reinforcement 0.0040, RCT 0.0012),
            // and RCT's repair spends another 0.0072/s while it is actually closing
            // wounds. Anything under about 0.014/s therefore loses ground with
            // everything switched on, no matter how many fingers.
            //
            //   perDay = 0.35 * (0.5 + 0.25n + 0.13n^2)
            //     n=1  ->  0.31/day  = 0.00031/s   (unchanged; a one-finger vessel
            //                                       is still nearly helpless)
            //     n=10 ->  5.57/day  = 0.00557/s   (holds one or two, not all three)
            //     n=15 -> 11.66/day  = 0.01166/s   (holds all three at rest)
            //     n=20 -> 20.13/day  = 0.02013/s   (holds all three while repairing,
            //                                       and still gains)
            float fingers = Mathf.Max(1, Scaling.FingersEaten(pawn));
            float perDay  = Props.regenPerDayBase
                          * (0.5f + fingers * 0.25f + fingers * fingers * Props.regenPerFingerSq);
            float perSecond = perDay / 60000f * 60f;   // 60000 ticks a day, 60 per step
            tracker.OffsetPsyfocusDirectly(perSecond * steps);
        }

        public override string CompTipStringExtra =>
            "Psyfocus returns on its own.";
    }
}
