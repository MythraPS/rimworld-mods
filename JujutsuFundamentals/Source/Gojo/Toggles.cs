using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbility = VEF.Abilities.Ability_Blank;

namespace GojoPsycasts
{
    /// <summary>
    /// Reverse Cursed Technique and "Raise cursed energy output" work the same way as
    /// Sukuna's (her call 2026-10-31: both characters have these, keep them
    /// consistent). Off by default, one click either way, psyfocus paid every second
    /// while up, lapsing when the pool is empty or heat is maxed and coming back on
    /// their own. Upkeep is Sukuna's, halved by the Six Eyes through
    /// VPE_PsyfocusCostFactor.
    /// </summary>
    public class GojoToggles : GameComponent
    {
        public const string RCT = "rct", Output = "output", Fly = "fly";
        private static Dictionary<string, HashSet<int>> on = new Dictionary<string, HashSet<int>>();

        public GojoToggles(Game game) { on = new Dictionary<string, HashSet<int>>(); }

        private static HashSet<int> Ids(string key)
        {
            if (!on.TryGetValue(key, out var s)) { s = new HashSet<int>(); on[key] = s; }
            return s;
        }

        // Raise output and reverse cursed technique are the shared techniques' toggles now (2026-11-10)
        public static bool IsOn(Pawn p, string key) =>
            p != null && (key == Output ? JujutsuFundamentals.Jujutsu.Raised(p)
                        : key == RCT ? JujutsuFundamentals.Toggles.IsOn(p, "rct")
                        : Ids(key).Contains(p.thingIDNumber));
        public static bool AnyOn(string key) => on.TryGetValue(key, out var s) && s.Count > 0;

        private static float Drain(string key) => key == RCT ? 0.0012f : key == Fly ? 0.0008f : 0.0040f;   // per second; RCT and output are Sukuna's

        public static void Set(Pawn p, string key, string label, bool now)
        {
            if (p == null) return;
            var s = Ids(key);
            if (now) { if (!s.Add(p.thingIDNumber)) return; Pay(p, key); }
            else { if (!s.Remove(p.thingIDNumber)) return; Lapse(p, key); }
            if (p.Faction == Faction.OfPlayer)
                Messages.Message(label + (now ? " on." : " off."), p, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Lapse(Pawn p, string key)
        {
            if (key == RCT)
            {
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_ReverseCursedTechnique);
                if (h != null) p.health.RemoveHediff(h);
            }
            else if (key == Output && p.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_Reinforcement) is Hediff_GojoReinforcement r)
                r.Boosted = false;   // the passive half stays
        }

