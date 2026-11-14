using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>One sustained technique: off by default, switched by a checkbox, and it eats psyfocus
    /// every second while up. It lapses when the pool empties or neural heat maxes, and comes back on
    /// its own: the toggle stays armed until it is switched off.</summary>
    public class ToggleSpec
    {
        public string key;
        public string title;
        public Func<HediffDef> hediff;
        public Func<VEFAbilityDef> ability;
        /// <summary>Psyfocus per second while up.</summary>
        public float drain;
        /// <summary>The hediff is a passive that stays; the toggle only raises it (Reinforcement).</summary>
        public bool raisesPassive;
        /// <summary>Why it can't come up right now, or null.</summary>
        public Func<Pawn, string> blocked;
        /// <summary>Called when it is switched on.</summary>
        public Action<Pawn> onOn;
        /// <summary>A per-pawn multiplier on the drain (Uro's flight), or null.</summary>
        public Func<Pawn, float> drainFactor;
    }

    public class Toggles : GameComponent
    {
        public static readonly Dictionary<string, ToggleSpec> Specs = new Dictionary<string, ToggleSpec>();
        private static Dictionary<string, HashSet<int>> on = new Dictionary<string, HashSet<int>>();

        public Toggles(Game game) { on = new Dictionary<string, HashSet<int>>(); }

        static Toggles()
        {
            Add(new ToggleSpec { key = "reinforcement", title = "Raise cursed energy output", drain = 0.0040f, raisesPassive = true,
                hediff = () => JJKDefOf.JJK_Reinforcement, ability = () => JJKAbilityDefOf.JJK_Reinforcement });
            Add(new ToggleSpec { key = "rct", title = "Reverse cursed technique", drain = 0.0012f,
                hediff = () => JJKDefOf.JJK_ReverseCursedTechnique, ability = () => JJKAbilityDefOf.JJK_ReverseCursedTechnique });
            Add(new ToggleSpec { key = "amplification", title = "Domain amplification", drain = 0.0030f,
                hediff = () => JJKDefOf.JJK_DomainAmplification, ability = () => JJKAbilityDefOf.JJK_DomainAmplification });
            Add(new ToggleSpec { key = "basket", title = "Hollow wicker basket", drain = 0.0010f,
                hediff = () => JJKDefOf.JJK_HollowWickerBasket, ability = () => JJKAbilityDefOf.JJK_HollowWickerBasket,
                blocked = StandStill.Blocked, onOn = StandStill.Begin });
            Add(new ToggleSpec { key = "simpledomain", title = "Simple domain", drain = 0.0015f,
                hediff = () => JJKDefOf.JJK_SimpleDomain, ability = () => JJKAbilityDefOf.JJK_SimpleDomain,
                blocked = StandStill.Blocked,
                onOn = p => { Set(p, "basket", false, quiet: true); StandStill.Begin(p); } });
            Add(new ToggleSpec { key = "hazymoon", title = "Hazy moon", drain = 0.0020f,
                hediff = () => JJKDefOf.JJK_HazyMoon, ability = () => JJKAbilityDefOf.JJK_HazyMoon });
        }

        public static void Add(ToggleSpec s) => Specs[s.key] = s;

        private static HashSet<int> SetOf(string key)
        {
            if (!on.TryGetValue(key, out var s)) { s = new HashSet<int>(); on[key] = s; }
            return s;
        }

        public static bool IsOn(Pawn p, string key) => p != null && SetOf(key).Contains(p.thingIDNumber);

        public static void Set(Pawn p, string key, bool now, bool quiet = false)
        {
            if (p == null || !Specs.TryGetValue(key, out var spec)) return;
            var s = SetOf(key);
            if (now)
            {
                string why = spec.blocked?.Invoke(p);
                if (why != null)
                {
                    if (!quiet && p.Faction == Faction.OfPlayer) Messages.Message(why, p, MessageTypeDefOf.RejectInput, false);
                    return;
                }
                if (!s.Add(p.thingIDNumber)) return;
                spec.onOn?.Invoke(p);
                var tr = p.psychicEntropy;
                if (tr != null && tr.CurrentPsyfocus <= 0.01f && p.Faction == Faction.OfPlayer)
                    Messages.Message(("{0} has no psyfocus. It will come up as soon as there is any.").Formatted(p.LabelShortCap),
                                     p, MessageTypeDefOf.CautionInput, false);
                else Pay(p, spec);   // up on the same frame it was clicked
            }
            else
            {
                if (!s.Remove(p.thingIDNumber)) return;
                Lapse(p, spec);
            }
            if (!quiet && p.Faction == Faction.OfPlayer)
                Messages.Message(spec.title + (now ? " on." : " off."), p, MessageTypeDefOf.NeutralEvent, false);
        }

        private static void Lapse(Pawn p, ToggleSpec spec)
        {
            var h = p.health?.hediffSet?.GetFirstHediffOfDef(spec.hediff());
            if (h is Hediff_Reinforcement r) r.Raised = false;   // the passive stays
            else if (h != null) p.health.RemoveHediff(h);
        }

        /// <summary>Charge one second and make sure the hediff is there. False if the pawn couldn't pay:
        /// the buff lapses but the toggle stays armed.</summary>
        private static bool Pay(Pawn pawn, ToggleSpec spec)
        {
            var tr = pawn.psychicEntropy;
            var def = spec.hediff();
            float drain = spec.drain * CostFactor(pawn) * (spec.drainFactor?.Invoke(pawn) ?? 1f);
            var h = pawn.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (tr == null || tr.CurrentPsyfocus <= drain || spec.blocked?.Invoke(pawn) != null)   // neural heat doesn't stop them (her call 2026-11-10)
            {
                Lapse(pawn, spec);
                return false;
            }
            if (spec.raisesPassive && !(h is Hediff_Reinforcement)) return false;   // PassiveSync adds the passive
            tr.OffsetPsyfocusDirectly(-drain);
            if (h == null) h = pawn.health.AddHediff(def);
            if (h is Hediff_Reinforcement r) r.Raised = true;
            return true;
        }

        private static StatDef costStat; private static bool lookedCost;
        /// <summary>VPE's psyfocus cost factor (the Six Eyes halve it, as Gojo's own toggles did).</summary>
        public static float CostFactorOf(Pawn p) => CostFactor(p);

        private static float CostFactor(Pawn p)
        {
            if (!lookedCost) { lookedCost = true; costStat = DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor"); }
            return costStat == null ? 1f : p.GetStatValue(costStat);
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            Dictionary<int, Pawn> byId = null;
            // copies: paying adds and removes hediffs, and any code that runs from that and reads a toggle (IsOn adds a
            // missing key) or flips one would change these collections mid-loop and throw
            foreach (var kv in new List<KeyValuePair<string, HashSet<int>>>(on))
            {
                if (kv.Value.Count == 0 || !Specs.TryGetValue(kv.Key, out var spec)) continue;
                if (byId == null) byId = Pawns.ById();
                List<int> drop = null;
                foreach (var id in new List<int>(kv.Value))
                {
                    if (!byId.TryGetValue(id, out var pawn)) continue;   // carried or in a pod: stays armed, no upkeep
                    if (pawn.Dead || !Jujutsu.Knows(pawn, spec.ability()))
                    {
                        if (drop == null) drop = new List<int>();
                        drop.Add(id);
                        if (!pawn.Dead) Lapse(pawn, spec);
                        continue;
                    }
                    Pay(pawn, spec);
                }
                if (drop != null) foreach (var id in drop) kv.Value.Remove(id);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var key in Specs.Keys)
            {
                var l = new List<int>(SetOf(key));
                Scribe_Collections.Look(ref l, "jujutsuToggle_" + key, LookMode.Value);
                if (Scribe.mode == LoadSaveMode.LoadingVars) on[key] = l == null ? new HashSet<int>() : new HashSet<int>(l);
            }
        }
    }

    public static class Pawns
    {
        /// <summary>Every pawn on a map or in a caravan, by id.</summary>
        public static Dictionary<int, Pawn> ById()
        {
            var byId = new Dictionary<int, Pawn>();
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned) byId[p.thingIDNumber] = p;
            foreach (var c in Find.WorldObjects.Caravans)
                foreach (var p in c.PawnsListForReading) byId[p.thingIDNumber] = p;
            return byId;
        }
    }

    /// <summary>A sustained technique drawn as a real checkbox: lit when on, one click either way.
    /// VPE passes any gizmo that isn't a Command_Ability straight through.</summary>
    public class Ability_Toggle : Ability
    {
        public string Key => def.GetModExtension<ToggleExtension>()?.key;

        public override bool ShowGizmoOnPawn()
        {
            // Hollow Wicker Basket turns into Simple Domain once that is learned (her call 2026-11-10)
            if (Key == "basket" && Jujutsu.Knows(pawn, JJKAbilityDefOf.JJK_SimpleDomain)) return false;
            return base.ShowGizmoOnPawn();
        }

        public override Gizmo GetGizmo()
        {
            var spec = Toggles.Specs.TryGetValue(Key ?? "", out var s) ? s : null;
            return new Command_Toggle
            {
                defaultLabel = spec?.title ?? def.LabelCap.ToString(),
                defaultDesc = def.description,
                icon = def.icon,
                isActive = () => Toggles.IsOn(pawn, Key),
                toggleAction = () => Toggles.Set(pawn, Key, !Toggles.IsOn(pawn, Key)),
                activateSound = SoundDefOf.Tick_Tiny,
            };
        }

        // the gizmo is a checkbox; a stray autocast or AI path must not cast behind it
        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    public class ToggleExtension : DefModExtension
    {
        public string key;
    }

    /// <summary>Keeps the passive techniques' hediffs in step with what each pawn has learned
    /// (Black Flash, and the half-strength Reinforcement under its toggle). Reads CompAbilities.HasAbility,
    /// so it survives loading and VPE's respec.</summary>
    public class PassiveSync : GameComponent
    {
        public PassiveSync(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 120 != 7) return;
            foreach (var map in Find.Maps)
            {
                var list = map.mapPawns.AllPawnsSpawned;
                for (int i = 0; i < list.Count; i++) Sync(list[i]);
            }
            foreach (var c in Find.WorldObjects.Caravans)
                foreach (var p in c.PawnsListForReading) Sync(p);
        }

        public static void Sync(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || !pawn.RaceProps.Humanlike) return;
            ZeroCursedEnergy.Sync(pawn);
            var comp = pawn.GetComp<CompAbilities>();
            if (comp == null) return;
            Match(pawn, comp, JJKAbilityDefOf.JJK_BlackFlash, JJKDefOf.JJK_BlackFlash);
            Match(pawn, comp, JJKAbilityDefOf.JJK_Reinforcement, JJKDefOf.JJK_Reinforcement);
            PositiveEnergy.Sync(pawn, comp);   // the Positive Energy toggle comes with Reverse Cursed Technique (2026-11-14)
        }


        public static void Match(Pawn pawn, CompAbilities comp, VEFAbilityDef node, HediffDef passive)
        {
            if (node == null || passive == null) return;
            bool unlocked = comp.HasAbility(node);
            var present = pawn.health.hediffSet.GetFirstHediffOfDef(passive);
            if (unlocked && present == null) pawn.health.AddHediff(passive);
            else if (!unlocked && present != null) pawn.health.RemoveHediff(present);
        }
    }
}
