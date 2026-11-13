using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Granite Tsunami's look (her ask 2026-11-11: flashier, Granite Blasts landing from the sky). Each blast is a beam in
    /// Granite Blast's colours that shoots down from high above a cell, with a ball of cursed energy at its head; when the
    /// head reaches the ground it bursts (flash, dust, sparks, a crack of thunder) and only then does its hit land.
    /// Falls are spread over the domain's interval so blasts rain down steadily. Visual queue only, not saved: a fall
    /// that is mid-air when the game is saved is dropped.
    /// </summary>
    [StaticConstructorOnStartup]
    public class RyuSkyBlasts : MapComponent
    {
        public const int DropTicks = 10;    // from the sky to the ground
        public const int FadeTicks = 14;    // the beam fades after it lands
        public const float Height = 16f;    // how far above the cell it starts, in cells (screen-up)

        private class Fall
        {
            public IntVec3 cell; public int start; public bool big; public Action onImpact; public bool landed;
        }

        private readonly List<Fall> falls = new List<Fall>();

        // Granite Blast's own blue beam and ball (Beams, Blast.cs)
        private static Material Core => Beams.GCore;
        private static Material Glow => Beams.GGlow;
        private static Material Orb => Beams.GraniteOrb;

        public RyuSkyBlasts(Map map) : base(map) { }

        /// <summary>A blast that lands on <paramref name="cell"/> after <paramref name="delay"/> ticks plus its drop.</summary>
        public static void Drop(Map map, IntVec3 cell, int delay, bool big, Action onImpact = null)
        {
            var c = map?.GetComponent<RyuSkyBlasts>();
            if (c == null) { onImpact?.Invoke(); return; }
            c.falls.Add(new Fall { cell = cell, start = Find.TickManager.TicksGame + delay, big = big, onImpact = onImpact });
        }

        public override void MapComponentTick()
        {
            if (falls.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = falls.Count - 1; i >= 0; i--)
            {
                var f = falls[i];
                int age = now - f.start;
                if (age < DropTicks) continue;
                if (!f.landed)
                {
                    f.landed = true;
                    Impact(f);
                }
                if (age > DropTicks + FadeTicks) falls.RemoveAt(i);
            }
        }

        private void Impact(Fall f)
        {
            if (!f.cell.InBounds(map)) return;
            var v = f.cell.ToVector3Shifted();
            FleckMaker.Static(f.cell, map, FleckDefOf.ExplosionFlash, f.big ? 4f : 2.6f);
            FleckMaker.ThrowLightningGlow(v, map, f.big ? 2.6f : 1.6f);
            FleckMaker.ThrowDustPuffThick(v, map, f.big ? 2.4f : 1.7f, new Color(0.75f, 0.75f, 0.82f));
            FleckMaker.ThrowMicroSparks(v, map);
            if (f.big || Rand.Chance(0.35f)) SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(f.cell, map));
            try { f.onImpact?.Invoke(); }
            catch (Exception e) { Log.Warning("[JJK Core] Granite Tsunami hit failed: " + e.Message); }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map || falls.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            float alt = AltitudeLayer.MoteOverhead.AltitudeFor();
            foreach (var f in falls)
            {
                int age = now - f.start;
                if (age < 0) continue;
                var ground = f.cell.ToVector3Shifted(); ground.y = alt;
                var top = ground + new Vector3(0f, 0f, Height);
                float w = f.big ? 1.6f : 1f;
                if (age < DropTicks)
                {
                    // the head races down from the sky, the beam trailing behind it
                    float t = age / (float)DropTicks;
                    var head = Vector3.Lerp(top, ground, t * t);
                    GenDraw.DrawLineBetween(top, head, Glow, 1.6f * w);
                    GenDraw.DrawLineBetween(top, head, Core, 0.55f * w);
                    float s = 1.4f * w;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(head, Quaternion.identity, new Vector3(s, 1f, s)), Orb, 0);
                }
                else
                {
                    float fade = 1f - Mathf.Clamp01((age - DropTicks) / (float)FadeTicks);
                    GenDraw.DrawLineBetween(top, ground, Glow, 2.2f * w * fade);
                    GenDraw.DrawLineBetween(top, ground, Core, 0.8f * w * fade);
                    float s = (1.5f + 2.5f * (1f - fade)) * w;
                    Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(ground, Quaternion.identity, new Vector3(s, 1f, s)), Orb, 0);
                }
            }
        }
    }
}
