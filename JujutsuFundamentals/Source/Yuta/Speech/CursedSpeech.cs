using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class SpeechDefOf
    {
        public static HediffDef JJK_ThroatStrain;
        public static HediffDef JJK_VoiceLost;
        public static HediffDef JJK_CS_Asleep;
        public static IncidentDef JJK_InumakiJoins;
        static SpeechDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SpeechDefOf));
    }

    /// <summary>
    /// The balance rules (canon and her calls 2026-11-11). All numbers are proposals.
    ///
    /// How much a command does: the stronger the listener is next to the speaker, the less.
    ///   speaker strength = Power x (1 + psycast level / 20)
    ///   target strength  = body size (at least 0.3) x (1 + psycast level / 10) x (sorcerers only: their Power, at least 1)
    ///   ratio r          = speaker / target
    ///   effect           = r held between 0.2 and 2, times the listener's Hearing (held 0..1; deaf = immune)
    ///   weak             = r x Hearing of 2.5 or more
    /// Inumaki (level 10, sensitivity 100%) is 1.5; an ordinary person is 1, so r = 1.5; a thrumbo r = 0.4; a squirrel r = 5.
    ///
    /// What a command costs (her rule): the more psychically sensitive and the stronger the listener, the more
    /// psyfocus and neural heat. Each listener weighs
    ///   W = target strength x (non-sorcerers: their psychic sensitivity, at least 0.25)
    /// (a sorcerer's sensitivity is already in their Power). An ordinary person is W 1 and costs the base: the tree's
    /// psyfocus and the command's HeatBase. Each listener heavier than that adds base x (W - 1) psyfocus and
    /// HeatBase x (W - 1) heat. A single-target command can't be aimed at someone the speaker can't pay for; an area
    /// command reaches the lightest listeners first and leaves out the ones it can't pay for. Yuta pays too.
    ///
    /// Backlash (throat strain) is separate, and Yuta never takes it (canon).
    /// </summary>
    public static class CursedSpeech
    {
        public const float MinEffect = 0.2f, MaxEffect = 2f, WeakRatio = 2.5f, MinSensitivityWeight = 0.25f;

        public static int Level(Pawn p)
        {
            var set = p?.health?.hediffSet;
            if (set == null) return 0;
            foreach (var h in set.hediffs)
                if (h is VanillaPsycastsExpanded.Hediff_PsycastAbilities psy) return Mathf.Max(0, psy.level);
            return p.GetPsylinkLevel();
        }

        public static float SpeakerStrength(Pawn s) => Mathf.Max(0.05f, Jujutsu.Power(s)) * (1f + Level(s) / 20f);

        public static float TargetStrength(Pawn t)
        {
            float v = Mathf.Max(0.3f, t.BodySize) * (1f + Level(t) / 10f);
            if (Jujutsu.IsSorcerer(t)) v *= Mathf.Max(1f, Jujutsu.Power(t));
            return v;
        }

        /// <summary>How heavy a listener is to command (the cost weight W).</summary>
        public static float Weight(Pawn t) =>
            TargetStrength(t) * (Jujutsu.IsSorcerer(t) ? 1f : Mathf.Max(MinSensitivityWeight, Jujutsu.Sensitivity(t)));

        public static float Hearing(Pawn t)
        {
            var caps = t?.health?.capacities;
            return caps == null ? 1f : Mathf.Clamp01(caps.GetLevel(PawnCapacityDefOf.Hearing));
        }

        public static bool Hears(Pawn t) => Hearing(t) > 0.01f;

        public static float Ratio(Pawn speaker, Pawn target) => SpeakerStrength(speaker) / Mathf.Max(0.05f, TargetStrength(target));
        /// <summary>The multiplier on durations and damage: r held between 0.2 and 2, times Hearing.</summary>
        public static float Effect(Pawn speaker, Pawn target) =>
            Mathf.Clamp(Ratio(speaker, target), MinEffect, MaxEffect) * Hearing(target);
        public static bool Weak(Pawn speaker, Pawn target) => Ratio(speaker, target) * Hearing(target) >= WeakRatio;

        /// <summary>Yuta's copy has no backlash (canon). Everyone else speaks by Inumaki's rule.</summary>
        public static bool TakesBacklash(Pawn speaker) => speaker != null && !Yuta.Is(speaker);

        /// <summary>Throat strain: the command's base cost, times 1/r held between 0.5 and 4, so commands on stronger
        /// targets hurt more. At full strain the voice goes (JJK_VoiceLost).</summary>
        public static void Strain(Pawn speaker, float baseCost, float ratio)
        {
            if (!TakesBacklash(speaker) || speaker.health == null) return;
            float add = baseCost * Mathf.Clamp(1f / Mathf.Max(0.01f, ratio), 0.5f, 4f);
            var h = speaker.health.hediffSet.GetFirstHediffOfDef(SpeechDefOf.JJK_ThroatStrain);
            if (h == null) { h = speaker.health.AddHediff(SpeechDefOf.JJK_ThroatStrain); h.Severity = add; }
            else h.Severity += add;
            if (h.Severity >= 0.999f && !speaker.health.hediffSet.HasHediff(SpeechDefOf.JJK_VoiceLost))
            {
                speaker.health.AddHediff(SpeechDefOf.JJK_VoiceLost);
                if (speaker.Faction == Faction.OfPlayer)
                    Messages.Message($"{speaker.LabelShortCap} has strained their throat too far and lost their voice for a while.",
                                     speaker, MessageTypeDefOf.NegativeEvent, false);
            }
        }

        /// <summary>Who an area command can reach: enemies of the speaker who can hear, not inside an Evening Moon
        /// circle (Protection.Parried; damage, stuns and harmful hediffs are also blocked by
        /// PsycastContext, but flee jobs, throws and outright kills are not, so it is checked here).</summary>
        public static bool Affects(Pawn speaker, Pawn victim) =>
            victim != null && victim != speaker && !victim.Dead && victim.Spawned
            && (victim.HostileTo(speaker) || (SureHit.Active && Domains.Hits(speaker, victim)))   // a domain sure hit reaches non-allies too
            && !Protection.Parried(victim, speaker) && Hears(victim);

        public static void Say(Pawn speaker, string words)
        {
            if (speaker?.Map == null) return;
            MoteMaker.ThrowText(speaker.DrawPos + new Vector3(0f, 0f, 0.6f), speaker.Map, words, new Color(0.85f, 0.8f, 1f), 2.5f);
        }

        private static StatDef costFactor;
        public static float CostFactor(Pawn p)
        {
            costFactor = costFactor ?? DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor");
            return costFactor == null ? 1f : Mathf.Max(0f, p.GetStatValue(costFactor));
        }
    }

    /// <summary>A Cursed Speech command. Every command needs a voice (lost to strain, or Talking under 30%), and pays
    /// the listener-weighted psyfocus and neural heat described on CursedSpeech.</summary>
    public abstract class Ability_CursedSpeech : Ability
    {
        public const float MinTalking = 0.3f;

        /// <summary>Throat strain at r = 1 (proposal).</summary>
        protected abstract float StrainCost { get; }
        /// <summary>Neural heat for one ordinary listener (proposal).</summary>
        protected abstract float HeatBase { get; }
        protected abstract string Words { get; }
        /// <summary>Aimed at one pawn (Stop, Sleep, Get Crushed, Explode).</summary>
        protected virtual bool SingleTarget => false;

        protected float BasePsyfocus =>
            def.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>()?.psyfocusCost ?? 0f;

        /// <summary>Psyfocus a listener of weight W costs on top of what VPE already charges.</summary>
        protected float ExtraPsyfocus(float w) => BasePsyfocus * Mathf.Max(0f, w - 1f) * CursedSpeech.CostFactor(pawn);
        protected float Heat(float w) => HeatBase * Mathf.Max(0f, w - 1f);

        private float HeatGain => pawn.GetStatValue(StatDefOf.PsychicEntropyGain);

        protected bool CanPay(float psyfocus, float heat)
        {
            var tr = pawn.psychicEntropy;
            if (tr == null) return true;
            return tr.CurrentPsyfocus + 0.0001f >= psyfocus && !tr.WouldOverflowEntropy(heat * HeatGain);
        }

        protected void Pay(float psyfocus, float heat)
        {
            var tr = pawn.psychicEntropy;
            if (tr == null) return;
            if (psyfocus > 0f) tr.OffsetPsyfocusDirectly(-psyfocus);
            if (heat > 0f) tr.TryAddEntropy(heat, pawn, scale: true, overLimit: false);
        }

        public override bool IsEnabledForPawn(out string reason)
        {
            if (pawn?.health?.capacities != null)
            {
                if (pawn.health.hediffSet.HasHediff(SpeechDefOf.JJK_VoiceLost)) { reason = "Lost their voice to throat strain."; return false; }
                if (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking) < MinTalking) { reason = "Can't speak."; return false; }
            }
            if (pawn?.psychicEntropy != null && pawn.psychicEntropy.WouldOverflowEntropy(HeatBase * HeatGain))
            { reason = "Neural heat too high to give a command."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        /// <summary>Single-target commands: refuse a listener who can't hear, or one the speaker can't pay for.</summary>
        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            var t = target.Thing as Pawn;
            if (!SingleTarget || t == null || t == pawn) return base.ValidateTarget(target, showMessages);
            string why = null;
            if (!CursedSpeech.Hears(t)) why = $"{t.LabelShortCap} can't hear: Cursed Speech does nothing to them.";
            else
            {
                float w = CursedSpeech.Weight(t);
                float psy = BasePsyfocus * CursedSpeech.CostFactor(pawn) + ExtraPsyfocus(w), heat = HeatBase + Heat(w);
                if (!CanPay(psy, heat))
                    why = $"{t.LabelShortCap} is too strong to command: needs {psy:P0} psyfocus and {heat * HeatGain:0} neural heat.";
            }
            if (why != null)
            {
                if (showMessages) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (pawn?.Map == null) return;
            CursedSpeech.Say(pawn, Words);
            Pay(0f, HeatBase);
            float lowest = Command(targets);
            CursedSpeech.Strain(pawn, StrainCost, lowest);
        }

        /// <summary>Carries out the command; returns the lowest ratio among those it reached (1 if nobody).</summary>
        protected abstract float Command(GlobalTargetInfo[] targets);

        /// <summary>The single target, if it can hear, isn't shielded, and the speaker can still pay its extra cost
        /// (which is paid here).</summary>
        protected Pawn Listener(GlobalTargetInfo[] targets)
        {
            if (targets == null || targets.Length == 0) return null;
            var p = targets[0].Thing as Pawn;
            if (p == null || p.Dead || !p.Spawned || p == pawn || !CursedSpeech.Hears(p)) return null;
            if (p.HostileTo(pawn) && Protection.Parried(p, pawn)) return null;
            float w = CursedSpeech.Weight(p), psy = ExtraPsyfocus(w), heat = Heat(w);
            if (!CanPay(psy, heat))
            {
                Messages.Message($"{pawn.LabelShortCap} couldn't put enough into the command to move {p.LabelShort}.", pawn, MessageTypeDefOf.RejectInput, false);
                return null;
            }
            Pay(psy, heat);
            return p;
        }

        /// <summary>Area commands: the enemies in range who can hear, lightest first, as many as the speaker can pay for.</summary>
        protected List<Pawn> Listeners(IntVec3 center, float radius)
        {
            var list = new List<Pawn>();
            var map = pawn.Map;
            if (map == null) return list;
            var all = map.mapPawns.AllPawnsSpawned
                .Where(p => p.Position.InHorDistOf(center, radius) && CursedSpeech.Affects(pawn, p))
                .Select(p => new { p, w = CursedSpeech.Weight(p) }).OrderBy(x => x.w).ToList();
            int left = 0;
            foreach (var x in all)
            {
                float psy = ExtraPsyfocus(x.w), heat = Heat(x.w);
                if (!CanPay(psy, heat)) { left++; continue; }
                Pay(psy, heat);
                list.Add(x.p);
            }
            if (left > 0 && pawn.Faction == Faction.OfPlayer)
                Messages.Message($"{left} of them were too strong for what {pawn.LabelShort} had left to put into the command.", pawn, MessageTypeDefOf.NeutralEvent, false);
            return list;
        }

        protected float Radius => def.radius > 0f ? def.radius : 5.9f;
        protected float Effect(Pawn t) => CursedSpeech.Effect(pawn, t);
    }

    /// <summary>Stop: one target freezes, 2 seconds x the effect (proposal).</summary>
    public class Ability_CS_Stop : Ability_CursedSpeech
    {
        protected override bool SingleTarget => true;
        public const int Ticks = 120;
        protected override float StrainCost => 0.05f;
        protected override float HeatBase => 3f;
        protected override string Words => "\"Stop.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var t = Listener(targets);
            if (t == null) return 1f;
            t.stances?.stunner?.StunFor(Mathf.RoundToInt(Ticks * Effect(t)), pawn, false);
            return CursedSpeech.Ratio(pawn, t);
        }
    }

    /// <summary>Run Away: every enemy near the speaker flees, 4 seconds x the effect (proposal).</summary>
    public class Ability_CS_RunAway : Ability_CursedSpeech
    {
        public const int Ticks = 240;
        public const float FleeDistance = 16f;
        protected override float StrainCost => 0.1f;
        protected override float HeatBase => 5f;
        protected override string Words => "\"Run away.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            float lowest = float.MaxValue;
            var threats = new List<Thing> { pawn };
            foreach (var t in Listeners(pawn.Position, Radius))
            {
                lowest = Mathf.Min(lowest, CursedSpeech.Ratio(pawn, t));
                if (t.Downed || t.jobs == null) continue;
                var dest = CellFinderLoose.GetFleeDest(t, threats, FleeDistance);
                if (!dest.IsValid || dest == t.Position) continue;
                var job = JobMaker.MakeJob(JobDefOf.Flee, dest, pawn);
                job.expiryInterval = Mathf.Max(1, Mathf.RoundToInt(Ticks * Effect(t)));
                job.checkOverrideOnExpire = true;
                t.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            return lowest == float.MaxValue ? 1f : lowest;
        }
    }

    /// <summary>Don't Move: every enemy in the circle freezes, 4 seconds x the effect, so stronger ones break free sooner (proposal).</summary>
    public class Ability_CS_DontMove : Ability_CursedSpeech
    {
        public const int Ticks = 240;
        protected override float StrainCost => 0.15f;
        protected override float HeatBase => 8f;
        protected override string Words => "\"Don't move.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            if (targets == null || targets.Length == 0) return 1f;
            float lowest = float.MaxValue;
            foreach (var t in Listeners(targets[0].Cell, Radius))
            {
                lowest = Mathf.Min(lowest, CursedSpeech.Ratio(pawn, t));
                t.stances?.stunner?.StunFor(Mathf.RoundToInt(Ticks * Effect(t)), pawn, false);
            }
            return lowest == float.MaxValue ? 1f : lowest;
        }
    }

    /// <summary>Blast Away: enemies around the speaker are thrown 6 cells x the square root of the effect, and take
    /// 10 blunt damage x the effect (proposals).</summary>
    public class Ability_CS_BlastAway : Ability_CursedSpeech
    {
        public const float Distance = 6f, Damage = 10f;
        protected override float StrainCost => 0.15f;
        protected override float HeatBase => 8f;
        protected override string Words => "\"Blast away.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var map = pawn.Map;
            float lowest = float.MaxValue;
            FleckMaker.Static(pawn.Position, map, FleckDefOf.PsycastAreaEffect, Radius);
            foreach (var t in Listeners(pawn.Position, Radius))
            {
                float e = Effect(t);
                lowest = Mathf.Min(lowest, CursedSpeech.Ratio(pawn, t));
                t.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Damage * e, 0.2f, -1f, pawn));
                if (t.Dead || !t.Spawned) continue;
                var dir = (t.Position - pawn.Position).ToVector3();
                if (dir.sqrMagnitude < 0.01f) dir = Rand.InsideUnitCircleVec3;
                GojoPsycasts.Fx.Throw(t, GojoPsycasts.Fx.Along(t.Position, dir, Distance * Mathf.Sqrt(e), map));
            }
            return lowest == float.MaxValue ? 1f : lowest;
        }
    }

    /// <summary>Sleep: one target falls unconscious for 3 hours x the effect (proposal: 36 minutes to 6 hours), until
    /// they're hurt (HediffComp_WakeWhenHurt, her call 2026-11-11).</summary>
    public class Ability_CS_Sleep : Ability_CursedSpeech
    {
        protected override bool SingleTarget => true;
        public const int Ticks = 3 * GenDate.TicksPerHour;
        protected override float StrainCost => 0.2f;
        protected override float HeatBase => 10f;
        protected override string Words => "\"Sleep.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var t = Listener(targets);
            if (t == null) return 1f;
            var h = t.health.hediffSet.GetFirstHediffOfDef(SpeechDefOf.JJK_CS_Asleep) ?? t.health.AddHediff(SpeechDefOf.JJK_CS_Asleep);
            var gone = (h as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();
            if (gone != null) gone.ticksToDisappear = Mathf.Max(gone.ticksToDisappear, Mathf.RoundToInt(Ticks * Effect(t)));
            if (t.HostileTo(pawn)) GojoPsycasts.Fx.NotifyAttacked(t, pawn);
            return CursedSpeech.Ratio(pawn, t);
        }
    }

    /// <summary>Get Crushed: 30 blunt damage x the effect, armour penetration 40% (proposals).</summary>
    public class Ability_CS_GetCrushed : Ability_CursedSpeech
    {
        protected override bool SingleTarget => true;
        public const float Damage = 30f;
        protected override float StrainCost => 0.25f;
        protected override float HeatBase => 12f;
        protected override string Words => "\"Get crushed.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var t = Listener(targets);
            if (t == null) return 1f;
            FleckMaker.ThrowDustPuffThick(t.DrawPos, t.Map, 1.8f, new Color(0.6f, 0.55f, 0.65f));
            t.TakeDamage(new DamageInfo(DamageDefOf.Blunt, Damage * Effect(t), 0.4f, -1f, pawn));
            return CursedSpeech.Ratio(pawn, t);
        }
    }

    /// <summary>Explode: 45 bomb damage x the effect, armour penetration 50%; a weak target is destroyed outright (proposals).</summary>
    public class Ability_CS_Explode : Ability_CursedSpeech
    {
        protected override bool SingleTarget => true;
        public const float Damage = 45f;
        protected override float StrainCost => 0.35f;
        protected override float HeatBase => 18f;
        protected override string Words => "\"Explode.\"";

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var t = Listener(targets);
            if (t == null) return 1f;
            var map = t.Map;
            FleckMaker.Static(t.Position, map, FleckDefOf.ExplosionFlash, 3f);
            FleckMaker.ThrowDustPuffThick(t.DrawPos, map, 2.2f, new Color(0.35f, 0.3f, 0.35f));
            float r = CursedSpeech.Ratio(pawn, t);
            Burst(t, map, CursedSpeech.Weak(pawn, t));
            if (CursedSpeech.Weak(pawn, t))
            {
                // her call 2026-11-11: a weak target bursts apart, no body left (their gear drops)
                var at = t.Position;
                t.Kill(new DamageInfo(DamageDefOf.Bomb, 9999f, 1f, -1f, pawn));
                var corpse = t.Corpse;
                if (corpse != null && !corpse.Destroyed)
                {
                    if (corpse.Spawned) corpse.Strip();
                    corpse.Destroy();
                }
            }
            else t.TakeDamage(new DamageInfo(DamageDefOf.Bomb, Damage * Effect(t), 0.5f, -1f, pawn));
            return r;
        }

        /// <summary>The blast itself: a flash, the bang, and (when they burst) their blood thrown all around.</summary>
        private static void Burst(Pawn t, Map map, bool apart)
        {
            var at = t.Position;
            FleckMaker.Static(at, map, FleckDefOf.ExplosionFlash, apart ? 6f : 4f);
            for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(t.DrawPos, map);
            FleckMaker.ThrowSmoke(t.DrawPos, map, 1.5f);
            DefDatabase<SoundDef>.GetNamedSilentFail("Explosion_Bomb")?.PlayOneShot(new TargetInfo(at, map));
            var blood = t.RaceProps?.BloodDef;
            if (blood == null) return;
            int n = apart ? 12 : 4;
            foreach (var c in GenRadial.RadialCellsAround(at, apart ? 2.5f : 1.5f, true).InRandomOrder().Take(n))
                if (c.InBounds(map) && c.Walkable(map)) FilthMaker.TryMakeFilth(c, map, blood, apart ? 2 : 1);
            if (apart)
                for (int i = 0; i < 3; i++)
                    FleckMaker.ThrowDustPuffThick(t.DrawPos + new Vector3(Rand.Range(-0.8f, 0.8f), 0f, Rand.Range(-0.8f, 0.8f)), map, 1.8f, new Color(0.55f, 0.05f, 0.05f));
        }
    }

    /// <summary>Die (Yuta, with Rika fully manifested, or Inumaki): every enemy around the speaker who hears it. Anomaly entities and
    /// mutants weaker than him (r x Hearing 1 or more) die outright; anyone else takes 40 cursed energy damage x the
    /// effect, armour penetration 60% (proposals).</summary>
    public class Ability_CS_Die : Ability_CursedSpeech
    {
        public const float Damage = 40f;
        protected override float StrainCost => 0.5f;
        protected override float HeatBase => 25f;
        protected override string Words => "\"Die.\"";

        /// <summary>Set while a domain katana releases Die (her call 2026-11-12): it reaches the whole domain, once.</summary>
        public static IntVec3? domainCenter;
        public static float domainRadius;

        public override bool IsEnabledForPawn(out string reason)
        {
            // her call 2026-11-11: Inumaki has it too; only Yuta needs Rika out for it
            if (Yuta.Is(pawn) && !Rika.Manifested(pawn)) { reason = "Needs Rika fully manifested."; return false; }
            return base.IsEnabledForPawn(out reason);
        }

        protected override float Command(GlobalTargetInfo[] targets)
        {
            var map = pawn.Map;
            float lowest = float.MaxValue;
            var dmgDef = DefDatabase<DamageDef>.GetNamedSilentFail("JJK_CursedEnergy") ?? DamageDefOf.Blunt;
            var center = domainCenter ?? pawn.Position;
            float radius = domainCenter.HasValue ? domainRadius : Radius;
            FleckMaker.Static(center, map, FleckDefOf.PsycastAreaEffect, Mathf.Min(radius, 20f));
            foreach (var t in Listeners(center, radius))
            {
                float r = CursedSpeech.Ratio(pawn, t);
                lowest = Mathf.Min(lowest, r);
                FleckMaker.ThrowLightningGlow(t.DrawPos, map, 1.5f);
                if (Patch_RctVsAnomalies.IsAnomaly(t) && r * CursedSpeech.Hearing(t) >= 1f) t.Kill(new DamageInfo(dmgDef, 9999f, 1f, -1f, pawn));
                else t.TakeDamage(new DamageInfo(dmgDef, Damage * Effect(t), 0.6f, -1f, pawn));
            }
            return lowest == float.MaxValue ? 1f : lowest;
        }
    }

    /// <summary>Commanded sleep ends when the sleeper is hurt (her call 2026-11-11).</summary>
    public class HediffCompProperties_WakeWhenHurt : HediffCompProperties
    {
        public HediffCompProperties_WakeWhenHurt() => compClass = typeof(HediffComp_WakeWhenHurt);
    }

    public class HediffComp_WakeWhenHurt : HediffComp
    {
        private bool woken;

        public override bool CompShouldRemove => woken || base.CompShouldRemove;

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0f) woken = true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref woken, "jjkWoken");
        }
    }
}
