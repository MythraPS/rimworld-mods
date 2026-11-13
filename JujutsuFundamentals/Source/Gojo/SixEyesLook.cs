using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace GojoPsycasts
{
    /// <summary>The Six Eyes look (2026-11-06). Without Facial Animation: our two light-blue eye pictures
    /// (vanilla eye render nodes on the Six Eyes hediff). With it: those are skipped, since they don't line
    /// up with its animated eyes, and its own eyes are coloured light blue instead. The setting can force
    /// our pictures on or off.</summary>
    public static class SixEyesLook
    {
        private static bool? fa;
        public static bool FacialAnimation
        {
            get
            {
                // any version of it (the main mod and its Experimentals branch): matched by name or id, since
                // the exact package id couldn't be confirmed from here
                if (fa == null) fa = LoadedModManager.RunningModsListForReading.Any(m =>
                    (m.PackageIdPlayerFacing ?? "").ToLowerInvariant().Contains("facialanimation")
                    || (m.Name ?? "").IndexOf("Facial Animation", System.StringComparison.OrdinalIgnoreCase) >= 0);
                return fa.Value;
            }
        }

        /// <summary>The Six Eyes' light blue, measured from our own eye picture.</summary>
        public static readonly Color Blue = new Color(150f / 255f, 212f / 255f, 248f / 255f);

        // Facial Animation draws its own eyes (EyeballControllerComp). Their colour is the comp's colour
        // field (FaceColor; FaceSecondColor for the other eye), which its human eye types use as is
        // (colorMode compDefaultColor -> CompDefaultCurrentColor returns color). Checked in its 1.6 dll,
        // 2026-11-06. Reached by reflection so this mod doesn't need Facial Animation.
        private static System.Type eyeComp;
        private static PropertyInfo main, second;
        private static MethodInfo dirty;
        private static bool looked;

        /// <summary>With Facial Animation on, make a Six Eyes pawn's own animated eyes light blue (so he keeps
        /// his blue eyes and they still blink). Cheap to call often: only writes when they differ.</summary>
        /// <returns>True when Facial Animation's eyes were already blue (nothing left to do).</returns>
        public static bool TintFacialAnimationEyes(Pawn p)
        {
            if (!FacialAnimation || p == null) return false;
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
                    if (!Same((Color)main.GetValue(c), Blue)) { main.SetValue(c, Blue); changed = true; }
                    if (second != null && !Same((Color)second.GetValue(c), Blue)) { second.SetValue(c, Blue); changed = true; }
                    if (changed) dirty?.Invoke(c, null);
                }
                catch (System.Exception e) { Log.WarningOnce("[Satoru Gojo] couldn't colour Facial Animation's eyes: " + e.Message, 81503417); eyeComp = null; return false; }
                return !changed;
            }
            return false;
        }

        // Gojo's own face in Facial Animation (her ask 2026-11-06: as close to his anime face as it can get):
        // its big bright Normal eyes, the plain Normal head and plain skin (no random scar or mole). His brows
        // and lids take his hair colour in Facial Animation already (white).
        private static readonly string[][] GojoFaceParts =
        {
            new[] { "FacialAnimation.EyeballControllerComp", "FacialAnimation.EyeballTypeDef", "Gojo_EyeWideSmall" },   // Wide at 93% (her notes 2026-11-07: Normal looked bored, Wide a tiny bit big)
            new[] { "FacialAnimation.HeadControllerComp", "FacialAnimation.HeadTypeDef", "HeadNormal" },
            new[] { "FacialAnimation.SkinControllerComp", "FacialAnimation.SkinTypeDef", "SkinNormal" },
            // her note 2026-11-07, still bored: the lids, brows and mouth were random (Retexture has
            // LidBored, LidSleepy...). Half-lowered sharp lids and a lopsided smirk: smug (her note 2026-11-07), relaxed short brows. These
            // three only exist with the Retexture/Experimentals sets; without them they're skipped.
            new[] { "FacialAnimation.LidControllerComp", "FacialAnimation.LidTypeDef", "LidSharp" },
            new[] { "FacialAnimation.MouthControllerComp", "FacialAnimation.MouthTypeDef", "MouthSmiling" },
            new[] { "FacialAnimation.BrowControllerComp", "FacialAnimation.BrowTypeDef", "BrowShort" },
        };
        private static System.Collections.Generic.List<(System.Type comp, PropertyInfo type, Def def, MethodInfo dirty)> faceParts;

        /// <summary>Sets Gojo's Facial Animation face parts. Cheap: only writes what differs.</summary>
        /// <returns>True when every part was already set (nothing left to do).</returns>
        public static bool GojoFace(Pawn p)
        {
            if (!FacialAnimation || p == null) return false;
            if (faceParts == null)
            {
                faceParts = new System.Collections.Generic.List<(System.Type, PropertyInfo, Def, MethodInfo)>();
                foreach (var part in GojoFaceParts)
                {
                    var ct = GenTypes.GetTypeInAnyAssembly(part[0]);
                    var dt = GenTypes.GetTypeInAnyAssembly(part[1]);
                    if (ct == null || dt == null) continue;
                    var def = GenDefDatabase.GetDefSilentFail(dt, part[2], false);
                    var prop = ct.GetProperty("FaceType", BindingFlags.Instance | BindingFlags.Public);
                    if (def != null && prop != null) faceParts.Add((ct, prop, def, ct.GetMethod("SetDirty", BindingFlags.Instance | BindingFlags.Public)));
                }
            }
            int found = 0; bool changed = false;
            foreach (var c in p.AllComps)
                foreach (var part in faceParts)
                {
                    if (!part.comp.IsInstanceOfType(c)) continue;
                    found++;
                    try
                    {
                        if (part.type.GetValue(c) != (object)part.def) { part.type.SetValue(c, part.def); part.dirty?.Invoke(c, null); changed = true; }
                    }
                    catch (System.Exception e) { Log.WarningOnce("[Satoru Gojo] couldn't set Facial Animation's face: " + e.Message, 81503418); return false; }
                }
            return found > 0 && !changed;
        }

        private static bool Same(Color a, Color b) =>
            Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

        public static bool Show
        {
            get
            {
                var m = JujutsuFundamentals.JujutsuMod.Settings?.sixEyes ?? EyesMode.Auto;
                return m == EyesMode.On || (m == EyesMode.Auto && !FacialAnimation);
            }
        }
    }

    public class PawnRenderNodeWorker_SixEyes : PawnRenderNodeWorker_Eye
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) => SixEyesLook.Show && base.CanDrawNow(node, parms);
    }
}
