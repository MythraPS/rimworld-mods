using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Cursed energy on a held melee weapon (her calls 2026-11-10):
    /// - Hazy Moon with a melee weapon in hand: a blue aura around the weapon (no energy blade then).
    /// - Cursed Energy Reinforcement raised: the weapon itself glows, blue; Sukuna's red; Yuta's will be purple
    ///   (character mods add their colour through Jujutsu.EnergyColors).
    /// Drawn over the weapon exactly where the game draws it (same mesh, angle and size as DrawEquipmentAiming).
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_WeaponGlow
    {
        public static readonly Color Blue = new Color(0.45f, 0.72f, 1f);
        public static readonly Color AuraBlue = new Color(0.55f, 0.8f, 1f);
        public static readonly Color SukunaRed = new Color(1f, 0.22f, 0.2f);

        private static readonly Dictionary<(Texture, Color), Material> mats = new Dictionary<(Texture, Color), Material>();

        private static Material Glow(Texture tex, Color c)
        {
            if (!mats.TryGetValue((tex, c), out var m))
            {
                m = MaterialPool.MatFrom((Texture2D)tex, ShaderDatabase.MoteGlow, c);
                mats[(tex, c)] = m;
            }
            return m;
        }

        /// <summary>The colour of this pawn's cursed energy: blue, Sukuna red, or what a character adds (Yuta purple).</summary>
        public static Color EnergyColor(Pawn p)
        {
            foreach (var f in Jujutsu.EnergyColors) { var c = f(p); if (c.HasValue) return c.Value; }
            return IsSukuna(p) ? SukunaRed : Blue;
        }

        /// <summary>The colour of this pawn's cursed energy, if its Reinforcement is raised right now.</summary>
        public static Color? ReinforcedColor(Pawn p)
        {
            bool raised = Hediff_Reinforcement.Of(p)?.Raised == true || SukunaRaised(p);
            if (!raised) return null;
            foreach (var f in Jujutsu.EnergyColors) { var c = f(p); if (c.HasValue) return c; }
            return IsSukuna(p) ? SukunaRed : Blue;
        }

        public static void Postfix(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            var pawn = (eq?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            if (pawn == null || !eq.def.IsMeleeWeapon) return;
            bool aura = HazyMoon.On(pawn);
            var glow = ReinforcedColor(pawn);
            if (!aura && glow == null) return;

            float angle = aimAngle - 90f;
            Mesh mesh;
            if (aimAngle > 200f && aimAngle < 340f) { mesh = MeshPool.plane10Flip; angle -= 180f; angle -= eq.def.equippedAngleOffset; }
            else { mesh = MeshPool.plane10; angle += eq.def.equippedAngleOffset; }
            angle %= 360f;
            var g = eq.Graphic is Graphic_StackCount sc ? sc.SubGraphicForStackCount(1, eq.def) : eq.Graphic;
            var tex = g?.MatSingleFor(eq)?.mainTexture;
            if (tex == null) return;
            var size = new Vector3(eq.Graphic.drawSize.x, 1f, eq.Graphic.drawSize.y);
            var rot = Quaternion.AngleAxis(angle, Vector3.up);
            var pos = drawLoc; pos.y += 0.002f;
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.realtimeSinceStartup * 5f);
            // in steps: every new alpha made (and cached) a new material, so a glowing weapon leaked one per frame
            pulse = Mathf.Round(pulse * 40f) / 40f;

            if (aura)   // a soft halo, the weapon's own shape drawn larger and fainter
            {
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, rot, size * 1.45f), Glow(tex, new Color(AuraBlue.r, AuraBlue.g, AuraBlue.b, 0.35f * pulse)), 0);
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, rot, size * 1.2f), Glow(tex, new Color(AuraBlue.r, AuraBlue.g, AuraBlue.b, 0.55f * pulse)), 0);
            }
            if (glow.HasValue)   // the weapon itself lit up in the colour
            {
                var c = glow.Value;
                Graphics.DrawMesh(mesh, Matrix4x4.TRS(pos, rot, size), Glow(tex, new Color(c.r, c.g, c.b, 0.8f * pulse)), 0);
            }
        }

        // ---- Sukuna, read by name until his mod is moved onto this one
        private static Func<Pawn, bool> isSukuna; private static Type sukunaHediff; private static System.Reflection.PropertyInfo boosted; private static bool looked;

        private static void Look()
        {
            if (looked) return;
            looked = true;
            var m = AccessTools.Method(AccessTools.TypeByName("SukunaPsycasts.SukunaBehaviour"), "IsSukuna", new[] { typeof(Pawn) });
            if (m != null) isSukuna = (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), m);
            sukunaHediff = AccessTools.TypeByName("SukunaPsycasts.Hediff_CursedEnergyReinforcement");
            boosted = sukunaHediff == null ? null : AccessTools.Property(sukunaHediff, "Boosted");
        }

        private static bool IsSukuna(Pawn p) { Look(); return isSukuna != null && isSukuna(p); }

        private static bool SukunaRaised(Pawn p)
        {
            Look();
            if (boosted == null || p?.health?.hediffSet == null) return false;
            foreach (var h in p.health.hediffSet.hediffs)
                if (sukunaHediff.IsInstanceOfType(h) && (bool)boosted.GetValue(h)) return true;
            return false;
        }
    }
}
