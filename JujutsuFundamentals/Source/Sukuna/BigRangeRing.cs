using System;
using HarmonyLib;
using UnityEngine;
using Verse;
using VEFAbility = VEF.Abilities.Ability;

namespace SukunaPsycasts
{
    /// <summary>
    /// Lag while aiming (her report 2026-11-08, Fire Arrow "when first cast"). VEF draws an ability's
    /// range as a cell ring every frame while its button is hovered or it is being aimed, and the
    /// game's ring lists and edges every cell inside it: Modulo Yuji's Fire Arrow range (32.9 x
    /// sensitivity, about 148) meant ~69,000 cells a frame. Past 40 cells, only for ability rings,
    /// a smooth outline (at most 48 line segments) is drawn instead.
    /// </summary>
    public static class BigRangeRing
    {
        public const float Threshold = 40f;
        [ThreadStatic] internal static int inAbilityDraw;
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.DrawHighlight))]
    public static class Patch_BigRing_Highlight
    {
        public static void Prefix() => BigRangeRing.inAbilityDraw++;
        public static void Finalizer() => BigRangeRing.inAbilityDraw--;
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.GizmoUpdateOnMouseover))]
    public static class Patch_BigRing_Mouseover
    {
        public static void Prefix() => BigRangeRing.inAbilityDraw++;
        public static void Finalizer() => BigRangeRing.inAbilityDraw--;
    }

    [HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawRadiusRing),
        new Type[] { typeof(IntVec3), typeof(float), typeof(Color), typeof(Func<IntVec3, bool>) })]
    public static class Patch_BigRing_Draw
    {
        public static bool Prefix(IntVec3 center, float radius)
        {
            if (BigRangeRing.inAbilityDraw <= 0 || radius <= BigRangeRing.Threshold) return true;
            GenDraw.DrawCircleOutline(center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays), radius);
            return false;
        }
    }
}
