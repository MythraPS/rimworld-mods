using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VanillaPsycastsExpanded.UI;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace GojoPsycasts
{
    /// <summary>
    /// Lays the Limitless nodes out on the infinity sign in the background instead of
    /// VPE's grid. VPE's own DoPathAbilities (read from VanillaPsycastsExpanded.dll,
    /// 1.6) does three things for a path, and this does the same three for ours:
    ///   1. a 2px line from each node to each prerequisite, white once the
    ///      prerequisite is learned and grey before;
    ///   2. a 36x36 rect per node, handed to the doAbility callback, which draws
    ///      the button and handles clicks;
    ///   3. each node's centre written into abilityPos, which other UI reads.
    /// Every other path goes through VPE untouched.
    ///
    /// Positions are in units of the panel's WIDTH from its centre, so the two
    /// rings stay round whatever size the panel is drawn at. Picture:
    ///
    ///        Black Flash      Purple       Domain
    ///   Blue Fist  ( blue )  Infinity  ( red )  Red
    ///   Speed     Blue       Teleport       RCT
    /// </summary>
    [HarmonyPatch(typeof(PsycastsUIUtility), nameof(PsycastsUIUtility.DoPathAbilities))]
    public static class TreeLayout
    {
        public const float RingX = 0.19f;   // ring centres, left and right of Infinity
        public const float RingR = 0.21f;   // ring radius: larger than RingX, so the rings overlap around Infinity

        private static readonly Dictionary<string, Vector2> Spots = new Dictionary<string, Vector2>
        {
            { "Gojo_Infinity",               new Vector2(0f, 0f) },
            { "Gojo_BlackFlash",             new Vector2(-RingX, -RingR) },
            { "Gojo_BlueFist",               new Vector2(-RingX - RingR, 0f) },
            { "Gojo_Blue",                   new Vector2(-RingX, RingR) },
            { "Gojo_Speed",                  new Vector2(-RingX - RingR, 0.27f) },
            { "Gojo_InfiniteVoid",           new Vector2(RingX, -RingR) },
            { "Gojo_Red",                    new Vector2(RingX + RingR, 0f) },
            { "Gojo_ReverseCursedTechnique", new Vector2(RingX, RingR) },
            { "Gojo_Reinforcement",          new Vector2(RingX + RingR, 0.27f) },   // mirrors Speed (2026-10-31)
            { "Gojo_Purple",                 new Vector2(0f, -0.30f) },
            { "Gojo_Teleport",               new Vector2(0f, 0.30f) },
        };

        private const float Node = 36f;

        public static bool Prefix(Rect inRect, PsycasterPathDef path,
                                  Dictionary<VEFAbilityDef, Vector2> abilityPos,
                                  Action<Rect, VEFAbilityDef> doAbility)
        {
            if (path != GojoDefOf.Gojo_Path) return true;
            try
            {
                var centre = inRect.center;
                float w = inRect.width;

                foreach (var def in path.abilities)
                {
                    var off = Spots.TryGetValue(def.defName, out var o) ? o : Vector2.zero;
                    abilityPos[def] = centre + off * w;
                }

                var comp = PsycastsUIUtility.CompAbilities;
                foreach (var def in path.abilities)
                {
                    var prereqs = def.Psycast()?.prerequisites;
                    if (prereqs == null) continue;
                    foreach (var pre in prereqs)
                    {
                        if (!abilityPos.TryGetValue(pre, out var from)) continue;
                        bool lit = comp != null && comp.HasAbility(pre);
                        Widgets.DrawLine(abilityPos[def], from, lit ? Color.white : Color.grey, 2f);
                    }
                }

                foreach (var def in path.abilities)
                {
                    var c = abilityPos[def];
                    doAbility(new Rect(c.x - Node / 2f, c.y - Node / 2f, Node, Node), def);
                }
                return false;
            }
            catch (Exception e)
            {
                Log.ErrorOnce("[Satoru Gojo] Infinity-sign layout failed, using VPE's grid: " + e, 0x60701);
                return true;
            }
        }
    }
}
