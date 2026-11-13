using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKToolDefOf
    {
        public static ThingDef JJK_InvertedSpear, JJK_PlayfulCloud, JJK_SplitSoulKatana, JJK_ChainThousandMiles,
                               JJK_DragonBone, JJK_NanamiCleaver, JJK_PrisonRealm, JJK_PrisonRealmTrap;
        static JJKToolDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKToolDefOf));
    }

    /// <summary>
    /// What the cursed tools do on a hit (her calls where she made them, 2026-11-10; the rest from canon, numbers
    /// are proposals). Called from Patch_PreApplyDamage, before Black Flash, so a Black Flash stacks on top.
    /// </summary>
    public static class CursedTools
    {
        public const float RatioChance = 0.3f, RatioDamage = 1.5f;
        public const float DragonBoneShare = 0.5f, DragonBoneCap = 60f;

        private static void SetAP(ref DamageInfo d, float ap)
        {
            try { ref float a = ref AccessTools.StructFieldRefAccess<DamageInfo, float>(ref d, "armorPenetrationInt"); a = ap; } catch { }
        }

        private static void Say(Thing t, string text, Color c)
        {
            if (t?.Spawned == true) MoteMaker.ThrowText(t.DrawPos + new Vector3(0f, 0f, 0.5f), t.Map, text, c, 1.8f);
        }

        /// <summary>Cursed tools hurt Anomaly entities and mutants more (her call 2026-11-12), the Black Rope and the
        /// Inverted Spear of Heaven most. Numbers are proposals.</summary>
        public const float VsAnomaly = 1.5f, VsAnomalyNullifier = 2.5f;

        private static ThingCategoryDef toolCat;
        public static bool IsCursedTool(ThingDef d) =>
            d?.thingCategories != null && d.thingCategories.Contains(toolCat ?? (toolCat = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("JJK_CursedTools")));

        /// <summary>The attacker's tool, on a hit it makes.</summary>
        public static void OnHit(Pawn attacker, Pawn victim, ref DamageInfo dinfo)
        {
            var w = dinfo.Weapon;
            // against Anomalies (her calls 2026-11-12): cursed tools x1.5, the Black Rope and Inverted Spear x2.5, and any
            // melee hit with Domain Amplification on x2.5. The best one applies; they don't stack.
            if (BlackFlashRules.InMelee && Patch_RctVsAnomalies.IsAnomaly(victim))
            {
                float f = 1f;
                if (w != null && IsCursedTool(w)) f = w == JJKToolDefOf2.JJK_BlackRope || w == JJKToolDefOf.JJK_InvertedSpear ? VsAnomalyNullifier : VsAnomaly;
                if (Jujutsu.Has(attacker, JJKDefOf.JJK_DomainAmplification)) f = Mathf.Max(f, VsAnomalyNullifier);
                if (f > 1f) dinfo.SetAmount(dinfo.Amount * f);
            }
            if (w == null || !BlackFlashRules.InMelee) return;
            if (w == JJKToolDefOf2.JJK_BlackRope)
            {
                // disrupts whatever technique it touches, and wears away doing it (her call 2026-11-11)
                if (AmplificationStrip.Strip(victim, attacker))
                    attacker.equipment?.Primary?.TryGetComp<CompBlackRope>()?.Used(attacker);
            }
            else if (w == JJKToolDefOf.JJK_InvertedSpear)
            {
                // nullifies whatever technique it touches: psycast buffs, shields, Infinity, raised Reinforcement
                AmplificationStrip.Strip(victim, attacker);
            }
            else if (w == JJKToolDefOf.JJK_SplitSoulKatana)
            {
                SetAP(ref dinfo, 2f);   // cuts the soul directly: no armour counts
            }
            else if (w == JJKToolDefOf.JJK_NanamiCleaver && Rand.Chance(RatioChance))
            {
                // Ratio (her call): the 7:3 point. More damage and no armour; Black Flash can still land on top
                dinfo.SetAmount(dinfo.Amount * RatioDamage);
                SetAP(ref dinfo, 2f);
                Say(victim, "Ratio", new Color(1f, 0.85f, 0.4f));
            }
            else if (w == JJKToolDefOf.JJK_PlayfulCloud)
            {
                // no cursed energy of its own: its force is the wielder's body, counted a second time
                dinfo.SetAmount(dinfo.Amount * Mathf.Max(1f, attacker.GetStatValue(StatDefOf.MeleeDamageFactor)));
            }
            else if (w == JJKToolDefOf.JJK_DragonBone)
            {
                var comp = attacker.equipment?.Primary?.TryGetComp<CompDragonBone>();
                if (comp != null && comp.stored > 0f)
                {
                    dinfo.SetAmount(dinfo.Amount + comp.stored);
                    Say(victim, "Released " + Mathf.RoundToInt(comp.stored), new Color(1f, 0.6f, 0.3f));
                    comp.stored = 0f;
                }
            }
        }

        /// <summary>The victim's Dragon-Bone stores part of every hit taken.</summary>
        public static void OnTaken(Pawn victim, DamageInfo dinfo)
        {
            if (victim?.equipment?.Primary?.def != JJKToolDefOf.JJK_DragonBone || dinfo.Instigator == victim) return;
            var comp = victim.equipment.Primary.TryGetComp<CompDragonBone>();
            if (comp != null) comp.stored = Mathf.Min(DragonBoneCap, comp.stored + dinfo.Amount * DragonBoneShare);
        }
    }

    public class CompProperties_DragonBone : CompProperties
    {
        public CompProperties_DragonBone() => compClass = typeof(CompDragonBone);
    }

    public class CompDragonBone : ThingComp
    {
        public float stored;
        public override string CompInspectStringExtra() => stored > 0f ? "Stored force: " + Mathf.RoundToInt(stored) : null;
        public override void PostExposeData() { base.PostExposeData(); Scribe_Values.Look(ref stored, "jjkStored", 0f); }
    }

    // ==================================================================== Gizmos from the held weapon

    /// <summary>A weapon comp with buttons of its own (the chain's lash, Kamutoke's lightning). The game only asks
    /// apparel for buttons, so held weapons are asked here.</summary>
    public interface IWeaponGizmos { IEnumerable<Gizmo> Gizmos(Pawn holder); }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_WeaponGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (var g in values) yield return g;
            var w = __instance?.equipment?.Primary;
            if (w == null || __instance.Faction != Faction.OfPlayer || __instance.Dead || !__instance.Spawned) yield break;
            foreach (var c in w.AllComps)
                if (c is IWeaponGizmos wg)
                    foreach (var g in wg.Gizmos(__instance)) yield return g;
        }
    }

    // ==================================================================== Chain of a Thousand Miles

    public class CompProperties_ChainLash : CompProperties
    {
        public float range = 19.9f;
        public int cooldownTicks = 300;
        public CompProperties_ChainLash() => compClass = typeof(CompChainLash);
    }

    /// <summary>Lash a target in sight, farther off than melee reaches, with the chain's own melee blow.</summary>
    public class CompChainLash : ThingComp, IWeaponGizmos
    {
        private int readyAt;
        public CompProperties_ChainLash Props => (CompProperties_ChainLash)props;

        public IEnumerable<Gizmo> Gizmos(Pawn holder)
        {
            var cmd = new Command_Action
            {
                defaultLabel = "Lash",
                defaultDesc = $"Swing the chain at a target in sight, up to {Props.range:0} tiles away, with a melee blow.",
                icon = parent.def.uiIcon,
                action = () => Find.Targeter.BeginTargeting(new TargetingParameters { canTargetPawns = true, canTargetBuildings = true, canTargetLocations = false },
                    t => Lash(holder, t), null, t => Valid(holder, t)),
            };
            if (Find.TickManager.TicksGame < readyAt) cmd.Disable("Recharging.");
            else if (holder.Downed || !holder.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) cmd.Disable("Can't swing it.");
            yield return cmd;
        }

        private bool Valid(Pawn p, LocalTargetInfo t) =>
            t.Thing != null && t.Thing != p && t.Thing.Map == p.Map && t.Cell.InHorDistOf(p.Position, Props.range)
            && GenSight.LineOfSight(p.Position, t.Cell, p.Map);

        private void Lash(Pawn p, LocalTargetInfo t)
        {
            if (!Valid(p, t)) { Messages.Message("Out of reach or out of sight.", MessageTypeDefOf.RejectInput, false); return; }
            var tool = parent.def.tools?.MaxBy(x => x.power);
            float dmg = (tool?.power ?? 16f) * p.GetStatValue(StatDefOf.MeleeDamageFactor);
            var dinfo = new DamageInfo(DamageDefOf.Blunt, dmg, tool?.armorPenetration ?? 0f, p.Position.ToVector3Shifted().AngleToFlat(t.Cell.ToVector3Shifted()), p, null, parent.def);
            t.Thing.TakeDamage(dinfo);
            var line = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastPsychicLine");
            if (line != null) FleckMaker.ConnectingLine(p.DrawPos, t.Thing.DrawPos, line, p.Map, 0.5f);
            readyAt = Find.TickManager.TicksGame + Props.cooldownTicks;
            p.stances?.SetStance(new Stance_Cooldown(60, t, null));
        }

        public override void PostExposeData() { base.PostExposeData(); Scribe_Values.Look(ref readyAt, "jjkReadyAt", 0); }
    }

    // ==================================================================== Prison Realm

    /// <summary>
    /// The Prison Realm (her calls): used like a psychic lance, or planted like a mine; after a short wind-up it seals one
    /// person inside and nullifies their abilities (they are out of play until released). Only one in a game, holding
    /// one person.
    /// </summary>
    public class Apparel_PrisonRealm : Apparel, IThingHolder
    {
        private static Thing instance; private static Game instanceGame;
        private ThingOwner<Thing> inner;

        public Apparel_PrisonRealm() { inner = new ThingOwner<Thing>(this, true, LookMode.Deep); }

        public static bool Exists => instance != null && instanceGame == Current.Game && !instance.Destroyed;
        /// <summary>The one Prison Realm, wherever it is (PrisonRealmQuest takes it to seal Gojo).</summary>
        public static Apparel_PrisonRealm Instance => Exists ? instance as Apparel_PrisonRealm : null;
        private void Register() { instance = this; instanceGame = Current.Game; }

        public Pawn Prisoner => inner.Count > 0 ? inner[0] as Pawn : null;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        public ThingOwner GetDirectlyHeldThings() => inner;

        public override void PostMake() { base.PostMake(); Register(); }

        public static bool CanSeal(Pawn p) => p != null && !p.Dead && p.RaceProps.Humanlike;

        public bool Seal(Pawn p)
        {
            if (!CanSeal(p)) return false;
            if (Prisoner != null) { Messages.Message("The Prison Realm already holds someone.", this, MessageTypeDefOf.RejectInput, false); return false; }
            var map = p.MapHeld; var pos = p.PositionHeld;
            if (p.Spawned) p.DeSpawn();
            // a caravan member is a world pawn: the world would save him too, a second copy next to the realm's
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            if (!inner.TryAddOrTransfer(p)) return false;
            if (map != null) FleckMaker.Static(pos, map, FleckDefOf.PsycastAreaEffect, 2f);
            Messages.Message($"{p.LabelShortCap} is sealed in the Prison Realm.", new TargetInfo(pos, map), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        public void Release()
        {
            var p = Prisoner;
            var map = MapHeld; var pos = PositionHeld;
            if (p == null || map == null) return;
            inner.TryDrop(p, pos, map, ThingPlaceMode.Near, out _);
            Messages.Message($"{p.LabelShortCap} is released from the Prison Realm.", p, MessageTypeDefOf.NeutralEvent, false);
        }

        private Command_Action ReleaseCommand()
        {
            var cmd = new Command_Action
            {
                defaultLabel = "Release " + Prisoner.LabelShort, defaultDesc = "Open the Prison Realm and let out whoever is sealed inside.",
                icon = def.uiIcon, action = Release,
            };
            // the captors' seal on Gojo: only Jacob's Ladder opens it (her design 2026-11-11)
            if (PrisonRealmQuest.GojoLocked(this)) cmd.Disable("Sealed shut. Only Jacob's Ladder can open it.");
            return cmd;
        }

        /// <summary>Jacob's Ladder opens any Prison Realm, the captors' seal on Gojo included.</summary>
        public void OpenByLadder()
        {
            if (Prisoner == null) return;
            PrisonRealmQuest.Instance?.Notify_Opened(this);
            Release();
        }

        public override IEnumerable<Gizmo> GetWornGizmos()
        {
            foreach (var g in base.GetWornGizmos()) yield return g;
            if (Wearer == null || Wearer.Faction != Faction.OfPlayer) yield break;
            if (Prisoner != null) yield return ReleaseCommand();
            if (Wearer.Spawned)
                yield return new Command_Action
                {
                    defaultLabel = "Plant As A Trap", icon = def.uiIcon,
                    defaultDesc = "Set the Prison Realm down here. The first enemy person to come close is sealed inside if they are still near when it finishes opening.",
                    action = () => PrisonRealmTrap.Plant(Wearer, this),
                };
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (Prisoner != null) yield return ReleaseCommand();
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            string mine = Prisoner != null ? "Sealed inside: " + Prisoner.LabelShort : "Empty.";
            return s.NullOrEmpty() ? mine : s + "\n" + mine;
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            var map = MapHeld; var pos = PositionHeld;
            if (Prisoner != null && map != null) inner.TryDropAll(pos, map, ThingPlaceMode.Near);
            base.Destroy(mode);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref inner, "jjkRealm", this);
            if (inner == null) inner = new ThingOwner<Thing>(this, true, LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) Register();
        }
    }

    /// <summary>Aimed at a person in range: sealed after the verb's wind-up.</summary>
    public class Verb_PrisonRealm : Verb
    {
        private Apparel_PrisonRealm Realm => (DirectOwner as CompApparelVerbOwner)?.parent as Apparel_PrisonRealm;

        public override bool Available() => base.Available() && Realm?.Prisoner == null;

        protected override bool TryCastShot() => Realm != null && Realm.Seal(currentTarget.Pawn);

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!Apparel_PrisonRealm.CanSeal(target.Pawn)) { if (showMessages) Messages.Message("The Prison Realm seals people only.", MessageTypeDefOf.RejectInput, false); return false; }
            return base.ValidateTarget(target, showMessages);
        }
    }

    /// <summary>The planted Prison Realm: the realm item is held inside the building.</summary>
    public class PrisonRealmTrap : Building, IThingHolder
    {
        public const int WindUp = 180;
        private ThingOwner<Thing> held;
        private Pawn target; private int windStart = -1;

        public PrisonRealmTrap() { held = new ThingOwner<Thing>(this, true, LookMode.Deep); }

        public Apparel_PrisonRealm Realm => held.Count > 0 ? held[0] as Apparel_PrisonRealm : null;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        public ThingOwner GetDirectlyHeldThings() => held;

        public static void Plant(Pawn wearer, Apparel_PrisonRealm realm)
        {
            var map = wearer.Map; var pos = wearer.Position;
            var trap = (PrisonRealmTrap)ThingMaker.MakeThing(JJKToolDefOf.JJK_PrisonRealmTrap);
            trap.SetFaction(wearer.Faction);
            wearer.apparel.Remove(realm);
            trap.held.TryAdd(realm);
            GenSpawn.Spawn(trap, pos, map, WipeMode.VanishOrMoveAside);
        }

        protected override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (!this.IsHashIntervalTick(10, delta)) return;
            var realm = Realm;
            if (realm == null || realm.Prisoner != null) return;
            if (target != null)
            {
                bool near = target.Spawned && !target.Dead && target.Map == Map && target.Position.InHorDistOf(Position, 2.9f);
                if (!near) { target = null; windStart = -1; return; }
                if (Find.TickManager.TicksGame - windStart >= WindUp) { realm.Seal(target); target = null; windStart = -1; }
                return;
            }
            foreach (var t in GenRadial.RadialDistinctThingsAround(Position, Map, 1.9f, true))
                if (t is Pawn p && Apparel_PrisonRealm.CanSeal(p) && p.HostileTo(Faction ?? Faction.OfPlayer))
                {
                    target = p; windStart = Find.TickManager.TicksGame;
                    MoteMaker.ThrowText(DrawPos, Map, "The Prison Realm opens", new Color(0.8f, 0.6f, 1f), 2f);
                    break;
                }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            var r = Realm;
            if (r?.Prisoner != null)
                yield return new Command_Action { defaultLabel = "Release " + r.Prisoner.LabelShort, icon = def.uiIcon, action = r.Release };
            yield return new Command_Action
            {
                defaultLabel = "Pick Up", icon = def.uiIcon,
                defaultDesc = "Take the Prison Realm back up. Whoever is sealed inside stays sealed.",
                action = PickUp,
            };
        }

        private void PickUp()
        {
            var map = Map; var pos = Position; var r = Realm;
            if (r != null) held.Remove(r);
            Destroy();
            if (r != null) GenPlace.TryPlaceThing(r, pos, map, ThingPlaceMode.Near);
        }

        public override string GetInspectString()
        {
            var r = Realm;
            string s = base.GetInspectString();
            string mine = r?.Prisoner != null ? "Sealed inside: " + r.Prisoner.LabelShort : target != null ? "Sealing " + target.LabelShort + "..." : "Waiting.";
            return s.NullOrEmpty() ? mine : s + "\n" + mine;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref held, "jjkHeld", this);
            Scribe_References.Look(ref target, "jjkTarget");
            Scribe_Values.Look(ref windStart, "jjkWindStart", -1);
            if (held == null) held = new ThingOwner<Thing>(this, true, LookMode.Deep);
        }
    }

    /// <summary>Only one Prison Realm in a game (her call): no new one is generated for loot, rewards or traders
    /// while one exists.</summary>
    [HarmonyPatch(typeof(ThingSetMakerUtility), nameof(ThingSetMakerUtility.CanGenerate))]
    public static class Patch_OnePrisonRealm_Loot
    {
        public static void Postfix(ThingDef thingDef, ref bool __result)
        {
            if (__result && thingDef == JJKToolDefOf.JJK_PrisonRealm && Apparel_PrisonRealm.Exists) __result = false;
        }
    }

    [HarmonyPatch(typeof(StockGenerator_Tag), nameof(StockGenerator_Tag.HandlesThingDef))]
    public static class Patch_OnePrisonRealm_Trade
    {
        public static void Postfix(ThingDef thingDef, ref bool __result)
        {
            if (__result && thingDef == JJKToolDefOf.JJK_PrisonRealm && Apparel_PrisonRealm.Exists) __result = false;
        }
    }
}
