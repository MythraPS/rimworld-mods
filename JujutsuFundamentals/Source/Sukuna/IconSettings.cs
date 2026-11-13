using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    /// <summary>Which ability icons to show (her request 2026-10-29): the pictures,
    /// kanji only where the picture is just a letter, or kanji everywhere. Kanji
    /// textures sit in Abilities/Sukuna/Kanji/ under the same names as the pictures.</summary>
    public enum IconStyle { Pictures, KanjiForLetters, KanjiForAll }

    // The old "Ryomen Sukuna & Yuji Itadori" settings page is gone (2026-11-12): its options live on the mod's one settings page
    // (JujutsuFundamentals.JujutsuSettings), and the code reads them from there.

    [StaticConstructorOnStartup]
    public static class AbilityIcons
    {
        /// <summary>Abilities whose picture icon is only a letter.</summary>
        private static readonly HashSet<string> LetterIcons = new HashSet<string> { "PseudoInfinity", "BlackFlash", "ReverseCursedTechnique", "Thorns", "WorldCuttingSlash" };

        static AbilityIcons() => Apply();

        public static void Apply()
        {
            var style = (IconStyle)(int)(JujutsuFundamentals.JujutsuMod.Settings?.icons ?? JujutsuFundamentals.IconStyle.Pictures);
            foreach (var def in DefDatabase<VEFAbilityDef>.AllDefs.Where(d => d.defName.StartsWith("Sukuna_") && !d.iconPath.NullOrEmpty()))
            {
                string name = def.iconPath.Substring(def.iconPath.LastIndexOf('/') + 1);
                bool kanji = style == IconStyle.KanjiForAll || (style == IconStyle.KanjiForLetters && LetterIcons.Contains(name));
                Texture2D tex = null;
                if (kanji) tex = ContentFinder<Texture2D>.Get(def.iconPath.Substring(0, def.iconPath.LastIndexOf('/')) + "/Kanji/" + name, false);
                def.icon = tex ?? ContentFinder<Texture2D>.Get(def.iconPath, false) ?? def.icon;
            }
        }
    }
}
