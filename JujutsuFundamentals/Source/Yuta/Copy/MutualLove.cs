using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>Domain Expansion: Authentic Mutual Love (her design 2026-11-11). His one domain. It costs what Ryu's
    /// does: half a psyfocus bar and neural heat to 90% of his limit (proposal, same as Ryu).</summary>
    public class Ability_MutualLove : Ability
    {
        public const float Psyfocus = 0.5f;
        private static StatDef costStat;

        /// <summary>Half a bar times his psyfocus cost factor, like Ryu's (her call 2026-11-11: his reserve is huge).</summary>
        public static float Cost(Pawn p)
        {
            costStat = costStat ?? DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor");
            return Psyfocus * (costStat == null ? 1f : Mathf.Clamp(p.GetStatValue(costStat), 0.1f, 1f));
        }

        /// <summary>What opening it costs now: half inside a barrier Dhruv's shikigami traced (her call 2026-11-11).</summary>
        public static float CostNow(Pawn p) => Cost(p) * (DhruvBarrier.Ready(p) ? DhruvBarrier.CostFactor : 1f);

        public override bool IsEnabledForPawn(out string reason)
        {
            if ((pawn.psychicEntropy?.CurrentPsyfocus ?? 0f) < CostNow(pawn)) { reason = $"Needs {CostNow(pawn):P0} psyfocus."; return false; }
            if (Jujutsu.Has(pawn, YutaCopyDefOf.JJK_MutualLoveDomain)) { reason = "His domain is already open."; return false; }
            if (MutualLove.Compatible(pawn).Count == 0) { reason = "He has no technique to put in the katanas."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            bool traced = DhruvBarrier.Ready(pawn);
            pawn.psychicEntropy?.OffsetPsyfocusDirectly(-CostNow(pawn));
            Domains.Strain(pawn, traced ? DhruvBarrier.StrainFactor : 1f);
            pawn.health.AddHediff(YutaCopyDefOf.JJK_MutualLoveDomain);
        }
    }

    public class HediffCompProperties_MutualLove : HediffCompProperties
    {
        public float radius = 11.9f;
        public float maxRadius = 30f;
        public HediffCompProperties_MutualLove() => compClass = typeof(HediffComp_MutualLove);
    }

    /// <summary>
    /// The domain: a barrier like Ryu's, with katanas stuck in the ground all over it. Each holds one of Yuta's
    /// techniques, unknown until he touches it (equips it). Then that technique hits every enemy in the domain as a
    /// sure hit, ignoring armour and shields. It collapses if he leaves it, dies, or loses a clash; the katanas go with it.
    /// </summary>
    public class HediffComp_MutualLove : HediffComp
    {
        public IntVec3 anchor = IntVec3.Invalid;
        public float radius;
        private List<Thing> barrier = new List<Thing>();
        private List<Thing> katanas = new List<Thing>();
        private DomainInfo info;

        public HediffCompProperties_MutualLove Props => (HediffCompProperties_MutualLove)props;

        public static HediffComp_MutualLove Of(Pawn p) =>
            p?.health?.hediffSet?.GetFirstHediffOfDef(YutaCopyDefOf.JJK_MutualLoveDomain)?.TryGetComp<HediffComp_MutualLove>();

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            var pawn = Pawn; var map = pawn?.Map;
            if (map == null) return;
            anchor = pawn.Position;
            radius = Mathf.Min(Props.radius * Jujutsu.Sensitivity(pawn) * Jujutsu.Boost(pawn), Props.maxRadius);
            // on a barrier Dhruv's shikigami traced: the domain is that circle, its centre and its size (her call
            // 2026-11-11: the circle they form is used as the domain), and uses it up
            var traced = DhruvBarrier.Get(pawn);
            if (traced != null && DhruvBarrier.Ready(pawn))
            {
                if (traced.center.InBounds(map)) anchor = traced.center;
                radius = Mathf.Clamp(traced.radius, 4f, Props.maxRadius);
                DhruvBarrier.Use(pawn);
            }
            barrier = Barrier.Raise(pawn, anchor, radius, map);
            Barrier.Ground("JJK_GroundMutualLove", anchor, radius, map, barrier);   // crosses, swords, knotted ropes
            Register();
            if (pawn.health.hediffSet.hediffs.Contains(parent)) Scatter(pawn, map);
        }

        private void Register(bool rejoin = false)
        {
            info = Domains.Make(parent, anchor, radius, "Authentic Mutual Love");
            if (rejoin) Domains.Rejoin(info); else Domains.Open(info);
        }

        private void Scatter(Pawn pawn, Map map)
        {
            var pool = MutualLove.Compatible(pawn).InRandomOrder().ToList();
            if (pool.Count == 0) return;
            // her calls 2026-11-11: more katanas than techniques, several holding the same one (proposal: twice as
            // many, at least 8), every technique in at least one
            // her call 2026-11-11 (later): more swords, and one always within quick reach. Proposal: four per technique,
            // at least 20; the first few land within 2-4 cells of Yuta so he can take one up at once.
            int n = Mathf.Max(pool.Count * 4, 20);
            const int Near = 4;
            for (int i = 0; i < n; i++)
            {
                IntVec3 c = IntVec3.Invalid;
                var from = i < Near && pawn.Spawned ? pawn.Position : anchor;
                float reach = i < Near ? 4f : Mathf.Max(1.5f, radius - 1f);
                for (int tries = 0; tries < 30; tries++)
                {
                    var x = from + GenRadial.RadialPattern[Rand.Range(i < Near ? GenRadial.NumCellsInRadius(1.5f) : 1, GenRadial.NumCellsInRadius(reach))];
                    if (x.InBounds(map) && x.Standable(map) && x.GetFirstItem(map) == null) { c = x; break; }
                }
                if (!c.IsValid) continue;
                var k = ThingMaker.MakeThing(YutaCopyDefOf.JJK_MutualLoveKatana);
                var comp = k.TryGetComp<CompMutualLoveKatana>();
                if (comp != null) { comp.technique = pool[i % pool.Count]; comp.owner = pawn; }
                GenSpawn.Spawn(k, c, map);
                FleckMaker.ThrowLightningGlow(c.ToVector3Shifted(), map, 1f);
                katanas.Add(k);
            }
        }

        private bool Left
        {
            get
            {
                var pawn = Pawn;
                if (pawn == null) return false;
                // collapsed already (on his death): the hediff goes too, or a resurrected caster is stuck with "already open"
                if (!anchor.IsValid) return true;
                return pawn.Dead || pawn.Downed || !pawn.Spawned || pawn.Position.DistanceTo(anchor) > radius;   // downed too (her call 2026-11-12)
            }
        }

        public override bool CompShouldRemove => base.CompShouldRemove || Left;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (info == null && anchor.IsValid && Pawn?.Map != null) Register(rejoin: true);   // after loading a save
        }

        public override void CompPostPostRemoved() { base.CompPostPostRemoved(); Collapse(); }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn.IsHashIntervalTick(250)) MutualLove.SweepStrays();
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null) { base.Notify_PawnDied(dinfo, culprit); Collapse(); }

        /// <summary>His own sword, put away (not dropped) while he holds a domain katana (her call 2026-11-11).</summary>
        public ThingWithComps stashed;

        private void Collapse()
        {
            if (info != null) Domains.Close(info);
            info = null;
            Barrier.Drop(barrier);
            foreach (var k in katanas) if (k != null && !k.Destroyed) k.Destroy();
            katanas.Clear();
            anchor = IntVec3.Invalid;
            MutualLove.Unstash(Pawn, this);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref anchor, "mutualLoveAnchor", IntVec3.Invalid);
            Scribe_Values.Look(ref radius, "mutualLoveRadius", 0f);
            Scribe_References.Look(ref stashed, "mutualLoveStashed");
            if (Scribe.mode == LoadSaveMode.Saving) { barrier.RemoveAll(b => b == null || b.Destroyed); katanas.RemoveAll(b => b == null || b.Destroyed); }
            Scribe_Collections.Look(ref barrier, "mutualLoveBarrier", LookMode.Reference);
            Scribe_Collections.Look(ref katanas, "mutualLoveKatanas", LookMode.Reference);
            if (barrier == null) barrier = new List<Thing>();
            if (katanas == null) katanas = new List<Thing>();
        }
    }

    public class CompProperties_MutualLoveKatana : CompProperties
    {
        public CompProperties_MutualLoveKatana() => compClass = typeof(CompMutualLoveKatana);
    }

    /// <summary>A katana from his domain: one hidden technique, revealed and released when Yuta takes it up.</summary>
    public class CompMutualLoveKatana : ThingComp
    {
        public VEFAbilityDef technique;
        public Pawn owner;
        public bool revealed;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (revealed || pawn == null || pawn != owner || technique == null) return;
            revealed = true;
            MutualLove.Release(pawn, technique);
        }

        public override string CompInspectStringExtra() =>
            revealed ? $"Technique: {technique?.LabelCap}" : "Holds one of Yuta's techniques. He won't know which until he takes it up.";

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref technique, "mutualLoveTechnique");
            Scribe_References.Look(ref owner, "mutualLoveOwner");
            Scribe_Values.Look(ref revealed, "mutualLoveRevealed");
        }
    }

    public static class MutualLove
    {
        /// <summary>How long a sure-hit mark lasts after a technique is released: long enough for projectiles to land.</summary>
        public const int MarkTicks = 300;

        private static readonly System.Reflection.FieldInfo Entropy = AccessTools.Field(typeof(Pawn_PsychicEntropyTracker), "currentEntropy");
        private static readonly System.Reflection.MethodInfo BeltBreak = AccessTools.Method(typeof(CompShield), "Break");

        /// <summary>Techniques a katana can hold: his learned ones that take a target (not self-only, not toggles or
        /// passives), and never Copy, Paste, Manifest Rika, healing others, or a domain.</summary>
        public static List<VEFAbilityDef> Compatible(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) return new List<VEFAbilityDef>();
            return comp.LearnedAbilities.Select(a => a.def).Where(Fits).Distinct().ToList();
        }

        /// <summary>Not attacks (her call 2026-11-11: only hostile techniques go in the katanas): movement, swaps,
        /// summons, healing, giving and copying.</summary>
        private static readonly string[] NotHostile = { "Ability_Teleport", "Ability_AirStep", "Ability_Leap", "Ability_BoogieWoogie",
            "Ability_DhruvShikigami", "Ability_Paste", "Ability_Copy", "Ability_RctOutput", "Ability_ManifestRika" };

        private static bool Fits(VEFAbilityDef d)
        {
            if (d == null || d.abilityClass == null) return false;
            if (d.isPositive == true || NotHostile.Any(n => d.abilityClass.Name.StartsWith(n))) return false;
            if (YutaAbilities.IsYutaOnly(d) && d.defName != YutaAbilities.RikaArm && d.defName != YutaAbilities.RikaBite
                && d.defName != YutaAbilities.RikaGrapple && d.defName != YutaAbilities.Die) return false;
            if (d.defName == YutaAbilities.Die) return true;   // her call 2026-11-12: Die goes in the katanas, reaching the whole domain
            // targetMode / targetingParameters are private: VEF copies them into these lists on load
            if (d.targetModes != null && d.targetModes.Any(m => m.ToString() == "Self" || m.ToString() == "Tile")) return false;
            if (d.targetCount > 1) return false;
            if (typeof(Ability_Toggle).IsAssignableFrom(d.abilityClass) || typeof(Ability_Passive).IsAssignableFrom(d.abilityClass)) return false;
            if (d.modExtensions != null && d.modExtensions.Any(e => e is ToggleExtension)) return false;
            var tp = d.targetingParametersList?.FirstOrDefault();
            if (tp != null && !tp.canTargetPawns && !tp.canTargetLocations) return false;
            return true;
        }

        /// <summary>Casts <paramref name="def"/> on every enemy inside his domain, as a sure hit. What it costs him is
        /// given back: the domain pays for it, and the technique isn't left on cooldown (proposal).</summary>
        public static void Release(Pawn yuta, VEFAbilityDef def)
        {
            var dom = HediffComp_MutualLove.Of(yuta);
            var ability = yuta.GetComp<CompAbilities>()?.LearnedAbilities.FirstOrDefault(a => a.def == def);
            if (dom == null || !dom.anchor.IsValid || yuta.Map == null)
            {
                Messages.Message("The katana holds " + def.label + ", but his domain is gone.", yuta, MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            if (ability == null)
            {
                Messages.Message("The katana holds " + def.label + ", which Yuta no longer knows.", yuta, MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Messages.Message($"The katana holds {def.label}. It hits every enemy in Authentic Mutual Love.", yuta, MessageTypeDefOf.PositiveEvent, false);
            MoteMaker.ThrowText(yuta.DrawPos, yuta.Map, def.LabelCap, Yuta.Purple, 3f);

            var map = yuta.Map;
            var enemies = map.mapPawns.AllPawnsSpawned
                .Where(v => v != yuta && !v.Dead && Domains.Hits(yuta, v) && v.Position.DistanceTo(dom.anchor) <= dom.radius && !SureHit.Blocked(v))
                .ToList();
            ReleaseFx(yuta, enemies);
            var tr = yuta.psychicEntropy;
            float focus = tr?.CurrentPsyfocus ?? 0f;
            object heat = (tr != null && Entropy != null) ? Entropy.GetValue(tr) : null;
            int cooldown = ability.cooldown;
            SureHit.Active = true;
            try
            {
                if (ability is Ability_CS_Die)
                {
                    // one command heard across the whole domain
                    foreach (var v in enemies) { StripShields(v, yuta); v.health.AddHediff(YutaCopyDefOf.JJK_MutualLoveMark); }
                    Ability_CS_Die.domainCenter = dom.anchor; Ability_CS_Die.domainRadius = dom.radius;
                    try { ability.Cast(new[] { new GlobalTargetInfo(yuta) }); }
                    catch (System.Exception e) { Log.Warning($"[JJK Yuta] Authentic Mutual Love: {def.defName} failed: {e.Message}"); }
                    finally { Ability_CS_Die.domainCenter = null; }
                }
                else foreach (var v in enemies)
                {
                    StripShields(v, yuta);
                    v.health.AddHediff(YutaCopyDefOf.JJK_MutualLoveMark);
                    try { ability.Cast(new[] { new GlobalTargetInfo(v) }); }
                    catch (System.Exception e) { Log.Warning($"[JJK Yuta] Authentic Mutual Love: {def.defName} on {v.LabelShort} failed: {e.Message}"); }
                }
            }
            finally
            {
                SureHit.Active = false;
                if (tr != null)
                {
                    tr.OffsetPsyfocusDirectly(focus - tr.CurrentPsyfocus);
                    if (heat != null) Entropy.SetValue(tr, heat);
                }
                ability.cooldown = cooldown;
            }
        }

        private static FleckDef line, area;

        /// <summary>What the release looks like (her ask 2026-11-11): a purple burst around Yuta, a line of cursed energy
        /// from him to every enemy it reaches, and a flash on each of them.</summary>
        public static void ReleaseFx(Pawn yuta, List<Pawn> enemies)
        {
            var map = yuta.Map;
            line = line ?? DefDatabase<FleckDef>.GetNamedSilentFail("PsycastPsychicLine");
            area = area ?? DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
            Tinted(yuta.DrawPos, map, area, 6f);
            FleckMaker.ThrowLightningGlow(yuta.DrawPos, map, 3f);
            foreach (var v in enemies)
            {
                if (line != null) FleckMaker.ConnectingLine(yuta.DrawPos, v.DrawPos, line, map, 1.5f);
                Tinted(v.DrawPos, map, area, 2.2f);
                FleckMaker.Static(v.Position, map, FleckDefOf.ExplosionFlash, 3f);
                FleckMaker.ThrowLightningGlow(v.DrawPos, map, 1.6f);
                FleckMaker.ThrowMicroSparks(v.DrawPos, map);
            }
        }

        private static void Tinted(Vector3 at, Map map, FleckDef def, float scale)
        {
            if (def == null || map == null) return;
            var data = FleckMaker.GetDataStatic(at, map, def, scale);
            data.instanceColor = Yuta.Purple;
            map.flecks.CreateFleck(data);
        }

        /// <summary>Back in his hand once the domain is gone.</summary>
        /// <summary>Katanas whose domain is gone (a save, a missed collapse) vanish, from the ground and from anyone's
        /// hands or pack (her call 2026-11-11). Runs while a domain is open and once a minute through MutualLoveSweep.</summary>
        public static void SweepStrays()
        {
            var def = YutaCopyDefOf.JJK_MutualLoveKatana;
            if (def == null) return;
            var stray = new List<Thing>();
            foreach (var map in Find.Maps)
            {
                foreach (var t in map.listerThings.ThingsOfDef(def)) if (Orphan(t)) stray.Add(t);
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (p.equipment?.Primary?.def == def && Orphan(p.equipment.Primary)) stray.Add(p.equipment.Primary);
                    if (p.inventory != null) foreach (var t in p.inventory.innerContainer) if (t.def == def && Orphan(t)) stray.Add(t);
                }
            }
            foreach (var t in stray)
            {
                var holder = (t.ParentHolder as Pawn_EquipmentTracker)?.pawn;
                if (!t.Destroyed) t.Destroy();
                if (holder != null && Yuta.Is(holder)) GiveBackSword(holder);
            }
        }

        private static bool Orphan(Thing k)
        {
            var owner = k.TryGetComp<CompMutualLoveKatana>()?.owner;
            return owner == null || owner.Dead || !Jujutsu.Has(owner, YutaCopyDefOf.JJK_MutualLoveDomain);
        }

        /// <summary>His own sword back from his pack when his hands are empty (a katana vanished from them).</summary>
        private static void GiveBackSword(Pawn yuta)
        {
            if (yuta.equipment == null || yuta.equipment.Primary != null || yuta.inventory == null) return;
            var w = yuta.inventory.innerContainer.FirstOrDefault(t => t.def.IsMeleeWeapon && t.def != YutaCopyDefOf.JJK_MutualLoveKatana) as ThingWithComps;
            if (w == null) return;
            yuta.inventory.innerContainer.Remove(w);
            yuta.equipment.AddEquipment(w);
        }

        public static void Unstash(Pawn yuta, HediffComp_MutualLove dom)
        {
            if (dom == null) return;
            var w = dom.stashed;
            dom.stashed = null;
            if (yuta == null || w == null || w.Destroyed || yuta.Dead || yuta.inventory == null || yuta.equipment == null) return;
            if (!yuta.inventory.innerContainer.Contains(w)) return;
            var held = yuta.equipment.Primary;
            if (held != null && held.TryGetComp<CompMutualLoveKatana>() == null) return;   // he picked up something else; leave it
            if (held != null) held.Destroy();
            yuta.inventory.innerContainer.Remove(w);
            yuta.equipment.AddEquipment(w);
        }

        /// <summary>Psycast shields and buffs (Domain Amplification's strip), and worn shield belts.</summary>
        private static void StripShields(Pawn v, Pawn yuta)
        {
            AmplificationStrip.Strip(v, yuta);
            if (v.apparel == null || BeltBreak == null) return;
            foreach (var a in v.apparel.WornApparel)
            {
                var s = a.TryGetComp<CompShield>();
                if (s != null && s.ShieldState == ShieldState.Active) BeltBreak.Invoke(s, null);
            }
        }
    }

    /// <summary>
    /// Marked by his domain: Yuta's damage on them is a full sure hit (her call 2026-11-11). Armour, shields and
    /// damage-taken multipliers (IncomingDamageFactor from Reinforcement, the superhuman body, Rika's full power,
    /// buffs) don't reduce it; neither do hediffs or genes that shrink damage. Done the way World Cutting Slash
    /// does it: Pawn.PreApplyDamage, where every defence acts, is skipped for the hit, and the hit is put back as it
    /// came in afterwards; armour off with SetIgnoreArmor, IncomingDamageFactor divided out.
    /// Hollow Wicker Basket and Simple Domain still protect (SureHit.Blocked), so a covered pawn gets no full hit.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_MutualLovePierce
    {
        public static bool Applies(Pawn victim, DamageInfo dinfo) =>
            YutaCopyDefOf.JJK_MutualLoveMark != null && dinfo.Instigator is Pawn a && Yuta.Is(a) && a != victim
            && victim.health?.hediffSet?.HasHediff(YutaCopyDefOf.JJK_MutualLoveMark) == true && !SureHit.Blocked(victim);

        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed, out DamageInfo? __state)
        {
            __state = null;
            if (!Applies(__instance, dinfo)) return true;
            __state = SukunaPsycasts.Patch_WorldSlashUnstoppable.MakeUnstoppable(dinfo, __instance);
            absorbed = false;
            return false;
        }

        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref DamageInfo dinfo, ref bool absorbed, DamageInfo? __state)
        {
            if (__state == null) return;
            dinfo = __state.Value;
            absorbed = false;
        }
    }

    /// <summary>Taking up a domain katana puts his own sword away in his inventory instead of dropping it
    /// (her call 2026-11-11); it comes back to his hand when the domain collapses.</summary>
    [HarmonyPatch]
    public static class Patch_MutualLoveKeepSword
    {
        static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var m in typeof(Pawn_EquipmentTracker).GetMethods())
                if (m.Name == nameof(Pawn_EquipmentTracker.MakeRoomFor)) yield return m;
        }

        public static void Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq)
        {
            var p = __instance?.pawn;
            var held = __instance?.Primary;
            if (p == null || held == null || eq == null || eq.TryGetComp<CompMutualLoveKatana>() == null) return;
            if (held.TryGetComp<CompMutualLoveKatana>() != null || p.inventory == null) return;   // swapping katanas: drop the old one
            var dom = HediffComp_MutualLove.Of(p);
            if (dom == null) return;
            if (__instance.TryTransferEquipmentToContainer(held, p.inventory.innerContainer)) dom.stashed = held;
        }
    }

    /// <summary>Once a minute, with or without a domain open: stray katanas go (MutualLove.SweepStrays).</summary>
    public class MutualLoveSweep : GameComponent
    {
        public MutualLoveSweep(Game game) { }
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 3600 == 1234) MutualLove.SweepStrays();
        }
    }
}
