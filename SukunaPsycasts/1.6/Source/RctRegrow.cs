using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Reverse cursed technique grows back lost body parts once the wounds are closed (her call 2026-11-02).
    /// One part at a time, the part closest to the body first (a regrown arm brings its hand and fingers
    /// with it). Yuji's two lost fingers (left ring finger and pinky) never come back.
    /// </summary>
    public static class RctRegrow
    {
        /// <summary>Healing needed per point of the part's max health.</summary>
        public const float CostPerHp = 0.5f;   // was 1.5; her call 2026-11-07: regrow limbs mid-battle

        public static readonly string[] YujiKeepsLost = { "left ring finger", "left pinky" };

        private static bool Exempt(Pawn p, BodyPartRecord part) =>
            Yuji.Is(p) && part.customLabel != null && YujiKeepsLost.Contains(part.customLabel);

        private static int Depth(BodyPartRecord r) { int d = 0; for (var x = r.parent; x != null; x = x.parent) d++; return d; }

        public static Hediff_MissingPart Next(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>()
             .Where(h => h.Part != null && !Exempt(p, h.Part))
             .OrderBy(h => Depth(h.Part)).FirstOrDefault();

        /// <summary>Spend <paramref name="amount"/> of healing on the next part; true while regrowing.</summary>
        public static bool Tick(Pawn p, ref float progress, float amount)
        {
            var miss = Next(p);
            if (miss == null) { progress = 0f; return false; }
            progress += amount;
            var part = miss.Part;
            if (progress < part.def.GetMaxHealth(p) * CostPerHp) return true;
            progress = 0f;

            // RestorePart brings back every child too; put back the losses Yuji keeps.
            var keep = p.health.hediffSet.hediffs.OfType<Hediff_MissingPart>()
                        .Where(h => h.Part != null && Exempt(p, h.Part)).Select(h => h.Part.customLabel).ToList();
            p.health.RestorePart(part, null, true);
            foreach (var label in keep) ModuloYuji.RemoveFinger(p, label);
            if (p.Spawned)
                MoteMaker.ThrowText(p.DrawPos, p.Map, part.LabelCap + " regrown", 3f);
            return true;
        }
    }
}
