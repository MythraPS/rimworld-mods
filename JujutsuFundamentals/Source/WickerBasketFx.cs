using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Hollow Wicker Basket's look (her ask 2026-11-11). Canon: "a spherical cage that looks like a woven basket
    /// appears with the user in the middle" (Game Rant; ch. 171, 249, 266), held by a hand sign, cracking under hits.
    /// No canon colour; her call: Sukuna's red and white, everyone else white and their own cursed-energy colour.
    /// Drawn every frame over the holder: the white weave and the coloured accent (tools/art_wicker_basket.py), turning
    /// slowly. Wear and hits flash a crack on it; a break shatters it; dropping it normally just fades it out.
    /// Numbers are proposals.
    /// </summary>
    [StaticConstructorOnStartup]
    public class WickerBasketFx : GameComponent
    {
        public const float Size = 2.2f;           // cells across
        public const int FadeTicks = 24, ShatterTicks = 18, CrackGap = 30;

        private static readonly HashSet<Pawn> up = new HashSet<Pawn>();
        private class Fading { public Map map; public Vector3 at; public Color accent; public int start; public bool shatter; }
        private static readonly List<Fading> fading = new List<Fading>();
        private static readonly Dictionary<int, int> lastCrack = new Dictionary<int, int>();
        private static readonly HashSet<int> shattered = new HashSet<int>();

        private static readonly Texture2D WeaveTex = ContentFinder<Texture2D>.Get("Things/Jujutsu/WickerBasket/Weave", false);
        private static readonly Texture2D AccentTex = ContentFinder<Texture2D>.Get("Things/Jujutsu/WickerBasket/Accent", false);
        private static readonly Dictionary<Color, Material> weaveMats = new Dictionary<Color, Material>();
        private static readonly Dictionary<Color, Material> accentMats = new Dictionary<Color, Material>();

        public WickerBasketFx(Game game) { up.Clear(); fading.Clear(); lastCrack.Clear(); shattered.Clear(); }

        /// <summary>Sukuna red; anyone else their cursed-energy colour (blue, Yuta purple, ...).</summary>
        public static Color Accent(Pawn p) => Patch_WeaponGlow.EnergyColor(p);

        public static void Register(Pawn p) { if (p != null) up.Add(p); }

        /// <summary>The basket went down: fade it out, or (if it was broken) the shatter already drew its burst.</summary>
        public static void Dropped(Pawn p)
        {
            if (p == null) return;
            up.Remove(p);
            if (shattered.Remove(p.thingIDNumber) || !p.Spawned) return;
            fading.Add(new Fading { map = p.Map, at = p.DrawPos, accent = Accent(p), start = Find.TickManager.TicksGame });
        }

        /// <summary>Worn or hit while held: a crack flashes across it (at most every half second).</summary>
        public static void Crack(Pawn p, float size = 1.3f)
        {
            if (p == null || !p.Spawned) return;
            int now = Find.TickManager.TicksGame;
            if (lastCrack.TryGetValue(p.thingIDNumber, out var last) && now - last < CrackGap) return;
            lastCrack[p.thingIDNumber] = now;
            var at = p.DrawPos + new Vector3(Rand.Range(-0.5f, 0.5f), 0f, Rand.Range(-0.5f, 0.5f));
            ThinIce.Crack(at, p.Map, size);
        }

        /// <summary>Broken (Malevolent Shrine's wear, Jacob's Ladder, a strip): it bursts apart.</summary>
        public static void Shatter(Pawn p)
        {
            if (p == null || !p.Spawned) return;
            var map = p.Map;
            shattered.Add(p.thingIDNumber);
            ThinIce.Crack(p.DrawPos, map, Size * 1.2f);
            for (int i = 0; i < 4; i++) FleckMaker.ThrowMicroSparks(p.DrawPos + new Vector3(Rand.Range(-0.8f, 0.8f), 0f, Rand.Range(-0.8f, 0.8f)), map);
            FleckMaker.ThrowDustPuffThick(p.DrawPos, map, 1.6f, new Color(1f, 1f, 1f, 0.8f));
            fading.Add(new Fading { map = map, at = p.DrawPos, accent = Accent(p), start = Find.TickManager.TicksGame, shatter = true });
        }

        private static Material Mat(Dictionary<Color, Material> cache, Texture2D tex, Color c)
        {
            if (tex == null) return null;
            if (!cache.TryGetValue(c, out var m)) { m = MaterialPool.MatFrom(tex, ShaderDatabase.Transparent, c); cache[c] = m; }
            return m;
        }

        private static void Draw(Vector3 at, float scale, float angle, float alpha, Color accent)
        {
            if (WeaveTex == null) return;
            at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            var m = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(scale, 1f, scale));
            // colours rounded so the material cache stays small
            float a = Mathf.Round(alpha * 20f) / 20f;
            var w = Mat(weaveMats, WeaveTex, new Color(1f, 1f, 1f, 0.8f * a));
            var c = Mat(accentMats, AccentTex, new Color(accent.r, accent.g, accent.b, a));
            if (w != null) Graphics.DrawMesh(MeshPool.plane10, m, w, 0);
            at.y += 0.002f;
            m = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(scale, 1f, scale));
            if (c != null) Graphics.DrawMesh(MeshPool.plane10, m, c, 0);
        }

        public override void GameComponentUpdate()
        {
            if (up.Count == 0 && fading.Count == 0) return;
            var map = Find.CurrentMap;
            if (map == null || RimWorld.Planet.WorldRendererUtility.WorldRendered) return;
            int now = Find.TickManager.TicksGame;
            float spin = now * 0.15f;                                   // a slow turn, stops when paused
            float pulse = 0.9f + 0.1f * Mathf.Sin(now * 0.05f);
            up.RemoveWhere(p => p == null || p.Destroyed || p.Dead || !Jujutsu.Has(p, JJKDefOf.JJK_HollowWickerBasket));
            foreach (var p in up)
                if (p.Spawned && p.Map == map) Draw(p.DrawPos, Size, spin, pulse, Accent(p));
            for (int i = fading.Count - 1; i >= 0; i--)
            {
                var f = fading[i];
                int life = f.shatter ? ShatterTicks : FadeTicks;
                float t = (now - f.start) / (float)life;
                if (t >= 1f || f.map == null || !Find.Maps.Contains(f.map)) { fading.RemoveAt(i); continue; }
                if (f.map != map) continue;
                // a fade shrinks a little; a shatter flies apart (grows) as it fades
                float scale = f.shatter ? Size * (1f + 0.6f * t) : Size * (1f - 0.1f * t);
                Draw(f.at, scale, spin, 1f - t, f.accent);
            }
        }
    }

    /// <summary>A pawn holding the basket and taking a hit: it cracks (canon ch. 266: Yuji's punches crack Sukuna's).</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_WickerBasketHit
    {
        public static void Postfix(Pawn __instance, float totalDamageDealt)
        {
            if (totalDamageDealt > 0f && Jujutsu.Has(__instance, JJKDefOf.JJK_HollowWickerBasket)) WickerBasketFx.Crack(__instance);
        }
    }
}
