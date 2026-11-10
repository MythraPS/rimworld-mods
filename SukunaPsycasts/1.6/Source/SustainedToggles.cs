using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbility = VEF.Abilities.Ability_Blank;

namespace SukunaPsycasts
{
    /// <summary>
    /// Thorns, Cursed Energy output and Reverse Cursed Technique are all sustained toggles:
    /// off by default, flipped instantly, and they eat psyfocus for as long as they
    /// are up. They lapse when the pool empties or neural heat maxes, and come back
    /// on their own — the toggle itself stays armed until it is switched off.
    /// </summary>
    public class SustainedToggles : GameComponent
    {
        private static Dictionary<string, HashSet<int>> on = new Dictionary<string, HashSet<int>>();

        public SustainedToggles(Game game) { on = new Dictionary<string, HashSet<int>>(); }

        private static HashSet<int> Set(string key)
        {
            if (!on.TryGetValue(key, out var s)) { s = new HashSet<int>(); on[key] = s; }
            return s;
        }

        public static bool IsOn(Pawn p, string key) =>
            p != null && Set(key).Contains(p.thingIDNumber);

        /// <summary>Set the toggle to an explicit state. The gizmo is a checkbox, so
        /// it always knows which way it is going — there is no flip-and-hope.</summary>
        public static void Set(Pawn p, string key, HediffDef hediff, string label, bool now)
        {
            if (p == null) return;
            var s = Set(key);
            if (now)
            {
                if (!s.Add(p.thingIDNumber)) return;
                var tr = p.psychicEntropy;
                if (tr != null && tr.CurrentPsyfocus <= 0.01f && p.Faction == Faction.OfPlayer)
                    Messages.Message(("{0} has no psyfocus — it will come up as soon as "
                                     + "there is any.").Formatted(p.LabelShortCap),
                                     p, MessageTypeDefOf.CautionInput, false);
                else if (tr == null || tr.CurrentPsyfocus > 0.01f)
                    Pay(p, key, hediff);   // up on the same frame it was clicked
            }
            else
            {
                if (!s.Remove(p.thingIDNumber)) return;
                var h = p.health?.hediffSet?.GetFirstHediffOfDef(hediff);
                if (h is Hediff_CursedEnergyReinforcement r) r.Boosted = false;   // passive stays
                else if (h != null) p.health.RemoveHediff(h);
            }
            if (p.Faction == Faction.OfPlayer)
                Messages.Message(label + (now ? " on." : " off."), p,
                                 MessageTypeDefOf.NeutralEvent, false);
        }

        private struct Entry { public string key; public HediffDef hediff; public float drain; }

        private static readonly List<Entry> Entries = new List<Entry>();

        private static void EnsureEntries()
        {
            if (Entries.Count > 0) return;
            Entries.Add(new Entry { key = "thorns",   hediff = SukunaDefOf.Sukuna_ThornsShield,          drain = 0.0015f });
            Entries.Add(new Entry { key = "infinity", hediff = SukunaDefOf.Sukuna_PseudoInfinity,        drain = 0.0040f });
            Entries.Add(new Entry { key = "rct",      hediff = SukunaDefOf.Sukuna_ReverseCursedTechnique, drain = 0.0012f });
        }

        private static float DrainFor(string key)
        {
            EnsureEntries();
            foreach (var e in Entries) if (e.key == key) return e.drain;
            return 0f;
        }

        /// <summary>Charge one second of upkeep and make sure the hediff is present.
        /// Returns false if the pawn could not pay, in which case the buff lapses but
        /// the toggle stays armed.</summary>
        private static bool Pay(Pawn pawn, string key, HediffDef hediff)
        {
            var tr = pawn.psychicEntropy;
            float drain = DrainFor(key);
            var h = pawn.health?.hediffSet?.GetFirstHediffOfDef(hediff);
            bool passive = h is Hediff_CursedEnergyReinforcement;
            if (tr == null || tr.CurrentPsyfocus <= drain || tr.EntropyValue >= tr.MaxEntropy)
            {
                // Reinforcement never lapses (it is passive); only the boost does.
                if (h is Hediff_CursedEnergyReinforcement r0) r0.Boosted = false;
                else if (h != null) pawn.health.RemoveHediff(h);
                return false;
            }
            // The boost needs the passive underneath it; the passive sync adds that.
            if (key == "infinity" && !passive) return false;

            tr.OffsetPsyfocusDirectly(-drain);
            if (h == null) h = pawn.health.AddHediff(hediff);
            if (h is Hediff_CursedEnergyReinforcement r) r.Boosted = true;
            return true;
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 60 != 0) return;
            EnsureEntries();
            Dictionary<int, Pawn> byId = null;   // one pass over the maps, only when something is on

