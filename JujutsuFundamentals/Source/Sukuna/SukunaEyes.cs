using System.Reflection;
using UnityEngine;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Sukuna's red eyes in Facial Animation (her note 2026-11-07). Facial Animation colours its
    /// eyes from EyeballControllerComp.FaceColor / FaceSecondColor (the same fields the Gojo mod sets for
    /// the Six Eyes). Reached by reflection so this mod doesn't need Facial Animation.</summary>
    public static class SukunaEyes
    {
        public static readonly Color Red = new Color(0.78f, 0.08f, 0.12f);

        private static System.Type eyeComp;
        private static PropertyInfo main, second;
        private static MethodInfo dirty;
        private static bool looked;

        /// <summary>Only writes when the colour differs.</summary>
        /// <returns>True when the eyes were already red (nothing left to do).</returns>
        public static bool Tint(Pawn p)
        {
            if (!PawnRenderNodeWorker_OverFacialAnimation.FacialAnimation || p == null) return false;
            if (!looked)
            {
                looked = true;
                eyeComp = GenTypes.GetTypeInAnyAssembly("FacialAnimation.EyeballControllerComp");
                if (eyeComp != null)
                {
                    main = eyeComp.GetProperty("FaceColor", BindingFlags.Instance | BindingFlags.Public);
                    second = eyeComp.GetProperty("FaceSecondColor", BindingFlags.Instance | BindingFlags.Public);
                    dirty = eyeComp.GetMethod("SetDirty", BindingFlags.Instance | BindingFlags.Public);
                }
            }
            if (eyeComp == null || main == null) return false;
            foreach (var c in p.AllComps)
            {
                if (!eyeComp.IsInstanceOfType(c)) continue;
                bool changed = false;
                try
                {
                    if (!Same((Color)main.GetValue(c), Red)) { main.SetValue(c, Red); changed = true; }
                    if (second != null && !Same((Color)second.GetValue(c), Red)) { second.SetValue(c, Red); changed = true; }
                    if (changed) dirty?.Invoke(c, null);
                }
                catch (System.Exception e) { Log.WarningOnce("[Sukuna] couldn't colour Facial Animation's eyes: " + e.Message, 81503517); eyeComp = null; return false; }
                return !changed;
            }
            return false;
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;
    }
}
