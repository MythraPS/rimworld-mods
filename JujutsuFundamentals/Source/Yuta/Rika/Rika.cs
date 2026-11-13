using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using LudeonTK;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using VEFAbility = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class RikaDefOf
    {
        public static PawnKindDef JJK_RikaCurse, JJK_RikaShikigami;
        public static HediffDef JJK_RikaCursePower, JJK_RikaManifestTimer, JJK_RikaGrappled;
        public static FleckDef JJK_Fleck_RikaArm, JJK_Fleck_RikaMaw, JJK_Fleck_RikaGrapple;
        static RikaDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RikaDefOf));
    }

    /// <summary>
    /// Rika (her design 2026-11-11). Two forms, one at a time, kept in YutaTracker.rika:
    ///  - Cursed Spirit Rika: an Anomaly entity, at full power. Until Yuta learns his domain (stages 1 and 2, JJK 0;
    ///    her call 2026-11-11) she manifests on her own when someone hurts him or he is downed with enemies near,
    ///    attacks them, can't be ordered, and fades once no enemy has been near for 30 seconds. Manifest Rika calls
    ///    her too in those stages: then she is his to draft and order, for five minutes. Learning the domain
    ///    unravels her curse (a letter) and she never acts on her own again.
    ///  - Shikigami Rika: called with Manifest Rika, five minutes (canon), weaker; at the curse form's power while Yuta
    ///    wears his ring; adult Yuta's is at that power always, with no time limit.
    /// Curse Rika also goes after anyone else who hurts Yuta, a colonist included (her call 2026-11-11: canon, his
