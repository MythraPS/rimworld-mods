using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Sorcerers study anomalies better (her call 2026-11-07): Anomaly's study efficiency
    /// x(1 + half their power). Added to the stat at startup, so the mod doesn't need Anomaly.</summary>
    public class StatPart_Sorcerer : StatPart
    {
        public const float PerPower = 0.5f;

        public static void Register()
        {
            var stat = DefDatabase<StatDef>.GetNamedSilentFail("StudyEfficiency");
            if (stat == null) return;
            if (stat.parts == null) stat.parts = new List<StatPart>();
            if (stat.parts.Exists(x => x is StatPart_Sorcerer)) return;
            stat.parts.Add(new StatPart_Sorcerer { parentStat = stat });
        }

        private static float Factor(StatRequest req) =>
            req.Thing is Pawn p && (Scaling.HasVessel(p) || Yuji.Is(p)) ? 1f + PerPower * Mathf.Max(0f, Scaling.Power(p)) : 1f;

        public override void TransformValue(StatRequest req, ref float val) => val *= Factor(req);

        public override string ExplanationPart(StatRequest req)
        {
            float f = Factor(req);
            return f > 1f ? "Sorcerer (cursed energy): x" + f.ToString("0.00") : null;
        }
    }
}