            foreach (var e in Entries)
            {
                var s = Set(e.key);
                if (s.Count == 0) continue;
                if (byId == null)
                {
                    byId = new Dictionary<int, Pawn>();
                    var maps = Find.Maps;
                    for (int m = 0; m < maps.Count; m++)
                    {
                        var list = maps[m].mapPawns.AllPawnsSpawned;
                        for (int i = 0; i < list.Count; i++) byId[list[i].thingIDNumber] = list[i];
                    }
                    foreach (var c in Find.WorldObjects.Caravans)
                        foreach (var p in c.PawnsListForReading) byId[p.thingIDNumber] = p;
                }
                List<int> drop = null;

                foreach (var id in s)
                {
                    if (!byId.TryGetValue(id, out var pawn)) continue;   // carried or in a pod: stays armed, no upkeep
                    if (pawn.Dead) { if (drop == null) drop = new List<int>(); drop.Add(id); continue; }
                    Pay(pawn, e.key, e.hediff);
                }
                if (drop != null) foreach (var id in drop) s.Remove(id);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            foreach (var key in new[] { "thorns", "infinity", "rct" })
            {
                var l = new List<int>(Set(key));
                Scribe_Collections.Look(ref l, "sukunaToggle_" + key, LookMode.Value);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                    on[key] = l == null ? new HashSet<int>() : new HashSet<int>(l);
            }
        }
    }

    /// <summary>
    /// A sustained technique, rendered as an actual checkbox rather than a cast
    /// button. VEF's Command_Ability is a targeting command — it fires an ability and
    /// forgets — so no amount of "toggle" in the description made these behave like
    /// toggles. Ability.GetGizmo is virtual, and VPE's gizmo postfix passes anything
    /// that is not a Command_Ability straight through, so returning a plain
    /// Command_Toggle here gives the real thing: lit when on, unlit when off, one
    /// click either way.
    /// </summary>
    public abstract class Ability_SustainedToggle : VEFAbility
    {
        protected abstract string   Key      { get; }
        protected abstract HediffDef Buff    { get; }
        protected abstract string   Title    { get; }

        public bool On => SustainedToggles.IsOn(pawn, Key);

        public override Gizmo GetGizmo()
        {
            var toggle = new Command_Toggle
            {
                defaultLabel = Title,
                defaultDesc  = def.description,
                icon         = def.icon,
                isActive     = () => On,
                toggleAction = () => SustainedToggles.Set(pawn, Key, Buff, Title, !On),
                activateSound = SoundDefOf.Tick_Tiny,
            };
            // Deliberately never disabled. A sustained technique costs nothing to
            // switch off, and a gizmo that greys out while the buff is up would trap
            // the pawn in it. Affordability is the upkeep loop's problem, not the
            // button's.
            return SukunaGizmoOrder.Apply(toggle, def);
        }

        // Never fires — the gizmo is a checkbox, not a cast button — but a stray
        // autocast or AI path must not sneak a cast in behind the toggle.
        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    public class Ability_ToggleThorns : Ability_SustainedToggle
    {
        protected override string    Key   => "thorns";
        protected override HediffDef Buff  => SukunaDefOf.Sukuna_ThornsShield;
        protected override string    Title => "Thorns";
    }

    public class Ability_TogglePseudoInfinity : Ability_SustainedToggle
    {
        protected override string    Key   => "infinity";
        protected override HediffDef Buff  => SukunaDefOf.Sukuna_PseudoInfinity;
        protected override string    Title => "Raise cursed energy output";
    }

    public class Ability_ToggleReverseCursedTechnique : Ability_SustainedToggle
    {
        protected override string    Key   => "rct";
        protected override HediffDef Buff  => SukunaDefOf.Sukuna_ReverseCursedTechnique;
        protected override string    Title => "Reverse cursed technique";
    }
}
