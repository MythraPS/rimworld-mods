using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Cursed Energy Blast (her call 2026-11-10): built up during a long cast, then let out as one
    /// continuous, powerful beam rather than a small shot. For a second the beam runs from the caster
    /// out to the full range in the target's direction, hitting everything along it every few ticks,
    /// until a wall stops it. Total damage is the ability's power (grows with psychic sensitivity).
    /// </summary>
    public class Ability_CursedEnergyBlast : Ability
    {
        /// <summary>Ryu's Granite Blast takes its place (her call 2026-11-10): the node stays learned, its button goes.</summary>
        public override bool ShowGizmoOnPawn() =>
            !Jujutsu.Knows(pawn, DefDatabase<VEF.Abilities.AbilityDef>.GetNamedSilentFail("JJK_GraniteBlast")) && base.ShowGizmoOnPawn();

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = targets.FirstOrDefault();
            if (!t.IsValid || pawn.Map == null) return;
            Beams.Fire(pawn, t.Cell, GetRangeForPawn(), GetPowerForPawn());
        }
    }

    /// <summary>
    /// Granite Blast (her calls 2026-11-10): a larger build-up, then a longer-lasting, wider beam than Cursed Energy
    /// Blast. While it builds, a ball of cursed energy grows in front of Ryu (Beams draws it).
    /// </summary>
    public class Ability_GraniteBlast : Ability
    {
        public const int BeamTicks = 150;
        public const int BeamPulses = 12;
        public const float BeamWidth = 2.4f;

        // VEF calls the no-argument PreWarmupAction (the one with a target is obsolete and never called)
        public override void PreWarmupAction()
        {
            base.PreWarmupAction();
            var targets = pawn.GetComp<CompAbilities>()?.currentlyCastingTargets;
            var cell = targets != null && targets.Length > 0 ? targets[0].Cell : pawn.Position;
            Beams.Charging(pawn, this, cell, GetCastTimeForPawn());
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            Beams.StopCharging(pawn);
            var t = targets.FirstOrDefault();
            if (!t.IsValid || pawn.Map == null) return;
            Beams.Fire(pawn, t.Cell, GetRangeForPawn(), GetPowerForPawn(), BeamTicks, BeamPulses, BeamWidth, granite: true);
        }
    }

    [StaticConstructorOnStartup]   // holds Material fields: the game wants these made on the main thread
    public class Beams : MapComponent
    {
        public const int Duration = 60;   // one second of beam
        public const int Pulses = 10;

        private class Beam
        {
            public Pawn caster; public IntVec3 from; public List<IntVec3> cells; public IntVec3 end;
            public float perPulse; public int start; public int duration = Duration; public int pulses = Pulses; public float width = 1f;
            public bool granite;
            /// <summary>Pulses already dealt: exactly <see cref="pulses"/> land, so the total is the ability's power.</summary>
            public int done;
        }

        /// <summary>A new or loaded game starts with no charging balls (StaticReset).</summary>
        public static void Reset() => charges.Clear();

        private class Charge { public Pawn caster; public Ability ability; public IntVec3 target; public int start; public int ticks; }
        private static readonly List<Charge> charges = new List<Charge>();

        public static void Charging(Pawn caster, Ability ability, IntVec3 target, int ticks)
        {
            StopCharging(caster);
            charges.Add(new Charge { caster = caster, ability = ability, target = target, start = Find.TickManager.TicksGame, ticks = Mathf.Max(1, ticks) });
        }

        public static void StopCharging(Pawn caster) => charges.RemoveAll(c => c.caster == caster);

        private static Material orb, graniteOrb;
        /// <summary>Granite Blast is blue (her call 2026-11-11); Cursed Energy Blast keeps the paler white-blue.</summary>
        public static readonly Color GraniteCore = new Color(0.55f, 0.75f, 1f, 0.95f), GraniteGlow = new Color(0.12f, 0.38f, 1f, 0.6f);
        public static Material GraniteOrb => graniteOrb ?? (graniteOrb = MaterialPool.MatFrom((Texture2D)Orb.mainTexture, ShaderDatabase.MoteGlow, new Color(0.45f, 0.65f, 1f)));
        private static Material redOrb;
        /// <summary>Sukuna's cursed energy is red (her call 2026-11-11): his blast's charge ball and beam.</summary>
        public static Material RedOrb => redOrb ?? (redOrb = MaterialPool.MatFrom((Texture2D)Orb.mainTexture, ShaderDatabase.MoteGlow, new Color(1f, 0.3f, 0.3f)));
        public static bool Red(Pawn p) => SukunaPsycasts.SukunaBehaviour.IsSukuna(p);
        /// <summary>A soft glowing ball, made once.</summary>
        public static Material Orb
        {
            get
            {
                if (orb != null) return orb;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - r); a = a * a * (1.6f - 0.6f * a);
                        float core = Mathf.Clamp01(1f - r * 2.2f);
                        px[y * n + x] = new Color32((byte)(150 + 105 * core), (byte)(195 + 60 * core), 255, (byte)(255 * a));
                    }
                tex.SetPixels32(px);
                tex.Apply();
                orb = MaterialPool.MatFrom(tex, ShaderDatabase.MoteGlow, Color.white);
                return orb;
            }
        }

        private readonly List<Beam> beams = new List<Beam>();
        private static Material core, glow, gCore, gGlow, rCore, rGlow;
        private static Material RCore => rCore ?? (rCore = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, new Color(1f, 0.82f, 0.8f, 0.95f)));
        private static Material RGlow => rGlow ?? (rGlow = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, new Color(1f, 0.12f, 0.15f, 0.5f)));
        private static Material Core => core ?? (core = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, new Color(0.85f, 0.93f, 1f, 0.95f)));
        private static Material Glow => glow ?? (glow = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, new Color(0.45f, 0.65f, 1f, 0.45f)));
        public static Material GCore => gCore ?? (gCore = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, GraniteCore));
        public static Material GGlow => gGlow ?? (gGlow = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MoteGlow, GraniteGlow));

        public Beams(Map map) : base(map) { }

        public static void Fire(Pawn caster, IntVec3 target, float range, float total, int duration = Duration, int pulses = Pulses, float width = 1f, bool granite = false)
        {
            var map = caster.Map;
            var comp = map.GetComponent<Beams>();
            if (comp == null) return;
            var from = caster.Position;
            var dir = (target - from).ToVector3();
            if (dir.sqrMagnitude < 0.01f) return;
            dir.Normalize();
            var far = (from.ToVector3Shifted() + dir * range).ToIntVec3();
            var cells = new List<IntVec3>();
            foreach (var c in GenSight.BresenhamCellsBetween(from, far))
            {
                if (c == from) continue;
                if (!c.InBounds(map)) break;
                cells.Add(c);
                var ed = c.GetEdifice(map);
                if (ed != null && ed.def.Fillage == FillCategory.Full) break;   // a wall stops it
            }
            if (cells.Count == 0) return;
            comp.beams.Add(new Beam { caster = caster, from = from, cells = cells, end = cells[cells.Count - 1],
                                      perPulse = total / pulses, start = Find.TickManager.TicksGame,
                                      duration = duration, pulses = pulses, width = width, granite = granite });
            Snd.At("JJK_Snd_Blast", from, map);
        }

        public override void MapComponentTick()
        {
            if (beams.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = beams.Count - 1; i >= 0; i--)
            {
                var b = beams[i];
                int age = now - b.start;
                if (age > b.duration || b.caster == null || b.caster.Dead || !b.caster.Spawned) { beams.RemoveAt(i); continue; }
                // was age % step == 0 for ages 0..duration: 11 pulses instead of 10 (Blast), 13 instead of 12 (Granite)
                if (b.done >= b.pulses || age < b.done * Mathf.Max(1, b.duration / b.pulses)) continue;
                b.done++;
                PsycastContext.Push(b.caster);   // shields (Hollow Wicker Basket, Simple Domain) still block it
                try
                {
                    var def = DefDatabase<DamageDef>.GetNamed("JJK_CursedEnergy");
                    foreach (var c in b.cells)
                        foreach (var t in c.GetThingList(map).ToList())
                        {
                            if (t == b.caster || !(t is Pawn || t is Building) || t.Destroyed) continue;
                            t.TakeDamage(new DamageInfo(def, b.perPulse, 0.3f, -1f, b.caster));
                        }
                    FleckMaker.ThrowMicroSparks(b.end.ToVector3Shifted(), map);
                }
                finally { PsycastContext.Pop(); }
            }
        }

        public override void MapComponentUpdate()
        {
            if (Find.CurrentMap != map) return;
            DrawCharges();
            foreach (var b in beams)
            {
                if (b.caster == null || !b.caster.Spawned) continue;
                var a = b.caster.DrawPos; a.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                var e = b.end.ToVector3Shifted(); e.y = a.y;
                float fade = 1f - Mathf.Clamp01((Find.TickManager.TicksGame - b.start) / (float)b.duration) * 0.5f;
                bool red = !b.granite && Red(b.caster);
                GenDraw.DrawLineBetween(a, e, b.granite ? GGlow : red ? RGlow : Glow, 1.3f * b.width * fade);
                GenDraw.DrawLineBetween(a, e, b.granite ? GCore : red ? RCore : Core, 0.45f * b.width * fade);
            }
        }

        /// <summary>The growing ball in front of a caster still building up, until the cast fires or is broken off.</summary>
        private void DrawCharges()
        {
            if (charges.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            charges.RemoveAll(c => c.caster == null || c.caster.Dead || !c.caster.Spawned || now - c.start > c.ticks + 30
                                   || c.caster.GetComp<CompAbilities>()?.currentlyCasting != c.ability);
            foreach (var c in charges)
            {
                if (c.caster.Map != map) continue;
                float t = Mathf.Clamp01((now - c.start) / (float)c.ticks);
                var dir = (c.target - c.caster.Position).ToVector3();
                dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
                var pos = c.caster.DrawPos + dir * 0.7f;
                pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
                float size = Mathf.Lerp(0.4f, 3.2f, t) * (1f + 0.06f * Mathf.Sin(now * 0.6f));
                Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, 1f, size)),
                                  c.ability is Ability_GraniteBlast ? GraniteOrb : Red(c.caster) ? RedOrb : Orb, 0);
            }
        }
    }
}