        private static void Pay(Pawn p, string key)
        {
            if (key != Fly) return;   // only Fly is Gojo's own now
            var tr = p.psychicEntropy;
            float drain = Drain(key) * p.GetStatValue(Gojo.CostFactorStat);
            if (tr == null || tr.CurrentPsyfocus <= drain || tr.EntropyValue >= tr.MaxEntropy) { Lapse(p, key); return; }
            if (key == Fly) tr.OffsetPsyfocusDirectly(-drain);   // GojoFlight reads psyfocus itself
            else if (key == RCT)
            {
                tr.OffsetPsyfocusDirectly(-drain);
                if (!p.health.hediffSet.HasHediff(GojoDefOf.Gojo_ReverseCursedTechnique))
                    p.health.AddHediff(GojoDefOf.Gojo_ReverseCursedTechnique);
            }
            else if (p.health.hediffSet.GetFirstHediffOfDef(GojoDefOf.Gojo_Reinforcement) is Hediff_GojoReinforcement r)
            {
                tr.OffsetPsyfocusDirectly(-drain);
                r.Boosted = true;
            }
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var key in new[] { RCT, Output, Fly })
            {
                var s = Ids(key);
                if (s.Count == 0) continue;
                foreach (var p in Gojo.ActivePawns())
                    if (s.Contains(p.thingIDNumber) && !p.Dead) Pay(p, key);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var key in new[] { RCT, Output, Fly })
            {
                var l = new List<int>(Ids(key));
                Scribe_Collections.Look(ref l, "gojoToggle_" + key, LookMode.Value);
                if (Scribe.mode == LoadSaveMode.LoadingVars) on[key] = l == null ? new HashSet<int>() : new HashSet<int>(l);
            }
        }
    }

    /// <summary>A checkbox gizmo instead of a cast button, as in the Sukuna mod.</summary>
    public abstract class Ability_GojoToggle : VEFAbility
    {
        protected abstract string Key { get; }
        protected abstract string Title { get; }

        public override Gizmo GetGizmo()
        {
            var normal = base.GetGizmo();
            return new Command_Toggle
            {
                defaultLabel = Title,
                defaultDesc = def.description,
                icon = def.icon,
                isActive = () => GojoToggles.IsOn(pawn, Key),
                toggleAction = () => GojoToggles.Set(pawn, Key, Title, !GojoToggles.IsOn(pawn, Key)),
                activateSound = SoundDefOf.Tick_Tiny,
                Order = normal?.Order ?? 0f,
            };
        }

        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    public class Ability_ToggleRCT : Ability_GojoToggle
    {
        protected override string Key => GojoToggles.RCT;
        protected override string Title => "Reverse cursed technique";
    }

    public class Ability_ToggleFly : Ability_GojoToggle
    {
        protected override string Key => GojoToggles.Fly;
        protected override string Title => "Fly";
    }

    public class Ability_ToggleOutput : Ability_GojoToggle
    {
        protected override string Key => GojoToggles.Output;
        protected override string Title => "Raise cursed energy output";
    }

    /// <summary>
    /// Cursed Energy Reinforcement, Sukuna's numbers (Hediff_CursedEnergyReinforcement
    /// in that mod) on Gojo's power, psychic sensitivity. Always on at half once the
    /// node is learned; the output toggle sets Boosted for the full amount. No
    /// tempered stage: that comes from Sukuna's Furnace.
    /// </summary>
    public class Hediff_GojoReinforcement : HediffWithComps
    {
        public const float SharpArmour = 0.60f, BluntArmour = 0.60f, MeleeDamage = 0.50f, MoveSpeed = 1.00f;
        public const float PassiveShare = 0.50f, PowerCap = 2.00f;

        private bool boosted, dirty = true, building;
        private HediffStage stage;
        private float builtFor = -1f;
        private static readonly HediffStage Empty = new HediffStage { becomeVisible = true };

        public bool Boosted
        {
            get => boosted;
            set { if (boosted != value) { boosted = value; dirty = true; } }
        }

        public override HediffStage CurStage
        {
            get
            {
                if (dirty && !building) Rebuild();   // the guard stops the stat read looping back here
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
            var d = DefDatabase<StatDef>.GetNamedSilentFail(stat);
            if (d != null && v != 0f) l.Add(new StatModifier { stat = d, value = v });
        }

        private void Rebuild()
        {
            if (building) return;
            building = true;
            try
            {
                float p = Mathf.Round(Mathf.Clamp(Gojo.Power(pawn), 0f, PowerCap) * 100f) / 100f;
                if (!dirty && stage != null && Mathf.Approximately(p, builtFor)) return;
                dirty = false;
                float k = p * (boosted ? 1f : PassiveShare);
                var offsets = new List<StatModifier>();
                Add(offsets, "ArmorRating_Sharp", SharpArmour * k);
                Add(offsets, "ArmorRating_Blunt", BluntArmour * k);
                Add(offsets, "MoveSpeed", MoveSpeed * k);
                var factors = new List<StatModifier>();
                Add(factors, "MeleeDamageFactor", 1f + MeleeDamage * k);
                stage = new HediffStage { statOffsets = offsets, statFactors = factors, becomeVisible = true };
                bool first = builtFor < 0f;
                builtFor = p;
                if (!first) pawn.health?.Notify_HediffChanged(this);
            }
            finally { building = false; }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref boosted, "gojoBoosted", false);
        }
    }
}
