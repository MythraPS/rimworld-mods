using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Yuji's Black Flash (her call 2026-11-02): much likelier than Sukuna's or Gojo's, and once he is
    /// Modulo Yuji a toggle that makes every hit one.
    /// </summary>
    public static class YujiBlackFlash
    {
        /// <summary>Sukuna's is 6% x power and Gojo's 12% x power. Yuji's starts at 40%.</summary>
        public const float YujiBase = 0.40f, YujiMax = 0.85f;

        private static readonly Dictionary<int, HediffComp_BlackFlash> spare = new Dictionary<int, HediffComp_BlackFlash>();

        /// <summary>A stand-in comp with the def's numbers, for a Yuji who never learned it.</summary>
        public static HediffComp_BlackFlash Comp(Pawn p)
        {
            if (spare.TryGetValue(p.thingIDNumber, out var c) && c.parent?.pawn == p) return c;
            var props = SukunaDefOf.Sukuna_BlackFlash?.CompProps<HediffCompProperties_BlackFlash>();
            if (props == null) return null;
            var h = (HediffWithComps)HediffMaker.MakeHediff(SukunaDefOf.Sukuna_BlackFlash, p);
            c = h.TryGetComp<HediffComp_BlackFlash>();
            if (c != null) spare[p.thingIDNumber] = c;
            return c;
        }

        public static float Chance(Pawn p, HediffComp_BlackFlash comp)
        {
            if (!Yuji.Is(p)) return comp.ChanceFor(p);
            if (ModuloYuji.Is(p) && BlackFlashToggle.IsOn(p)) return 1f;
            return Mathf.Clamp(YujiBase + 3f * comp.ChanceFor(p), 0f, YujiMax);
        }
    }

    public class BlackFlashToggle : GameComponent
    {
        private static HashSet<int> on = new HashSet<int>();
        public BlackFlashToggle(Game game) { on = new HashSet<int>(); }

        public static bool IsOn(Pawn p) => p != null && on.Contains(p.thingIDNumber);

        public static void Set(Pawn p, bool value)
        {
            if (p == null) return;
            if (value) on.Add(p.thingIDNumber); else on.Remove(p.thingIDNumber);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            var list = new List<int>(on);
            Scribe_Collections.Look(ref list, "sukunaBlackFlashOn", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                on = list == null ? new HashSet<int>() : new HashSet<int>(list);
        }
    }

    /// <summary>
    /// Yuji's blood (her call 2026-11-02): a deep hemogen pool, psyfocus that keeps coming back, and
    /// buttons to turn one into the other.
    /// </summary>
    public static class YujiBlood
    {
        public const float HemogenMax = 5f;          // 500 hemogen
        public const float Step = 0.25f;             // per click: 25 hemogen <-> 25% psyfocus
        public const float PsyfocusPerSecond = 0.005f;

        public static Gene_Hemogen Hemogen(Pawn p) => p?.genes?.GetFirstGeneOfType<Gene_Hemogen>();

        /// <summary>Every 250 ticks, from Yuji's physique.</summary>
        public static void Maintain(Pawn p)
        {
            var g = Hemogen(p);
            if (g != null && g.Max < HemogenMax)
            {
                bool wasFull = g.Value >= g.Max - 0.001f;
                g.SetMax(HemogenMax);
                if (wasFull) g.Value = g.Max;
            }
            p.psychicEntropy?.OffsetPsyfocusDirectly(PsyfocusPerSecond * 250f / 60f);
        }

        public static IEnumerable<Gizmo> Gizmos(Pawn p)
        {
            var g = Hemogen(p);
            var psy = p.psychicEntropy;
            if (g == null || psy == null) yield break;

            var toFocus = new Command_Action
            {
                defaultLabel = "Blood to focus",
                defaultDesc = $"Burn {Step * 100f:0} hemogen for {Step * 100f:0}% psyfocus.",
                icon = ContentFinder<Texture2D>.Get("Abilities/Sukuna/BloodToFocus", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    g.Value -= Step;
                    psy.OffsetPsyfocusDirectly(Step);
                },
            };
            if (g.Value < Step) toFocus.Disable("Not enough hemogen.");
            else if (psy.CurrentPsyfocus >= 0.999f) toFocus.Disable("Psyfocus is already full.");
            yield return toFocus;

            var toBlood = new Command_Action
            {
                defaultLabel = "Focus to blood",
                defaultDesc = $"Spend {Step * 100f:0}% psyfocus for {Step * 100f:0} hemogen.",
                icon = ContentFinder<Texture2D>.Get("Abilities/Sukuna/FocusToBlood", false) ?? BaseContent.BadTex,
                action = () =>
                {
                    psy.OffsetPsyfocusDirectly(-Step);
                    g.Value += Step;
                },
            };
            if (psy.CurrentPsyfocus < Step) toBlood.Disable("Not enough psyfocus.");
            else if (g.Value >= g.Max - 0.001f) toBlood.Disable("Hemogen is already full.");
            yield return toBlood;
        }
    }

    /// <summary>Keeps the fire bow pointed at the target for anyone who can draw it without holding fingers
    /// (Yuji, Modulo Yuji, anyone with the Shrine imprint). Vessels get it from HediffComp_VesselPassives.</summary>
    public class HediffComp_AimBow : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!Scaling.HasVessel(Pawn)) WarmupMoteAim.Aim(Pawn);
        }
    }

    public class HediffComp_YujiBlood : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!Scaling.HasVessel(Pawn)) WarmupMoteAim.Aim(Pawn);
            if (Pawn.IsHashIntervalTick(250, delta) && !Pawn.Dead) YujiBlood.Maintain(Pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    public static class Patch_YujiGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Pawn __instance)
        {
            foreach (var g in values) yield return g;
            var p = __instance;
            if (p?.Faction == null || !p.Faction.IsPlayer || !Yuji.Is(p) || p.Dead) yield break;

            foreach (var g in YujiBlood.Gizmos(p)) yield return g;

            if (ModuloYuji.Is(p))
                yield return new Command_Toggle
                {
                    defaultLabel = "Black Flash: every hit",
                    defaultDesc = "On: every hit Yuji lands is a Black Flash. Off: they come as often as usual for him.",
                    icon = ContentFinder<Texture2D>.Get("Abilities/Sukuna/BlackFlash", false) ?? BaseContent.BadTex,
                    isActive = () => BlackFlashToggle.IsOn(p),
                    toggleAction = () => BlackFlashToggle.Set(p, !BlackFlashToggle.IsOn(p)),
                };
        }
    }
}
