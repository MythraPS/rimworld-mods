using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace SukunaPsycasts
{
    public class CompProperties_Kamutoke : CompProperties
    {
        public float boltDamage            = 32f;
        public float boltArmorPenetration  = 0.90f;
        public int   chainTargets          = 3;
        public float chainRadius           = 6.9f;
        public float psyfocusCost          = 0.08f;
        public int   cooldownTicks         = 600;
        public float mechanoidDamageFactor = 2.0f;

        public CompProperties_Kamutoke() => compClass = typeof(CompKamutoke);
    }

    /// <summary>
    /// Back from the Cursed Tools set-aside 2026-11-10. Below psycast level 10 the lightning button
    /// is not offered, and the weapon is an ordinary blade.
    /// </summary>
    public class CompKamutoke : ThingComp, JujutsuFundamentals.IWeaponGizmos
    {
        private int cooldownUntil;

        public CompProperties_Kamutoke Props => (CompProperties_Kamutoke)props;

        public const int MinLevel = 10;

        private Pawn Holder => (parent.ParentHolder as Pawn_EquipmentTracker)?.pawn;

        public IEnumerable<Gizmo> Gizmos(Pawn holder)
        {
            var pawn = holder;
            // anyone at psycast level 10 or above (her call 2026-11-10; was Sukuna only)
            if (pawn == null || JujutsuFundamentals.BlackFlashRules.PsycastLevel(pawn) < MinLevel) yield break;

            var cmd = new Command_Action
            {
                defaultLabel = "Kamutoke",
                defaultDesc  = "Call the storm folded up inside the blade. Chains to nearby targets, "
                             + "and is brutal against mechanoids.",
                icon = parent.def.uiIcon,
                action = () => Strike(pawn)
            };
            if (Find.TickManager.TicksGame < cooldownUntil)
                cmd.Disable("Recharging.");
            else if (pawn.psychicEntropy == null
                     || pawn.psychicEntropy.CurrentPsyfocus < Props.psyfocusCost)
                cmd.Disable("Not enough psyfocus.");
            yield return cmd;
        }

        /// <summary>For an enemy holder (the lair's Sukuna): strike when it's ready and anyone hostile is in reach.</summary>
        public bool TryStrikeForAI(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.Downed || Find.TickManager.TicksGame < cooldownUntil) return false;
            if (JujutsuFundamentals.BlackFlashRules.PsycastLevel(pawn) < MinLevel) return false;
            if (pawn.psychicEntropy == null || pawn.psychicEntropy.CurrentPsyfocus < Props.psyfocusCost) return false;
            bool any = GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.chainRadius, true)
                .Any(t => t is Pawn o && o != pawn && !o.Dead && !o.Downed && o.HostileTo(pawn));
            if (!any) return false;
            Strike(pawn);
            return true;
        }

        private void Strike(Pawn pawn)
        {
            if (pawn.Map == null) return;
            var hit = new List<Pawn>();
            foreach (var t in GenRadial.RadialDistinctThingsAround(
                         pawn.Position, pawn.Map, Props.chainRadius, true))
            {
                if (!(t is Pawn other) || other == pawn || other.Dead) continue;
                if (!other.HostileTo(pawn)) continue;
                hit.Add(other);
                if (hit.Count >= Props.chainTargets) break;
            }
            if (hit.Count == 0) return;

            foreach (var target in hit)
            {
                float dmg = Scaling.Damage(pawn, Props.boltDamage);
                if (target.RaceProps.IsMechanoid) dmg *= Props.mechanoidDamageFactor;
                var dinfo = new DamageInfo(DamageDefOf.Stun, 0f, 0f, -1f, pawn);
                target.TakeDamage(dinfo);
                target.TakeDamage(new DamageInfo(
                    DamageDefOf.Burn, dmg, Props.boltArmorPenetration, -1f, pawn));
                FleckMaker.Static(target.Position, target.Map, FleckDefOf.ExplosionFlash, 2f);
            }

            pawn.psychicEntropy?.OffsetPsyfocusDirectly(-Props.psyfocusCost);
            cooldownUntil = Find.TickManager.TicksGame + Props.cooldownTicks;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref cooldownUntil, "sukunaKamutokeCooldown", 0);
        }
    }
}
