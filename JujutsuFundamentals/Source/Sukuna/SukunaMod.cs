using System.Reflection;
using HarmonyLib;
using Verse;
using System.Linq;
using UnityEngine;
using RimWorld;

namespace SukunaPsycasts
{
    [StaticConstructorOnStartup]
    public static class SukunaStartup
    {
        static SukunaStartup()
        {
            // merged into the core mod (2026-11-10): patches and the sorcerer study bonus come from its Startup.
            // Vessels and Yuji count as sorcerers there; their fingers (and Modulo Yuji's x2) scale its techniques too.
            JujutsuFundamentals.Jujutsu.Sorcerers.Add(p => Scaling.HasVessel(p) || Yuji.Is(p));
            JujutsuFundamentals.Jujutsu.PowerFactors.Add(p => Scaling.FingerFactor(p) * (ModuloYuji.Is(p) ? Scaling.ModuloPower : 1f));

            // Their own Black Flash, Reverse Cursed Technique and Reinforcement moved to the shared ones (2026-11-10);
            // what was special about them comes along here.
            // Yuji has Black Flash without learning it, far more often than anyone, and Modulo Yuji's "always" toggle
            JujutsuFundamentals.Jujutsu.BlackFlashInnate.Add(p => Yuji.Is(p));
            // Yuji's abnormal physique IS the superhuman body (her call 2026-11-10), Modulo Yuji's too
            // ... and Sukuna's in his four-armed form, whatever body he is in (her call 2026-11-10)
            JujutsuFundamentals.ZeroCursedEnergy.SuperhumanBody.Add(p => Yuji.Is(p) || ModuloYuji.Is(p) || FourArmed.Is(p));
            JujutsuFundamentals.Jujutsu.BlackFlashChanceOverride.Add(p =>
            {
                if (!Yuji.Is(p)) return -1f;
                if (ModuloYuji.Is(p) && BlackFlashToggle.IsOn(p)) return 1f;
                var props = JujutsuFundamentals.BlackFlashHit.PropsFor(p);
                float own = (props?.baseChance ?? 0.03f) * JujutsuFundamentals.Jujutsu.Power(p);
                float c = Mathf.Clamp(YujiBlackFlash.YujiBase + 3f * own, 0f, YujiBlackFlash.YujiMax);
                return Mathf.Clamp01(c * JujutsuFundamentals.BlackFlashRules.Multiplier
                                     * JujutsuFundamentals.BlackFlashRules.LevelFactor.Evaluate(JujutsuFundamentals.BlackFlashRules.PsycastLevel(p))
                                     * (JujutsuFundamentals.BlackFlashFx.InZone(p) ? JujutsuFundamentals.BlackFlashFx.ZoneChance : 1f));
            });
            JujutsuFundamentals.BlackFlashHit.Landed.Add((attacker, target) =>
            {
                SlashUtility.BlackSparks(target);
                if (target is Pawn victim) BlackFlashLog.Mark(victim);
            });
            // Yuji's reverse cursed technique heals far faster; his two lost fingers never come back
            JujutsuFundamentals.Jujutsu.RctHealFactors.Add(YujiRct.HealFactor);
            JujutsuFundamentals.Jujutsu.RctKeepsLost.Add((p, part) => Yuji.Is(p) && part.customLabel != null && RctRegrow.YujiKeepsLost.Contains(part.customLabel));
            JujutsuFundamentals.Jujutsu.RctAfterRegrow.Add(p => { if (Yuji.Is(p)) foreach (var l in RctRegrow.YujiKeepsLost) ModuloYuji.RemoveFinger(p, l); });
            // Yuji's blood and the blood poisoning make healing cost more, as other poisons do
            JujutsuFundamentals.Jujutsu.Poisoned.Add(p =>
                (PoisonBlood.Def != null && p.health.hediffSet.HasHediff(PoisonBlood.Def))
                || (SukunaArcDefOf.Sukuna_BloodPoison != null && p.health.hediffSet.HasHediff(SukunaArcDefOf.Sukuna_BloodPoison)));
            // only Sukuna keeps his cursed technique with Domain Amplification on (her call 2026-11-10)
            JujutsuFundamentals.Jujutsu.AmplificationExempt.Add(SukunaBehaviour.IsSukuna);
            // calamity grades: Sukuna and Modulo Yuji can open their domain more than once in a row (her call 2026-11-10)
            JujutsuFundamentals.Domains.Calamity.Add(p => SukunaBehaviour.IsSukuna(p) || ModuloYuji.Is(p));
            // once Furnace is known, Reinforcement tempers the body against heat too (as his own Reinforcement did)
            JujutsuFundamentals.Jujutsu.ReinforcementExtras.Add((p, k, offsets, factors) =>
            {
                var comp = p.GetComp<VEF.Abilities.CompAbilities>();
                if (comp != null && SukunaAbilityDefOf.Sukuna_FireArrow != null && comp.HasAbility(SukunaAbilityDefOf.Sukuna_FireArrow))
                {
                    var stat = DefDatabase<StatDef>.GetNamedSilentFail("ComfyTemperatureMax");
                    if (stat != null) offsets.Add(new StatModifier { stat = stat, value = 60f });
                }
            });
        }
    }
}
