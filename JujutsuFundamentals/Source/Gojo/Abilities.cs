using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbility = VEF.Abilities.Ability_Blank;

namespace GojoPsycasts
{
    /// <summary>A tree node with no button: Infinity, Blue Fist, Black Flash and
    /// reverse cursed technique work through hediffs kept by HediffComp_Limitless.</summary>
    public class Ability_Passive : VEFAbility
    {
        public override bool ShowGizmoOnPawn() => false;

        public override bool IsEnabledForPawn(out string reason)
        {
            reason = "Passive: always active once learned.";
            return false;
        }

        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    internal static class Orbs
    {
        public static Projectile Launch(Pawn caster, ThingDef def, LocalTargetInfo target)
        {
            if (caster?.Map == null || def == null) return null;
            var proj = (Projectile)GenSpawn.Spawn(def, caster.Position, caster.Map);
            proj.Launch(caster, caster.DrawPos, target, target, ProjectileHitFlags.IntendedTarget, false, null, null);
            return proj;
        }

        public static LocalTargetInfo Target(GlobalTargetInfo[] targets) =>
            targets == null || targets.Length == 0 ? LocalTargetInfo.Invalid
            : targets[0].HasThing ? new LocalTargetInfo(targets[0].Thing) : new LocalTargetInfo(targets[0].Cell);

        /// <summary>The cell <paramref name="range"/> away from <paramref name="from"/>
        /// through <paramref name="aim"/>, kept inside the map.</summary>
        public static IntVec3 Beyond(IntVec3 from, IntVec3 aim, float range, Map map)
        {
            var dir = (aim - from).ToVector3();
            if (dir.sqrMagnitude < 0.01f) return aim;
            dir.Normalize();
            var last = from;
            for (float d = 1f; d <= range; d += 1f)
            {
                var c = (from.ToVector3Shifted() + dir * d).ToIntVec3();
                if (!c.InBounds(map)) break;
                last = c;
            }
            return last;
        }
    }

    public class Ability_Blue : VEFAbility
    {
        // the targeting circle shows the real pull (her report 2026-11-07: the crater was far bigger than the circle)
        public override float GetRadiusForPawn() => def.radius * Gojo.AreaScale(pawn) * Gojo.PsyfocusBoostAfter(pawn, def);

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = Orbs.Target(targets);
            if (!t.IsValid) return;
            Orbs.Launch(pawn, GojoDefOf.Gojo_BlueOrb, t);
        }
    }

    public class Ability_Red : VEFAbility
    {
        public const float BeamPush = 6f;

        // the real burst while aiming the orb (her report 2026-11-07)
        public override float GetRadiusForPawn() =>
            def.radius * Gojo.AreaScale(pawn) * Mathf.Max(1f, Gojo.PsyfocusBoostAfter(pawn, def));

        /// <summary>Red is always the orb now; the beam is its own button, Red: Beam (her call 2026-11-11: two
        /// buttons instead of the orb/beam switch).</summary>
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = Orbs.Target(targets);
            if (!t.IsValid || pawn.Map == null) return;
            Orbs.Launch(pawn, GojoDefOf.Gojo_RedOrb, t);
        }