/// bullies). Shikigami Rika is draftable and takes orders like a colonist (her call 2026-11-11); undrafted she keeps
/// her own AI. Curse Rika can't be drafted or ordered. Neither ever dies on the map: going down or a death blow
/// sends her back.
    /// </summary>
    public static partial class Rika
    {
        public static readonly Color Smoke = new Color(0.16f, 0.12f, 0.22f);
        /// <summary>How near Yuta an enemy must be for Rika to go after it, and to keep Curse Rika manifested (proposal).</summary>
        public const float GuardRadius = 20f;
        /// <summary>Curse Rika fades after this long with no enemy near (proposal: 30 seconds).</summary>
        public const int CurseFadeTicks = 1800;

        public static bool IsRika(Thing t) => t is Pawn p && (p.kindDef == RikaDefOf.JJK_RikaCurse || p.kindDef == RikaDefOf.JJK_RikaShikigami);
        public static bool IsCurseForm(Pawn p) => p != null && p.kindDef == RikaDefOf.JJK_RikaCurse;
        public static Pawn Owner => RikaTracker.Instance?.owner;

        /// <summary>Shikigami Rika, ours, on the map: draftable and orderable. Curse Rika never is.</summary>
        public static bool Controllable(Pawn p) =>
            p != null && p == Current && (!IsCurseForm(p) || Called) && p.Spawned && !p.Dead && p.Faction != null && p.Faction.IsPlayer;

        /// <summary>Gives Shikigami Rika the draft controller the game only makes for humanlikes and colony mechs.</summary>
        public static void EnsureDrafter(Pawn p)
        {
            if (p == null || (IsCurseForm(p) && !Called)) return;
            if (p.drafter == null) p.drafter = new Pawn_DraftController(p);
        }

        public static bool KnowsManifest(Pawn p) => Jujutsu.Knows(p, YutaAbilities.Def(YutaAbilities.ManifestRika));
        /// <summary>Her call 2026-11-11: the curse is unravelled when Yuta learns his domain (stages 1 and 2 still have it).</summary>
        public static bool KnowsDomain(Pawn p) => Jujutsu.Knows(p, YutaAbilities.Def(YutaAbilities.Domain));
        public static bool CurseBroken(Pawn yuta) => RikaTracker.Instance?.unravelled == true || KnowsDomain(yuta);
        /// <summary>Is the Rika out now Cursed Spirit Rika called with Manifest Rika (draftable, five minutes)?</summary>
        public static bool Called => RikaTracker.Instance?.called == true;

        /// <summary>Calls Rika beside <paramref name="yuta"/> (any pawn works, for dev testing). Sends away whoever is here first.</summary>
        public static Pawn Summon(Pawn yuta, bool curse, bool called = false)
        {
            var map = yuta?.Map;
            var t = YutaTracker.Instance; var rt = RikaTracker.Instance;
            if (map == null || t == null || rt == null) return null;
            Dismiss(null);   // also clears a stale dying flag and attacker list when nobody is out
            var kind = curse ? RikaDefOf.JJK_RikaCurse : RikaDefOf.JJK_RikaShikigami;
            Pawn p;
            NoMagicClass.making = true;
            try { p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, yuta.Faction, PawnGenerationContext.NonPlayer, forceGenerateNewPawn: true)); }
            finally { NoMagicClass.making = false; }
            p.Name = new NameSingle("Rika");
            if (!CellFinder.TryFindRandomSpawnCellForPawnNear(yuta.Position, map, out var cell, 3)) cell = yuta.Position;
            GenSpawn.Spawn(p, cell, map);
            Puff(p.DrawPos, map, 3.5f);
            t.rika = p;
            rt.owner = yuta;
            rt.lastThreatTick = Find.TickManager.TicksGame;
            rt.called = curse && called;
            if (curse)
            {
                p.health.AddHediff(RikaDefOf.JJK_RikaCursePower);
                // called with Manifest Rika (stages 1 and 2, her call 2026-11-11): his to order, for five minutes
                if (rt.called) { p.health.AddHediff(RikaDefOf.JJK_RikaManifestTimer); EnsureDrafter(p); }
            }
            else
            {
                if (!Yuta.Adult(yuta)) p.health.AddHediff(RikaDefOf.JJK_RikaManifestTimer);
                EnsureDrafter(p);
            }
            SyncPower(p, yuta);
            // she comes out drafted, ready for orders (her call 2026-11-11); Curse Rika out of his control can't be
            if (Controllable(p) && p.drafter != null) p.drafter.Drafted = true;
            return p;
        }

        /// <summary>Sends Rika back: she leaves no body and doesn't count as a death.</summary>
        public static void Dismiss(string message)
        {
            var r = Forget();
            if (r == null || r.Destroyed || r.Dead) return;
            if (r.Spawned)
            {
                Puff(r.DrawPos, r.Map, 3f);
                if (message != null) Messages.Message(message, new TargetInfo(r.Position, r.Map), MessageTypeDefOf.NeutralEvent, false);
            }
            r.Destroy(DestroyMode.Vanish);
            // left as a world pawn: combat logs and tales still point at her; the game clears her once nothing does
        }

        /// <summary>Clears who is out and the per-manifestation flags, and ends a ring connection that came with
        /// her at once (not up to 30 ticks later, when a new Rika could already be out). Returns the Rika that was out.</summary>
        public static Pawn Forget()
        {
            var t = YutaTracker.Instance; var rt = RikaTracker.Instance;
            var r = t?.rika;
            if (t != null) t.rika = null;
            if (rt == null) return r;
            bool wasRing = rt.ring;
            rt.attackers.Clear(); rt.dying = false; rt.called = false; rt.ring = false;
            // ring cleared first, so RikaRing.Ended doesn't call back in here
            if (wasRing && rt.owner != null && RikaRing.Connected(rt.owner)) RikaRing.Disconnect(rt.owner, null);
            return r;
        }

        /// <summary>Shikigami Rika has the curse form's power only for adult Yuta. (Until her call 2026-11-11 also while
        /// he wore the ring; now putting the ring on brings her in her curse look instead, see RikaRing.)</summary>
        public static void SyncPower(Pawn r, Pawn yuta)
        {
            if (r?.health == null || IsCurseForm(r)) return;
            bool want = Yuta.Adult(yuta);
            var h = r.health.hediffSet.GetFirstHediffOfDef(RikaDefOf.JJK_RikaCursePower);
            if (want && h == null) r.health.AddHediff(RikaDefOf.JJK_RikaCursePower);
            else if (!want && h != null) r.health.RemoveHediff(h);
        }

        public static void Puff(Vector3 at, Map map, float size)
        {
            if (map == null) return;
            for (int i = 0; i < 3; i++)
                FleckMaker.ThrowDustPuffThick(at + new Vector3(Rand.Range(-0.6f, 0.6f), 0f, Rand.Range(-0.6f, 0.6f)), map, size, Smoke);
        }

        public static void Fleck(FleckDef def, Vector3 at, Map map, float scale)
        {
            if (def == null || map == null) return;
            FleckMaker.Static(at, map, def, scale);
        }

        /// <summary>An enemy worth going after: hostile to Rika, standing, near Yuta. <paramref name="grudge"/>: someone
        /// who hurt Yuta, whom Curse Rika goes after even if they are on his side.</summary>
        public static bool Threat(Pawn rika, Pawn owner, Pawn e, bool grudge = false) =>
            e != null && e != rika && e != owner && e.Spawned && !e.Dead && !e.Downed && e.Map == rika.Map
            && (e.HostileTo(rika) || (grudge && IsCurseForm(rika))) && e.Position.InHorDistOf(owner.Position, GuardRadius);

        public static bool AnyThreatNear(Pawn owner)
        {
            var map = owner?.Map;
            if (map == null) return false;
            foreach (var e in map.attackTargetsCache.GetPotentialTargetsFor(owner))
                if (e.Thing is Pawn ep && !ep.Dead && !ep.Downed && ep.Position.InHorDistOf(owner.Position, GuardRadius)) return true;
            return false;
        }
    }

    /// <summary>Rika's state beyond the pawn itself (her owner, the curse's timer, who hurt Yuta).</summary>
    public class RikaTracker : GameComponent
    {
        public static RikaTracker Instance;

        public Pawn owner;
        public int lastThreatTick;
        /// <summary>Has Rika's curse been unravelled (Yuta learned his domain)? No uncontrolled manifestations after.</summary>
        public bool unravelled;
        /// <summary>The Curse Rika out now was called with Manifest Rika.</summary>
        public bool called;
        /// <summary>The Rika out now came with the ring (RikaRing), and when the ring can be put on again.</summary>
        public bool ring;
        public int ringReadyTick;
        /// <summary>Was Yuta ever seen with the curse still on him? Only then does breaking it send the letter (a Yuta who
        /// starts at a later stage has it from the start).</summary>
        public bool sawCursed;
        /// <summary>Set when something tried to kill Rika: she's sent back on the next tick instead.</summary>
        public bool dying;
        public readonly List<Pawn> attackers = new List<Pawn>();

        public RikaTracker(Game game) { Instance = this; }

        public override void GameComponentTick()
        {
            var t = YutaTracker.Instance;
            if (t == null) return;
            var r = t.rika;
            if (dying && r != null) { Rika.Dismiss($"{r.LabelShortCap} is struck down and goes back to {owner?.LabelShort ?? "Yuta"}."); return; }
            int now = Find.TickManager.TicksGame;
            if (now % 30 != 11) return;

            var yuta = t.yuta;
            if (yuta != null && !yuta.Dead)
            {
                bool knows = Rika.KnowsDomain(yuta);
                if (!knows) sawCursed = true;
                else if (!unravelled)
                {
                    unravelled = true;
                    if (sawCursed)
                        Find.LetterStack.ReceiveLetter("Rika's curse unravelled",
                            $"{yuta.LabelShortCap} has unravelled Rika's curse. Cursed Spirit Rika no longer comes out on her own: from now on Manifest Rika calls Shikigami Rika.",
                            LetterDefOf.PositiveEvent, yuta);
                }
                // downed with enemies near: the curse comes out even without a new hit
                if (!knows && !unravelled && r == null && yuta.Spawned && yuta.Downed && Rika.AnyThreatNear(yuta))
                {
                    Rika.Summon(yuta, curse: true);
                    return;
                }
            }

            if (r == null) return;
            if (r.Destroyed || r.Dead) { Rika.Forget(); return; }   // was only t.rika = null: called/ring/dying stayed set
            if (owner == null || owner.Dead || !owner.Spawned || !r.Spawned || owner.Map != r.Map)
            {
                Rika.Dismiss($"{r.LabelShortCap} goes back.");
                return;
            }
            if (r.Downed) { Rika.Dismiss($"{r.LabelShortCap} is struck down and goes back to {owner.LabelShort}."); return; }
            attackers.RemoveAll(a => a == null || a.Dead || a.Downed || !a.Spawned || a.Map != r.Map);

            if (Rika.IsCurseForm(r) && !called)
            {
                if (Rika.AnyThreatNear(owner) || attackers.Count > 0) lastThreatTick = now;
                if (unravelled && Yuta.Is(owner)) { Rika.Dismiss(null); return; }
                if (now - lastThreatTick > Rika.CurseFadeTicks) Rika.Dismiss($"With no enemy left near {owner.LabelShort}, {r.LabelShort} fades.");
                return;
            }
            if (Rika.IsCurseForm(r) && unravelled && !ring && Yuta.Is(owner)) { Rika.Dismiss(null); return; }
            // Shikigami Rika, or Curse Rika he called: five minutes, unless adult Yuta's
            if (!Yuta.Adult(owner) && !r.health.hediffSet.HasHediff(RikaDefOf.JJK_RikaManifestTimer))
            {
                Rika.Dismiss($"Five minutes are up: {r.LabelShort} goes back to {owner.LabelShort}.");
                return;
            }
            if (Yuta.Adult(owner))
            {
                var timer = r.health.hediffSet.GetFirstHediffOfDef(RikaDefOf.JJK_RikaManifestTimer);
                if (timer != null) r.health.RemoveHediff(timer);
            }
            Rika.SyncPower(r, owner);
            Rika.EnsureDrafter(r);   // a save from before 2026-11-11 had none
        }

        /// <summary>Someone hurt Yuta (or whoever owns Rika right now). Shikigami Rika only answers enemies; Curse Rika
        /// answers anyone (her call 2026-11-11: his bullies).</summary>
        public void Hurt(Pawn victim, Pawn attacker, bool hostile)
        {
            var r = YutaTracker.Instance?.rika;
            if (r != null && owner == victim)
            {
                if (!hostile && !Rika.IsCurseForm(r)) return;
                if (attacker != null && !attackers.Contains(attacker)) attackers.Add(attacker);
                lastThreatTick = Find.TickManager.TicksGame;
                return;
            }
            if (r == null && Yuta.Is(victim) && !Rika.CurseBroken(victim) && victim.Spawned)
            {
                var p = Rika.Summon(victim, curse: true);   // clears the list
                if (p != null && attacker != null) attackers.Add(attacker);
                if (p != null)
                    Messages.Message($"Someone hurt {victim.LabelShort}. Cursed Spirit Rika comes out for them.", p, MessageTypeDefOf.ThreatSmall, false);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref owner, "rikaOwner");
            Scribe_Values.Look(ref lastThreatTick, "rikaLastThreat");
            Scribe_Values.Look(ref unravelled, "rikaUnravelled");
            Scribe_Values.Look(ref called, "rikaCalled");
            Scribe_Values.Look(ref ring, "rikaRing");
            Scribe_Values.Look(ref ringReadyTick, "rikaRingReady");
            Scribe_Values.Look(ref sawCursed, "rikaSawCursed");
        }
    }

    /// <summary>A hit on Yuta, by an enemy or by anyone else (a colonist in a social fight), brings Curse Rika out
    /// (until he can call her himself) and tells her who to go after.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_RikaProtects
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || __instance == null || __instance.Dead) return;
            var rt = RikaTracker.Instance;
            if (rt == null) return;
            if (!Yuta.Is(__instance) && rt.owner != __instance) return;
            var inst = dinfo.Instigator;
            if (inst == null || inst == __instance || Rika.IsRika(inst)) return;
            bool hostile = inst.HostileTo(__instance);
            if (!hostile && !(inst is Pawn)) return;   // a friendly turret or trap isn't a bully
            rt.Hurt(__instance, inst as Pawn, hostile);
        }
    }

    /// <summary>Rika never dies on the map: a death blow sends her back on the next tick instead (no body, no death).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_RikaNeverDies
    {
        public static bool Prefix(Pawn __instance)
        {
            if (!Rika.IsRika(__instance) || __instance.Destroyed) return true;
            if (YutaTracker.Instance?.rika != __instance) return true;   // a stray Rika (not the current one) dies normally
            if (RikaTracker.Instance != null) RikaTracker.Instance.dying = true;
            return false;
        }
    }

    [StaticConstructorOnStartup]
    public static class RikaStartup
    {
        static RikaStartup()
        {
            // Cursed Spirit Rika is an Anomaly entity: RaceProperties.IsAnomalyEntity is true for EntityFlesh,
            // EntityMechanical and Fleshbeast flesh (and only with Anomaly active). EntityFlesh exists only with Anomaly.
            if (!ModsConfig.AnomalyActive) return;
            var ft = DefDatabase<FleshTypeDef>.GetNamedSilentFail("EntityFlesh");
            var race = RikaDefOf.JJK_RikaCurse?.race?.race;
            var f = AccessTools.Field(typeof(RaceProperties), "fleshType");   // not public
            if (ft != null && race != null && f != null) f.SetValue(race, ft);
        }
    }

    // ==================================================================== AI

    /// <summary>Go after whoever hurt Rika's owner, else the nearest enemy near the owner.</summary>
    public class JobGiver_RikaFight : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Drafted) return null;
            var owner = Rika.Owner;
            if (owner == null || !owner.Spawned || owner.Map != pawn.Map) return null;
            Pawn target = null;
            float best = float.MaxValue;
            var rt = RikaTracker.Instance;
            if (rt != null)
                foreach (var a in rt.attackers)
                    if (Rika.Threat(pawn, owner, a, grudge: true) && pawn.CanReach(a, PathEndMode.Touch, Danger.Deadly))
                    {
                        float d = a.Position.DistanceToSquared(pawn.Position);
                        if (d < best) { best = d; target = a; }
                    }
            if (target == null)
                foreach (var e in pawn.Map.attackTargetsCache.GetPotentialTargetsFor(pawn))
                {
                    var ep = e.Thing as Pawn;
                    if (!Rika.Threat(pawn, owner, ep) || !pawn.CanReach(ep, PathEndMode.Touch, Danger.Deadly)) continue;
                    float d = ep.Position.DistanceToSquared(pawn.Position);
                    if (d < best) { best = d; target = ep; }
                }
            if (target == null) return null;
            var job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
            job.expiryInterval = 120;
            job.checkOverrideOnExpire = true;
            job.killIncappedTarget = false;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }
    }

    /// <summary>Stay by Rika's owner.</summary>
    public class JobGiver_RikaFollow : ThinkNode_JobGiver
    {
        public const float Near = 4f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Drafted) return null;
            var owner = Rika.Owner;
            var map = pawn.Map;
            if (owner == null || !owner.Spawned || owner.Map != map) return null;
            if (pawn.Position.InHorDistOf(owner.Position, Near)) return null;
            if (!CellFinder.TryFindRandomReachableNearbyCell(owner.Position, map, 3f, TraverseParms.For(pawn), c => c.Standable(map), null, out var cell))
                cell = owner.Position;
            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.locomotionUrgency = LocomotionUrgency.Jog;
            job.expiryInterval = 180;
            job.checkOverrideOnExpire = true;
            return job;
        }
    }

    // ==================================================================== abilities

    /// <summary>Manifest Rika: Shikigami Rika beside Yuta (see Rika).</summary>
    public class Ability_ManifestRika : VEFAbility
    {
        public override bool IsEnabledForPawn(out string reason)
        {
            if (YutaTracker.Instance?.rika != null) { reason = "Rika is already manifested."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            // her call 2026-11-11: until the curse is unravelled (his domain) he calls Cursed Spirit Rika
            Rika.Summon(pawn, curse: Yuta.Is(pawn) && !Rika.CurseBroken(pawn), called: true);
        }
    }

    /// <summary>Partial manifestations scale with Jujutsu.Power and Raise Cursed Energy Output.</summary>
    public abstract class Ability_RikaPartial : VEFAbility
    {
        /// <summary>While Rika is out, these are hers (RikaHands).</summary>
        public override bool IsEnabledForPawn(out string reason)
        {
            if (RikaHands.BlockOnYuta(pawn, out reason)) return false;
            return base.IsEnabledForPawn(out reason);
        }

        protected Pawn Target(GlobalTargetInfo[] targets)
        {
            if (pawn?.Map == null || targets == null || targets.Length == 0) return null;
            var t = targets[0].Thing as Pawn;
            return t != null && t.Spawned && !t.Dead && t.Map == pawn.Map ? t : null;
        }

        protected float Scale => Jujutsu.Power(pawn) * Jujutsu.Boost(pawn);
    }

    /// <summary>Rika's Arm: heavy blunt hit and a short stun (proposal: 30 damage, 1.5 seconds).</summary>
    public class Ability_RikaArm : Ability_RikaPartial
    {
        public const float Damage = 30f;
        public const int StunTicks = 90;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            // her call 2026-11-11: the arm hits buildings and objects too
            var t = targets != null && targets.Length > 0 ? targets[0].Thing : null;
            if (t == null || !t.Spawned || t.Destroyed || pawn?.Map == null || t.Map != pawn.Map) return;
            Rika.Fleck(RikaDefOf.JJK_Fleck_RikaArm, t.DrawPos + new Vector3(0f, 0f, 0.3f), t.Map, 2.6f);
            Rika.Puff(t.DrawPos, t.Map, 1.6f);
            t.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Damage * Scale, 0.4f, -1f, pawn));
            if (t is Pawn tp && !tp.Dead && tp.Spawned) tp.stances?.stunner?.StunFor(StunTicks, pawn, true, true, false);
        }
    }

    /// <summary>Rika's Bite: a bite that bleeds (proposal: 26 damage, armour penetration 0.5).</summary>
    public class Ability_RikaBite : Ability_RikaPartial
    {
        public const float Damage = 26f;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = Target(targets);
            if (t == null) return;
            Rika.Fleck(RikaDefOf.JJK_Fleck_RikaMaw, t.DrawPos, t.Map, 2.4f);
            t.TakeDamage(new DamageInfo(DamageDefOf.Bite, Damage * Scale, 0.5f, -1f, pawn));
        }
    }

    /// <summary>Rika's Grapple: held in place for five seconds (proposal), as a stun, so Copy counts it.</summary>
    public class Ability_RikaGrapple : Ability_RikaPartial
    {
        public const int HoldTicks = 300;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = Target(targets);
            if (t == null) return;
            Rika.Fleck(RikaDefOf.JJK_Fleck_RikaGrapple, t.DrawPos, t.Map, 2.2f);
            t.stances?.stunner?.StunFor(HoldTicks, pawn, true, false, true);
            if (t.stances?.stunner?.Stunned != true) return;   // blocked (a Hollow Wicker Basket, a Simple Domain)
            var old = t.health.hediffSet.GetFirstHediffOfDef(RikaDefOf.JJK_RikaGrappled);
            if (old != null) t.health.RemoveHediff(old);
            t.health.AddHediff(RikaDefOf.JJK_RikaGrappled);
        }
    }

    // ==================================================================== gizmo

    /// <summary>Send Shikigami Rika back early, from Yuta or from Rika herself. Cursed Spirit Rika can't be sent back.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    [StaticConstructorOnStartup]
    public static class Patch_RikaGizmos
    {
        private static Texture2D icon;
        // internal in the game, so by reflection
        private static readonly System.Reflection.MethodInfo draftGizmos = AccessTools.Method(typeof(Pawn_DraftController), "GetGizmos");

        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (var g in __result) yield return g;
            var r = YutaTracker.Instance?.rika;
            // vanilla only shows these for colonists, colony mechs and ghouls
            if (__instance == r && Rika.Controllable(r) && r.drafter != null)
            {
                if (draftGizmos != null && draftGizmos.Invoke(r.drafter, null) is IEnumerable<Gizmo> dg)
                    foreach (var g in dg) yield return g;
                foreach (var g in PawnAttackGizmoUtility.GetAttackGizmos(r)) yield return g;
            }
            if (r == null || (Rika.IsCurseForm(r) && !Rika.Called) || __instance.Faction != Faction.OfPlayer) yield break;
            if (__instance != r && __instance != Rika.Owner) yield break;
            if (icon == null) icon = ContentFinder<Texture2D>.Get("Abilities/Yuta/Rika/ManifestRika", false) ?? BaseContent.BadTex;
            yield return new Command_Action
            {
                defaultLabel = "Send Rika Back",
                defaultDesc = "Rika goes back to Yuta now, before her time is up.",
                icon = icon,
                action = () => Rika.Dismiss(null),
            };
        }
    }

    // ==================================================================== orders

    /// <summary>Shikigami Rika takes orders (the right-click menu's pawn filter reads CanTakeOrder).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.CanTakeOrder), MethodType.Getter)]
    public static class Patch_RikaTakesOrders
    {
        public static void Postfix(Pawn __instance, ref bool __result) { if (!__result && Rika.Controllable(__instance)) __result = true; }
    }

    /// <summary>...and counts as player-controlled (drafted auto-attack in JobDriver_Wait, melee jobs, pathing).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsPlayerControlled), MethodType.Getter)]
    public static class Patch_RikaPlayerControlled
    {
        public static void Postfix(Pawn __instance, ref bool __result) { if (!__result && Rika.Controllable(__instance)) __result = true; }
    }

    /// <summary>The attack gizmos' own check (colonist, colony mech or ghoul).</summary>
    [HarmonyPatch(typeof(PawnAttackGizmoUtility), "CanOrderPlayerPawn")]   // private
    public static class Patch_RikaAttackGizmos
    {
        public static void Postfix(Pawn pawn, ref bool __result) { if (!__result && Rika.Controllable(pawn)) __result = true; }
    }

    /// <summary>Right-click melee attack: the same colonist/mech/ghoul check, skipped for her with the game's own flag.</summary>
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetMeleeAttackAction),
        new[] { typeof(Pawn), typeof(LocalTargetInfo), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_RikaMeleeOrder
    {
        public static void Postfix(Pawn pawn, LocalTargetInfo target, ref string failStr, bool ignoreControlled, ref System.Action __result)
        {
            if (__result != null || ignoreControlled || !Rika.Controllable(pawn)) return;
            __result = FloatMenuUtility.GetMeleeAttackAction(pawn, target, out failStr, true);
        }
    }

    // ==================================================================== dev

    public static class RikaDebug
    {
        private const string Cat = "Jujutsu Kaisen";

        [DebugAction(Cat, "Rika: manifest Shikigami Rika (on pawn)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Shikigami(Pawn p) { if (p != null && !Rika.IsRika(p)) Rika.Summon(p, curse: false); }

        [DebugAction(Cat, "Rika: manifest Cursed Spirit Rika (on pawn)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Curse(Pawn p) { if (p != null && !Rika.IsRika(p)) Rika.Summon(p, curse: true); }

        [DebugAction(Cat, "Rika: send Rika back", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Back() => Rika.Dismiss("Rika goes back (dev).");

        [DebugAction(Cat, "Rika: reset the curse (uncontrolled again until Yuta's domain is learned)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Reset()
        {
            var rt = RikaTracker.Instance;
            if (rt == null) return;
            rt.unravelled = false;
            rt.sawCursed = false;
            // the curse breaks when Yuta learns his domain (her call 2026-11-11), so a Yuta who knows it unravels it again at once
            Messages.Message("Rika's curse is tangled again: she acts on her own until Yuta learns his domain.", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
