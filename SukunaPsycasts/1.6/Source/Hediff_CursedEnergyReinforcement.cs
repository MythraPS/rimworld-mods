using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;

namespace SukunaPsycasts
{
    /// <summary>
    /// Cursed Energy Reinforcement (呪力強化). The body is coated in cursed energy:
    /// harder skin, heavier blows, faster feet. Per the JJK wiki, Sukuna leans on it
    /// for most of his strength and durability feats (tanking a 200% Purple with
    /// reinforced hands, ch. 234).
    ///
    /// PASSIVE: always present once the node is learned (HediffComp_VesselPassives),
    /// at half output. The "Raise cursed energy output" toggle pays psyfocus to set
    /// Boosted, which doubles it (user 2026-10-23: "Sukuna always has it on
    /// basically. But the toggle can increase his output").
    ///
    /// Everything scales with Scaling.Power (psychic sensitivity × fingers), so the
    /// stage is rebuilt every 250 ticks. Tempered (Furnace known) adds heat comfort; heat armour is always on
    /// (2026-11-07).
    /// </summary>
    public class Hediff_CursedEnergyReinforcement : HediffWithComps
    {
        // Values at Power 1 (20 fingers, sensitivity 100%) WITH the output raised.
        // The passive gives PassiveShare of each.
        // armour doubled and heat armour always on (her call 2026-11-07: bullets and fire hurt him too much)
        public const float SharpArmour   = 1.20f;
        public const float BluntArmour   = 1.20f;
        public const float HeatArmour    = 0.80f;
        /// <summary>Damage taken is multiplied by 0.5^(2k): 50% passive and 25% raised at 20 fingers.</summary>
        public const float DamageHalvings = 2f;
        public const float FireHalvings   = 2f;   // chance to catch fire: x0.5 passive, x0.25 raised (Power 1)
        public const float MeleeDamage   = 0.50f;  // +50% melee damage
        public const float MoveSpeed     = 1.00f;  // cells per second
        public const float PassiveShare  = 0.50f;
        public const float PowerCap      = 2.00f;  // stop sensitivity stacking it past sanity

        private bool boosted;
        public bool Boosted
        {
            get { return boosted; }
            set { if (boosted != value) { boosted = value; dirty = true; } }
        }

        private HediffStage stage;
        private bool dirty = true;
        private bool building;
        private float builtFor = -1f;

        // Rebuild reads psychic sensitivity, and reading any stat asks every hediff
        // for its CurStage, this one included. While building, that inner call gets
        // the stage as it was (or an empty one the first time) instead of starting
        // another build; without this the game froze in an endless loop (2026-10-28).
        private static readonly HediffStage Empty = new HediffStage { becomeVisible = true };
        private bool builtTempered;

        private static StatDef S(string name) => DefDatabase<StatDef>.GetNamedSilentFail(name);

        public bool Tempered
        {
            get
            {
                var comp = pawn?.GetComp<CompAbilities>();
                return comp != null && SukunaAbilityDefOf.Sukuna_FireArrow != null
                       && comp.HasAbility(SukunaAbilityDefOf.Sukuna_FireArrow);
            }
        }

        public override HediffStage CurStage
        {
            get
            {
                if (dirty && !building) Rebuild();
                return stage ?? Empty;
            }
        }

        public override string LabelInBrackets => boosted ? "raised output" : null;

        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (pawn.IsHashIntervalTick(250, delta)) Rebuild();
        }

        private static void Add(List<StatModifier> l, string stat, float v)
        {
            var d = S(stat);
            if (d != null && v != 0f) l.Add(new StatModifier { stat = d, value = v });
        }

        private void Rebuild()
        {
            if (building) return;
            building = true;
            try { Build(); }
            finally { building = false; }
        }

        private void Build()
        {
            float p = Mathf.Clamp(Scaling.Power(pawn), 0f, PowerCap);
            p = Mathf.Round(p * 100f) / 100f;
            bool tempered = Tempered;
            if (!dirty && stage != null && Mathf.Approximately(p, builtFor) && builtTempered == tempered) return;
            dirty = false;

            float k = p * (boosted ? 1f : PassiveShare);
            var offsets = new List<StatModifier>();
            Add(offsets, "ArmorRating_Sharp", SharpArmour * k);
            Add(offsets, "ArmorRating_Blunt", BluntArmour * k);
            Add(offsets, "MoveSpeed", MoveSpeed * k);
            Add(offsets, "ArmorRating_Heat", HeatArmour * k);
            if (tempered) Add(offsets, "ComfyTemperatureMax", 60f);
            var factors = new List<StatModifier>();
            Add(factors, "IncomingDamageFactor", Mathf.Pow(0.5f, DamageHalvings * k));
            Add(factors, "MeleeDamageFactor", 1f + MeleeDamage * k);
            // resistant to every other fire (her call 2026-11-08): harder to set alight, like damage taken
            Add(factors, "Flammability", Mathf.Pow(0.5f, FireHalvings * k));

            stage = new HediffStage
            {
                label = tempered ? "tempered" : null,
                statOffsets = offsets,
                statFactors = factors,
                becomeVisible = true,
            };
            bool first = builtFor < 0f;
            builtFor = p;
            builtTempered = tempered;
            if (!first) pawn.health?.Notify_HediffChanged(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref boosted, "sukunaBoosted", false);
        }
    }
}
