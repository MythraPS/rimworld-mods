using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace GojoPsycasts
{
    /// <summary>Which ability icons to show (her request 2026-10-29): the pictures,
    /// kanji only where the picture is just a letter, or kanji everywhere. Kanji
    /// textures sit in Abilities/Gojo/Kanji/ under the same names as the pictures.</summary>
    public enum IconStyle { Pictures, KanjiForLetters, KanjiForAll }

    public class IconModSettings : ModSettings
    {
        public IconStyle icons = IconStyle.Pictures;
        /// <summary>Black Flash chance, as a multiple of normal (her ask 2026-11-04). 0 = never.</summary>
        public float blackFlashChance = 1f;
        /// <summary>Off: only melee attacks can be a Black Flash. On: abilities too.</summary>
        public bool blackFlashAbilities = true;
        public override void ExposeData()
        {
            Scribe_Values.Look(ref icons, "icons", IconStyle.Pictures);
            Scribe_Values.Look(ref blackFlashChance, "blackFlashChance", 1f);
            Scribe_Values.Look(ref blackFlashAbilities, "blackFlashAbilities", true);
        }
    }

    public class IconSettingsMod : Mod
    {
        public static IconModSettings Settings;

        public IconSettingsMod(ModContentPack content) : base(content) => Settings = GetSettings<IconModSettings>();

        public override string SettingsCategory() => "Satoru Gojo";

        public override void DoSettingsWindowContents(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            l.Label("Ability icons");
            Option(l, IconStyle.Pictures, "Pictures", "The picture icons; abilities without a picture show a letter.");
            Option(l, IconStyle.KanjiForLetters, "Kanji instead of letters", "Keep the pictures, and show kanji where the icon is only a letter.");
            Option(l, IconStyle.KanjiForAll, "Kanji for everything", "Every ability shows its kanji.");
            l.GapLine();
            l.Label("Black Flash");
            int pct = Mathf.RoundToInt(Settings.blackFlashChance * 100f);
            l.Label((string)($"Chance: {pct}% of normal" + (pct == 0 ? " (never)" : "")), -1f, (TipSignal?)
                "Multiplies every Black Flash chance in this mod. 100% is the default.");
            Settings.blackFlashChance = Mathf.Round(l.Slider(Settings.blackFlashChance, 0f, 3f) * 20f) / 20f;
            l.CheckboxLabeled("Abilities can trigger Black Flash", ref Settings.blackFlashAbilities,
                "On: melee attacks and ability damage can both be a Black Flash. Off: only melee attacks. Gunshots never are.");
            l.End();
        }

        private static void Option(Listing_Standard l, IconStyle style, string label, string tip)
        {
            if (l.RadioButton(label, Settings.icons == style, 0f, tip) && Settings.icons != style)
            {
                Settings.icons = style;
                AbilityIcons.Apply();
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class AbilityIcons
    {
        /// <summary>Abilities whose picture icon is only a letter.</summary>
        private static readonly HashSet<string> LetterIcons = new HashSet<string> { "BlueFist", "Speed", "ReverseCursedTechnique", "Teleport", "Reinforcement" };

        static AbilityIcons() => Apply();

        public static void Apply()
        {
            var style = IconSettingsMod.Settings?.icons ?? IconStyle.Pictures;
            foreach (var def in DefDatabase<VEFAbilityDef>.AllDefs.Where(d => d.defName.StartsWith("Gojo_") && !d.iconPath.NullOrEmpty()))
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
