using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;

namespace SukunaPsycasts
{
    /// <summary>
    /// Which Yuji arrives (her calls 2026-11-12). The stage only sets who he is when he comes; the questline itself
    /// starts from the beginning, except that from the Culling Game on Sukuna has already left him for a stranger
    /// (Megumi, once Megumi is in the mod), so those games start at the hunt.
    ///  1 Start of the series: one finger (Kenjaku's), nothing learned.
    ///  2 Shibuya: 15 fingers inside him, Black Flash and Reinforcement, no cursed technique; the takeover can come.
    ///  3 Culling Game: Sukuna is out of him with the 15; Shrine's imprint, no Blood Manipulation.
    ///  4 Shinjuku: as 3, with all six Death Paintings eaten (Hemosage, Choso's Piercing Blood) and his domain; Choso
    ///    comes with him.
    ///  5 Modulo Yuji: as 4, then the pinnacle (more levels into stats, World Cutting Slash, Piercing Blood Scatter in
    ///    place of Piercing Blood).
    /// The fingers' levels follow the "Yuji's Shrine" setting: from the first finger, or (default) only once Sukuna has
    /// left him, all at once.
    /// </summary>
    public static class YujiStart
    {
        public const int Start = 1, Shibuya = 2, CullingGame = 3, Shinjuku = 4, Modulo = 5;
        public const int Fingers = 15;   // canon: Sukuna's count after Jogo fed him in Shibuya (her pick)

        public static int Setting => Mathf.Clamp(JujutsuFundamentals.JujutsuMod.Settings?.yujiStage ?? Start, Start, Modulo);

        /// <summary>True (default): while Sukuna is inside him, the fingers Yuji eats give him nothing; once Sukuna
        /// leaves, he gets all their levels at once. False: each finger gives its level as it's eaten.</summary>
        public static bool ShrineAfterSukunaLeaves => JujutsuFundamentals.JujutsuMod.Settings?.yujiShrineAfterSukuna ?? true;

        public static void Apply(Pawn yuji, int stage)
        {
            if (yuji == null || stage <= Start) return;
            bool was = CompCursedFinger.Silent, wasP = CompDeathPainting.Silent;
            CompCursedFinger.Silent = true; CompDeathPainting.Silent = true;
            try
            {
                // Kenjaku's sealed finger is already one of them
                for (int i = 1; i < Fingers; i++) CompCursedFinger.Consume(yuji, rollTakeover: false);
                if (stage == Shibuya) return;

                SukunaArc.Instance?.LeaveForStranger(yuji);
                if (stage == CullingGame) return;

                for (int i = 0; i < DeathPaintingCount; i++) CompDeathPainting.Eat(yuji);
                var comp = yuji.GetComp<CompAbilities>();
                // the six paintings teach six of Hemosage's ten; a Shinjuku Yuji has all of it (her pick 2026-11-12)
                var hemo = SukunaArc.Hemosage;
                var psyH = yuji.health.hediffSet.hediffs.OfType<Hediff_PsycastAbilities>().FirstOrDefault();
                if (hemo?.abilities != null && comp != null)
                    foreach (var a in hemo.abilities)
                        if (!comp.HasAbility(a))
                        {
                            comp.GiveAbility(a);
                            if (psyH != null && psyH.points > 0) psyH.SpentPoints(1);   // bought with the paintings' points
                        }
                if (SukunaArc.Masters(yuji)) SukunaArc.GrantBenevolent(yuji);
                if (comp != null && ChosoDefOf.Choso_PiercingBlood != null && !comp.HasAbility(ChosoDefOf.Choso_PiercingBlood))
                    comp.GiveAbility(ChosoDefOf.Choso_PiercingBlood);
                if (stage == Shinjuku) return;

                ModuloYuji.Ascend(yuji);
                if (yuji.ageTracker != null)
                {
                    yuji.ageTracker.AgeBiologicalTicks = (long)(24f * GenDate.TicksPerYear);   // as the dev-menu Modulo Yuji
                    yuji.ageTracker.AgeChronologicalTicks = (long)(83f * GenDate.TicksPerYear);
                }
                comp?.LearnedAbilities.RemoveAll(a => a.def == ChosoDefOf.Choso_PiercingBlood);   // Scatter instead
            }
            finally { CompCursedFinger.Silent = was; CompDeathPainting.Silent = wasP; }
        }

        public static int DeathPaintingCount => PaintingCensus.Total;

        /// <summary>Is Sukuna still inside this Yuji (so his fingers bank instead of growing him)?</summary>
        public static bool SukunaInside(Pawn yuji)
        {
            var arc = SukunaArc.Instance;
            return arc != null && Yuji.Is(yuji) && !ModuloYuji.Is(yuji) && arc.stage == ArcStage.None
                   && yuji.health?.hediffSet?.HasHediff(SukunaDefOf.Sukuna_Vessel) == true;
        }

