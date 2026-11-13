using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Cursed Energy Reinforcement: cursed energy coating the body. Harder skin, heavier blows, faster
    /// feet, and (her call 2026-11-10) the melee weapon in hand is coated too. Always on at half output
    /// once learned; the toggle pays psyfocus to raise it to full. Numbers are Sukuna's, at power 1.
    /// The stage is rebuilt every 250 ticks because power follows psychic sensitivity.
    /// </summary>
    public class Hediff_Reinforcement : HediffWithComps
    {
        public const float SharpArmour = 1.20f;
        public const float BluntArmour = 1.20f;
        public const float HeatArmour = 0.80f;
        /// <summary>Damage taken x0.5^(2k).</summary>
        public const float DamageHalvings = 2f;
        public const float FireHalvings = 2f;
        public const float MeleeDamage = 0.50f;
        public const float MoveSpeed = 1.00f;
        public const float PassiveShare = 0.50f;
        public const float PowerCap = 2.00f;
        /// <summary>The held melee weapon: extra damage and armour penetration at full strength (k = 1).</summary>
        public const float WeaponDamage = 0.25f;
        public const float WeaponArmourPen = 0.15f;

        private bool raised;
        public bool Raised
        {
            get { return raised; }
            set { if (raised != value) { raised = value; dirty = true; } }
        }

        private HediffStage stage;
        private bool dirty = true;
        private bool building;
        private float builtFor = -1f;

        // Reading a stat asks every hediff for its CurStage, this one included; while building, that inner
        // call gets the old stage instead of starting another build (Sukuna's endless-loop freeze, 2026-10-28).
        private static readonly HediffStage Empty = new HediffStage { becomeVisible = true };

        private static StatDef S(string name) => DefDatabase<StatDef>.GetNamedSilentFail(name);

        /// <summary>Strength now: power (capped) times the passive share unless raised.</summary>
        public float K
        {
            get
            {
                float p = Mathf.Round(Mathf.Clamp(Jujutsu.Power(pawn), 0f, PowerCap) * 100f) / 100f;
                return p * (raised ? 1f : PassiveShare) * Jujutsu.Product(Jujutsu.ReinforcementFactors, pawn);
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

        public override string LabelInBrackets => raised ? "raised output" : null;

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
            float p = Mathf.Round(Mathf.Clamp(Jujutsu.Power(pawn), 0f, PowerCap) * 100f) / 100f;
            float f = Jujutsu.Product(Jujutsu.ReinforcementFactors, pawn);
            if (!dirty && stage != null && Mathf.Approximately(p * f, builtFor)) return;
            dirty = false;

            float k = p * (raised ? 1f : PassiveShare) * f;
            var offsets = new List<StatModifier>();
            Add(offsets, "ArmorRating_Sharp", SharpArmour * k);
            Add(offsets, "ArmorRating_Blunt", BluntArmour * k);
            Add(offsets, "ArmorRating_Heat", HeatArmour * k);
            Add(offsets, "MoveSpeed", MoveSpeed * k);
            var factors = new List<StatModifier>();
            Add(factors, "IncomingDamageFactor", Mathf.Pow(0.5f, DamageHalvings * k));
            Add(factors, "MeleeDamageFactor", 1f + MeleeDamage * k);
            Add(factors, "Flammability", Mathf.Pow(0.5f, FireHalvings * k));
            foreach (var extra in Jujutsu.ReinforcementExtras) extra(pawn, k, offsets, factors);

            stage = new HediffStage { statOffsets = offsets, statFactors = factors, becomeVisible = true };
            bool first = builtFor < 0f;
            builtFor = p * f;
            if (!first) pawn.health?.Notify_HediffChanged(this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref raised, "jujutsuRaised", false);
        }

        public static Hediff_Reinforcement Of(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(JJKDefOf.JJK_Reinforcement) as Hediff_Reinforcement;

        /// <summary>The held melee weapon's share: hits made with it deal more and go through armour more.</summary>
        public static void CoatWeapon(Pawn attacker, ref DamageInfo dinfo)
        {
            if (dinfo.Weapon == null || !dinfo.Weapon.IsMeleeWeapon || !BlackFlashRules.InMelee) return;
            var r = Of(attacker);
            if (r == null) return;
            float k = r.K;
            dinfo.SetAmount(dinfo.Amount * (1f + WeaponDamage * k));
            try
            {
                ref float ap = ref AccessTools.StructFieldRefAccess<DamageInfo, float>(ref dinfo, "armorPenetrationInt");
                ap = Mathf.Min(ap + WeaponArmourPen * k, 1.9f);
            }
            catch { }
        }
    }
}