        /// <summary>Held and fired: everything on the line is hit and driven back.</summary>
        public static void Beam(Pawn caster, IntVec3 aim)
        {
            var map = caster.Map;
            float power = Gojo.Power(caster);
            var end = Orbs.Beyond(caster.Position, aim, GojoAbilityDefOf.Gojo_Red.range * power, map);
            var dir = (end - caster.Position).ToVector3().normalized;
            var hit = new HashSet<Thing>();
            var reached = caster.Position;
            foreach (var c in GenSight.PointsOnLineOfSight(caster.Position, end))
            {
                if (!c.InBounds(map)) break;
                reached = c;
                foreach (var thing in c.GetThingList(map).ToList())
                {
                    if (thing == caster || thing.Destroyed || !hit.Add(thing)) continue;
                    if (thing is Pawn victim)
                    {
                        victim.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, OrbTracker.RedDamage * power, 0.4f, -1f, caster));
                        Fx.NotifyAttacked(victim, caster);
                        if (victim.Spawned) Fx.Throw(victim, Fx.Along(victim.Position, dir, BeamPush * power, map));
                    }
                    else if (thing.def.useHitPoints && thing.def.category == ThingCategory.Building)
                        thing.TakeDamage(new DamageInfo(GojoDefOf.Gojo_Force, OrbTracker.RedBuildingDamage * power, 0.4f, -1f, caster));
                }
                if (c.Impassable(map) && c != caster.Position) break;   // a wall that survived stops the beam
            }
            DrawBeam(caster.DrawPos, reached.ToVector3Shifted(), map);
        }

        /// <summary>The beam as one straight line: overlapping stretched segments every
        /// half cell along the true line, not one blob per cell of the stepped path.</summary>
        private static void DrawBeam(Vector3 from, Vector3 to, Map map)
        {
            var d = to - from; d.y = 0f;
            float len = d.magnitude;
            if (len < 0.1f) return;
            var dir = d / len;
            float angle = dir.AngleFlat();
            for (float t = 0.25f; t <= len; t += 0.5f)
            {
                var data = FleckMaker.GetDataStatic(from + dir * t, map, GojoDefOf.Gojo_Fleck_RedBeam, 1f);
                data.exactScale = new Vector3(1.3f, 1f, 1.1f);
                data.rotation = angle;
                map.flecks.CreateFleck(data);
            }
        }
    }

    /// <summary>Red: Beam (her call 2026-11-11): Red held and fired as a line that hammers everything in it backward.
    /// Comes with Red, like Push; costs what Red does (8% psyfocus, 24 neural heat; Six Eyes halves the psyfocus).</summary>
    public class Ability_RedBeam : VEFAbility
    {
        public const float Psyfocus = 0.08f, Heat = 24f;

        private float Cost => Psyfocus * pawn.GetStatValue(Gojo.CostFactorStat);

        public override bool IsEnabledForPawn(out string reason)
        {
            var tr = pawn?.psychicEntropy;
            if (tr != null && tr.CurrentPsyfocus < Cost) { reason = "Not enough psyfocus."; return false; }
            if (tr != null && tr.WouldOverflowEntropy(Heat)) { reason = "Neural heat too high."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = Orbs.Target(targets);
            if (!t.IsValid || pawn.Map == null) return;
            var tr = pawn.psychicEntropy;
            if (tr != null)
            {
                tr.OffsetPsyfocusDirectly(-Mathf.Min(Cost, tr.CurrentPsyfocus));
                tr.TryAddEntropy(Heat, pawn, scale: true, overLimit: false);
            }
            Ability_Red.Beam(pawn, t.Cell);
        }
    }

    public class Ability_Purple : VEFAbility
    {
        /// <summary>The incantation, as in the manga (her ask 2026-11-06): one line every ChantEvery ticks
        /// over the warmup, then the name as it fires. The warmup is long enough to say it all.</summary>
        public static readonly string[] Chant =
            { "Phase.", "Twilight.", "Eyes of wisdom.", "Nine ropes.", "Polarized light.", "Crow and Declaration.", "Between front and back." };
        public const int ChantEvery = 36;   // 0.6 s a line
        public const int ChantWarmup = 300; // 5 s: all seven lines, and a beat before it fires

        /// <summary>The chanted Purple: twice as wide, half again as far.</summary>
        public const float ChantWidth = 2f, ChantRange = 1.5f;

        private int warmupStart = -1;

        private bool Chanting => HediffComp_Limitless.Of(pawn)?.chant ?? true;

        public override int GetCastTimeForPawn() => Chanting ? Mathf.Max(base.GetCastTimeForPawn(), ChantWarmup) : base.GetCastTimeForPawn();

        // VEF calls the no-argument PreWarmupAction (the one with a target is obsolete and never called), so before
        // 2026-11-10 warmupStart was never reset and later chants ran all their lines at once.
        public override void PreWarmupAction()
        {
            base.PreWarmupAction();
            warmupStart = Find.TickManager.TicksGame;
        }

        public override void WarmupToil(Verse.AI.Toil toil)
        {
            base.WarmupToil(toil);
            int said = 0;
            if (!Chanting) return;
            toil.AddPreTickAction(() =>
            {
                if (warmupStart < 0) warmupStart = Find.TickManager.TicksGame;
                int line = (Find.TickManager.TicksGame - warmupStart) / ChantEvery;
                if (line < said || said >= Chant.Length) return;
                Say(Chant[said], 2.2f);
                said++;
            });
        }

        private void Say(string text, float hold)
        {
            if (pawn?.Map == null) return;
            MoteMaker.ThrowText(pawn.DrawPos + new Vector3(0f, 0f, 0.9f), pawn.Map, text, new Color(0.82f, 0.7f, 1f), hold);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            bool chanted = Chanting;
            if (chanted) Say("Hollow Purple.", 3f);
            warmupStart = -1;
            base.Cast(targets);
            var t = Orbs.Target(targets);
            if (!t.IsValid || pawn.Map == null) return;
            var far = Orbs.Beyond(pawn.Position, t.Cell, def.range * Gojo.Power(pawn) * (chanted ? ChantRange : 1f), pawn.Map);
            if (Orbs.Launch(pawn, GojoDefOf.Gojo_PurpleOrb, far) is Projectile_Purple pp) pp.chanted = chanted;
        }

        /// <summary>Purple only needs a direction (her report 2026-11-04: pawns could not be
        /// targeted), so whatever is clicked counts as the ground under it.</summary>
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true) =>
            target.IsValid && base.ValidateTarget(new LocalTargetInfo(target.Cell), showMessages);

        public override bool CanHitTarget(LocalTargetInfo target) =>
            target.IsValid && base.CanHitTarget(new LocalTargetInfo(target.Cell));
    }

    public class Ability_InfiniteVoid : VEFAbility
    {
        /// <summary>While the domain is open the same button closes it (her call 2026-10-31).</summary>
        public override Gizmo GetGizmo()
        {
            var normal = base.GetGizmo();
            var open = pawn?.health?.hediffSet?.GetFirstHediffOfDef(GojoDefOf.Gojo_InfiniteVoid);
            if (open == null) return normal;
            return new Command_Action
            {
                defaultLabel = "Close Domain",
                defaultDesc = "Let Infinite Void collapse now. Anyone caught inside stays paralysed for a while after.",
                icon = def.icon,
                Order = normal?.Order ?? 0f,
                action = () => { if (pawn.health.hediffSet.hediffs.Contains(open)) pawn.health.RemoveHediff(open); },
            };
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            // a domain is a huge strain (her call 2026-11-03): half the caster's heat limit on top of the
            // cast's own heat, never past the limit itself
            // calamity grades keep the old strain (half their limit); anyone else ends near the limit (her call 2026-11-10)
            JujutsuFundamentals.Domains.Strain(pawn);
            Messages.Message("Domain Expansion: Infinite Void.", pawn, MessageTypeDefOf.ThreatBig, false);
        }
    }

    /// <summary>No range limit on the map (her call 2026-10-28). No range ring either:
    /// a ring this size cannot be drawn.</summary>
    public class Ability_Teleport : VEFAbility
    {
        public override float GetRangeForPawn() => 99999f;

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (target.IsValid) GenDraw.DrawTargetHighlight(target);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length == 0 || pawn.Map == null) return;
            var map = pawn.Map;
            var cell = targets[0].Cell;
            if (!cell.Standable(map) && !CellFinder.TryFindRandomCellNear(cell, map, 3, c => c.Standable(map), out cell)) return;
            Teleport.OnMap(pawn, cell);
        }
    }

    public static class Teleport
    {
        public const float FarPsyfocus = 0.10f;
        public const float FarHeat = 20f;

        public static void OnMap(Pawn pawn, IntVec3 cell)
        {
            var map = pawn.Map;
            Fx.TeleportFlash(pawn.Position, map);
            pawn.Position = cell;
            pawn.Notify_Teleported(true, true);
            Fx.TeleportFlash(cell, map);
            if (pawn.Faction == Faction.OfPlayer && Find.Selector.IsSelected(pawn)) CameraJumper.TryJump(pawn);
        }

        public static bool CanAffordFar(Pawn pawn, out string why)
        {
            why = null;
            var tr = pawn.psychicEntropy;
            if (tr == null) return true;
            float cost = FarPsyfocus * pawn.GetStatValue(Gojo.CostFactorStat);
            if (tr.CurrentPsyfocus < cost) { why = "Not enough psyfocus."; return false; }
            if (tr.WouldOverflowEntropy(FarHeat * pawn.GetStatValue(StatDefOf.PsychicEntropyGain)))
            { why = "Neural heat would overflow."; return false; }
            return true;
        }

        public static void BeginFar(Pawn pawn)
        {
            CameraJumper.TryJump(CameraJumper.GetWorldTarget(pawn));
            Find.WorldSelector.ClearSelection();
            Find.WorldTargeter.BeginTargeting(t => Arrive(pawn, t), true, GojoAbilityDefOf.Gojo_Teleport.icon, true);
        }

        private static bool Arrive(Pawn pawn, GlobalTargetInfo target)
        {
            if (!target.IsValid || pawn == null || pawn.Dead) return false;
            if (!CanAffordFar(pawn, out string why)) { Messages.Message(why, MessageTypeDefOf.RejectInput, false); return false; }
            var tile = target.Tile;

            var tr = pawn.psychicEntropy;
            if (tr != null)
            {
                tr.OffsetPsyfocusDirectly(-FarPsyfocus * pawn.GetStatValue(Gojo.CostFactorStat));
                tr.TryAddEntropy(FarHeat, pawn, scale: true, overLimit: false);
            }

            var parent = Find.WorldObjects.MapParentAt(tile);
            var destMap = parent != null && parent.HasMap ? parent.Map : null;

            // Leave wherever he is: a map or a caravan.
            if (pawn.Spawned) { Fx.TeleportFlash(pawn.Position, pawn.Map); pawn.DeSpawn(); }
            var caravan = pawn.GetCaravan();
            if (caravan != null)
            {
                caravan.RemovePawn(pawn);
                if (!caravan.PawnsListForReading.Any()) caravan.Destroy();
            }

            if (destMap != null)
            {
                var cell = CellFinder.RandomClosewalkCellNear(destMap.Center, destMap, 12);
                GenSpawn.Spawn(pawn, cell, destMap);
                Fx.TeleportFlash(cell, destMap);
                CameraJumper.TryJump(pawn);
            }
            else
            {
                var made = CaravanMaker.MakeCaravan(new[] { pawn }, pawn.Faction, tile, true);
                CameraJumper.TryJump(made);
            }
            Messages.Message($"{pawn.LabelShortCap} teleports.", MessageTypeDefOf.NeutralEvent, false);
            return true;
        }
    }

    /// <summary>Speed is a sustained toggle: on until switched off, paying psyfocus
    /// every second, lapsing while it cannot pay and coming back when it can.</summary>
    public class Ability_ToggleSpeed : VEFAbility
    {
        public override Gizmo GetGizmo() => new Command_Toggle
        {
            defaultLabel = "Blue: Acceleration",
            defaultDesc = def.description,
            icon = def.icon,
            isActive = () => SpeedToggle.IsOn(pawn),
            toggleAction = () => SpeedToggle.Set(pawn, !SpeedToggle.IsOn(pawn)),
            activateSound = SoundDefOf.Tick_Tiny,
            Order = 30f,
        };

        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    public class SpeedToggle : GameComponent
    {
        private static HashSet<int> on = new HashSet<int>();
        public const float Drain = 0.002f;   // psyfocus per second, before Six Eyes

        public SpeedToggle(Game game) { on = new HashSet<int>(); }

        public static bool IsOn(Pawn p) => p != null && on.Contains(p.thingIDNumber);

        public static void Set(Pawn p, bool now)
        {
            if (now) { on.Add(p.thingIDNumber); Pay(p); }
            else
            {
                on.Remove(p.thingIDNumber);
                var h = p.health.hediffSet.GetFirstHediffOfDef(GojoDefOf.Gojo_Speed);
                if (h != null) p.health.RemoveHediff(h);
            }
        }

        private static void Pay(Pawn p)
        {
            var tr = p.psychicEntropy;
            float drain = Drain * p.GetStatValue(Gojo.CostFactorStat);
            var h = p.health.hediffSet.GetFirstHediffOfDef(GojoDefOf.Gojo_Speed);
            if (tr == null || tr.CurrentPsyfocus <= drain || tr.EntropyValue >= tr.MaxEntropy)
            {
                if (h != null) p.health.RemoveHediff(h);
                return;
            }
            tr.OffsetPsyfocusDirectly(-drain);
            if (h == null) p.health.AddHediff(GojoDefOf.Gojo_Speed);
        }

        public override void GameComponentTick()
        {
            if (on.Count == 0 || Find.TickManager.TicksGame % 60 != 0) return;
            foreach (var p in Gojo.ActivePawns())
                if (on.Contains(p.thingIDNumber) && !p.Dead) Pay(p);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            var l = on.ToList();
            Scribe_Collections.Look(ref l, "gojoSpeedOn", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars) on = l == null ? new HashSet<int>() : new HashSet<int>(l);
        }
    }
}
