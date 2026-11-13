using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Choso, Modulo Yuji and Yuji make their own blood (her call 2026-11-11: they're not vampires). Their hemogen refills on
    /// its own and the Hemogenic gene's daily loss is cancelled, and they never take hemogen packs.
    /// Proposal: 1 hemogen every 250 ticks, about 10 an in-game hour.
    /// </summary>
    public class BloodNotVampire : GameComponent
    {
        public const int Interval = 250;
        public const float PerInterval = 0.01f;

        public BloodNotVampire(Game game) { }

        public static bool Applies(Pawn p) => ChosoTracker.Is(p) || ModuloYuji.Is(p) || Yuji.Is(p)
                                              || JujutsuFundamentals.Innate.Marked(p, JujutsuFundamentals.Innate.Blood);   // born with Blood Manipulation   // Yuji too once Death Painting makes him hemogenic (her call 2026-11-11)

        public override void GameComponentTick()
        {
            if (!ModsConfig.BiotechActive || Find.TickManager.TicksGame % Interval != 71) return;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                    if (Applies(p)) Refill(p);
            foreach (var p in Find.WorldPawns.AllPawnsAlive)
                if (Applies(p)) Refill(p);
        }

        private static void Refill(Pawn p)
        {
            if (p.Dead) return;
            var g = p.genes?.GetFirstGeneOfType<Gene_Hemogen>();
            if (g == null) return;
            g.hemogenPacksAllowed = false;
            // what the gene drains over the same span, plus the refill
            float gain = PerInterval + g.ResourceLossPerDay * Interval / GenDate.TicksPerDay;
            if (g.Value < g.Max) g.Value = Mathf.Min(g.Max, g.Value + gain);
        }
    }
}
