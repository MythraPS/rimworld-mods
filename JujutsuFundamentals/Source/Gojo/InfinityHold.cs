using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Infinity, the way it looks in the anime (her call 2026-11-07; idea from "Rim All-Stars"):
    /// a shot coming at Gojo slows down, and down, and stops about a cell short of him, hanging in the
    /// air. Each one it stops costs the same neural heat as a blocked hit. After a few seconds it drops,
    /// harmless. If Infinity goes down (heat at its limit, no psyfocus) the hanging shots carry on and
    /// hit. Blows and anything not caught here are still stopped by the shield as before.</summary>
    public static class InfinityHold
    {
        public const float Outer = 3.5f;     // where shots start slowing
        public const float Hang = 1.1f;      // where they stop
        public const int DropAfter = 240;    // ticks hanging before they fall

        private static readonly Dictionary<int, List<Pawn>> holders = new Dictionary<int, List<Pawn>>();
        private static readonly Dictionary<int, int> refreshedAt = new Dictionary<int, int>();
        private static readonly Dictionary<int, int> hangingSince = new Dictionary<int, int>();

        private static Game game;

        /// <summary>A loaded save or a new game starts map ids and ticks again: forget the old one
        /// (review 2026-11-07: the old game's Gojo kept holding shots, and its objects stayed in memory).</summary>
        private static void SameGame()
        {
            if (game == Current.Game) return;
            game = Current.Game;
            holders.Clear(); refreshedAt.Clear(); hangingSince.Clear();
        }

        public static bool AnyHolder(Map map) => map != null && Holders(map).Count > 0;

        private static List<Pawn> Holders(Map map)
        {
            SameGame();
            int now = Find.TickManager.TicksGame;
            if (refreshedAt.TryGetValue(map.uniqueID, out int at) && now >= at && now - at < 30 && holders.TryGetValue(map.uniqueID, out var list))
                return list;
            refreshedAt[map.uniqueID] = now;
            if (!holders.TryGetValue(map.uniqueID, out list)) holders[map.uniqueID] = list = new List<Pawn>();
            list.Clear();
            foreach (var p in map.mapPawns.AllPawnsSpawned)
                if (Poison.InfinityUp(p)) list.Add(p);
            return list;
        }

        private static readonly System.Reflection.FieldInfo destField = AccessTools.Field(typeof(Projectile), "destination");
        private static Vector3 Destination(Projectile p) => (Vector3)destField.GetValue(p);
        private static bool Near(Vector3 a, Vector3 b, float r) => new Vector2(a.x - b.x, a.z - b.z).magnitude < r;

        /// <summary>False: the shot doesn't move this tick.</summary>
        public static bool Move(Projectile shot)
        {
            var map = shot.Map;
            if (map == null) return true;
            var list = Holders(map);
            if (list.Count == 0) { hangingSince.Remove(shot.thingIDNumber); return true; }
            if (shot.def.defName.StartsWith("Gojo_")) return true;   // his own orbs
            Vector3 pos = shot.ExactPosition;
            foreach (var gojo in list)
            {
                if (!gojo.Spawned || gojo == shot.Launcher) continue;
                Vector3 g = gojo.DrawPos;
                float d = new Vector2(pos.x - g.x, pos.z - g.z).magnitude;
                if (d > Outer) continue;
                // only shots coming at him: aimed at him, or landing near him
                // aimed at him, or an enemy's shot landing beside him; never a friend's shot at someone he's fighting
                bool atHim = shot.intendedTarget.Thing == gojo
                             || (shot.Launcher != null && shot.Launcher.HostileTo(gojo) && Near(Destination(shot), g, 1.6f));
                if (!atHim) continue;
                if (!Poison.InfinityUp(gojo)) { hangingSince.Remove(shot.thingIDNumber); return true; }
                if (d <= Hang)
                {
                    int now = Find.TickManager.TicksGame;
                    if (!hangingSince.TryGetValue(shot.thingIDNumber, out int since))
                    {
                        hangingSince[shot.thingIDNumber] = now;
                        gojo.psychicEntropy?.TryAddEntropy(HediffComp_Infinity.HeatFor(shot.def.projectile?.damageDef,
                            shot.DamageAmount, shot.Launcher), gojo, scale: true, overLimit: true);
                    }
                    else if (now - since > DropAfter)
                    {
                        hangingSince.Remove(shot.thingIDNumber);
                        FleckMaker.ThrowDustPuff(pos, map, 0.6f);
                        shot.Destroy(DestroyMode.Vanish);
                    }
                    return false;
                }
                // slower the closer it gets: it moves on only some ticks
                hangingSince.Remove(shot.thingIDNumber);
                return Rand.Chance(Mathf.Clamp01((d - Hang) / (Outer - Hang)) * 0.9f + 0.05f);
            }
            hangingSince.Remove(shot.thingIDNumber);
            return true;
        }
    }

    /// <summary>Off screen the game moves shots 15 ticks at a time, so they jumped past Infinity's
    /// range in one step (review 2026-11-07). Near someone holding Infinity they move every tick.</summary>
    [HarmonyPatch(typeof(Projectile), "get_UpdateRateTicks")]
    public static class Patch_InfinityHoldRate
    {
        public static void Postfix(Projectile __instance, ref int __result)
        {
            if (__result > 1 && __instance.Spawned && InfinityHold.AnyHolder(__instance.Map)) __result = 1;
        }
    }

    [HarmonyPatch(typeof(Projectile), "TickInterval")]
    public static class Patch_InfinityHold
    {
        public static bool Prefix(Projectile __instance) => InfinityHold.Move(__instance);
    }
}
