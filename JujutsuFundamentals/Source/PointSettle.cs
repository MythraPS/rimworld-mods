using System.Collections.Generic;
using System.Linq;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;

namespace JujutsuFundamentals
{
    /// <summary>
    /// A character arrives with no psycast points to spend (her call 2026-11-11). Every path they have open and every
    /// technique they were given costs one point, as in the Psycasts tab; whatever is left goes into psycaster stats,
    /// as the Upgrade button does (SpentPoints + ImproveStats). Points from later levels are the player's.
    /// Settled on the next frame after they're made, once every maker has given its techniques. Gojo settles his own
    /// (Satoru.SpendPoints), and Modulo Yuji's arrive spent.
    /// </summary>
    public class PointSettle : GameComponent
    {
        private static readonly List<Pawn> pending = new List<Pawn>();

        public PointSettle(Game game) { pending.Clear(); }

        public static void Queue(Pawn p) { if (p != null && !pending.Contains(p)) pending.Add(p); }

        public override void GameComponentUpdate()
        {
            if (pending.Count == 0) return;
            var now = pending.ToList();
            pending.Clear();
            foreach (var p in now) Settle(p);
        }

        public static void Settle(Pawn p)
        {
            if (p == null || p.Destroyed && !p.Dead) return;
            var psy = p.health?.hediffSet?.hediffs.OfType<Hediff_PsycastAbilities>().FirstOrDefault();
            if (psy == null || psy.points <= 0) return;
            int cost = psy.unlockedPaths?.Count ?? 0;
            var comp = p.GetComp<CompAbilities>();
            if (comp != null)
                cost += comp.LearnedAbilities.Count(a => a?.def?.GetModExtension<AbilityExtension_Psycast>() != null);
            int pay = System.Math.Min(cost, psy.points);
            if (pay > 0) psy.SpentPoints(pay);
            if (psy.points > 0) { int rest = psy.points; psy.SpentPoints(rest); psy.ImproveStats(rest); }
        }
    }
}