        /// <summary>Sukuna has left: everything the banked fingers held, at once, spent as fingers spend it.</summary>
        public static void GrantBanked(Pawn yuji)
        {
            var arc = SukunaArc.Instance;
            if (arc == null || yuji == null || yuji.Dead || arc.bankedYujiLevels <= 0) return;
            int n = arc.bankedYujiLevels;
            arc.bankedYujiLevels = 0;
            bool was = CompCursedFinger.Silent;
            CompCursedFinger.Silent = true;
            try
            {
                for (int i = 0; i < n; i++)
                {
                    if (!PsycastSetup.EnsurePsylink(yuji)) break;
                    yuji.ChangePsylinkLevel(1, false);
                    var psy = PsycastSetup.Ensure(yuji);
                    if (psy == null) break;
                    if (!psy.unlockedPaths.Contains(SukunaDefOf.Sukuna_Path)) psy.unlockedPaths.Add(SukunaDefOf.Sukuna_Path);
                    int before = psy.points;
                    psy.ChangeLevel(1);
                    FingerProgression.YujiFinger(yuji, psy, before, false);
                }
            }
            finally { CompCursedFinger.Silent = was; }
            if (yuji.Faction == Faction.OfPlayer)
                Find.LetterStack.ReceiveLetter("Yuji's own power",
                    $"With Sukuna gone from him, everything the fingers held is Yuji's at last: {n} psycast levels, all at once, "
                    + "opening Shrine as far as they reach (World Cutting Slash stays out of reach until he reaches his pinnacle) "
                    + "and making him stronger after that.", LetterDefOf.PositiveEvent, yuji);
        }
    }

    /// <summary>World Cutting Slash isn't Yuji's until he is Modulo Yuji (her call 2026-11-12): his fingers skip it and
    /// it can't be learned with points.</summary>
    public static class YujiNoWorldSlash
    {
        public static bool Blocked(Pawn p, VEF.Abilities.AbilityDef d) =>
            d != null && d == SukunaAbilityDefOf.Sukuna_WorldCuttingSlash && Yuji.Is(p) && !ModuloYuji.Is(p);
    }

    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.PrereqsCompleted), new[] { typeof(Pawn) })]
    public static class Patch_YujiNoWorldSlash
    {
        public static void Postfix(AbilityExtension_Psycast __instance, Pawn pawn, ref bool __result)
        {
            if (__result && SukunaAbilityDefOf.Sukuna_WorldCuttingSlash?.GetModExtension<AbilityExtension_Psycast>() == __instance
                && YujiNoWorldSlash.Blocked(pawn, SukunaAbilityDefOf.Sukuna_WorldCuttingSlash)) __result = false;
        }
    }

    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.PrereqsCompleted), new[] { typeof(CompAbilities) })]
    public static class Patch_YujiNoWorldSlash2
    {
        public static void Postfix(AbilityExtension_Psycast __instance, CompAbilities compAbilities, ref bool __result)
        {
            if (__result && SukunaAbilityDefOf.Sukuna_WorldCuttingSlash?.GetModExtension<AbilityExtension_Psycast>() == __instance
                && YujiNoWorldSlash.Blocked(compAbilities?.Pawn, SukunaAbilityDefOf.Sukuna_WorldCuttingSlash)) __result = false;
        }
    }

    /// <summary>
    /// How strong enemy Sukuna is (her call 2026-11-12, a setting): Weakened 50%, Canon 100% (default, about even with
    /// Gojo), Overwhelming 150%, King of Curses 200%, The Enlightened One 300%. His damage is multiplied by it and the
    /// damage he takes divided by it. Only a Sukuna who isn't the player's.
    /// </summary>
    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class SukunaStrength
    {
        public static readonly float[] Factors = { 0.5f, 1f, 1.5f, 2f, 3f };
        public static readonly string[] Labels = { "Weakened", "Canon", "Overwhelming", "King of Curses", "The Enlightened One" };
        public static int Setting => Mathf.Clamp(JujutsuFundamentals.JujutsuMod.Settings?.sukunaStrength ?? 1, 0, Factors.Length - 1);
        public static float Factor => Factors[Setting];

        public static bool Enemy(Thing t) => t is Pawn p && !p.Dead && SukunaBehaviour.IsSukuna(p) && p.Faction != Faction.OfPlayer;

        [HarmonyPriority(Priority.Low)]
        public static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            float f = Factor;
            if (Mathf.Approximately(f, 1f)) return;
            if (Enemy(dinfo.Instigator) && dinfo.Instigator != __instance) dinfo.SetAmount(dinfo.Amount * f);
            if (Enemy(__instance)) dinfo.SetAmount(dinfo.Amount / f);
        }
    }
}
