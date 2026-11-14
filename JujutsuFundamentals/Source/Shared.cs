using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    public enum IconStyle { Pictures, KanjiForLetters, KanjiForAll }

    /// <summary>Settings shared by every Jujutsu mod (moved here from Sukuna's and Gojo's own).</summary>
    public class JujutsuSettings : ModSettings
    {
        public IconStyle icons = IconStyle.Pictures;
        /// <summary>Black Flash chance, as a multiple of normal. 0 = never.</summary>
        public float blackFlashChance = 1f;
        /// <summary>Off: only melee attacks can be a Black Flash. On: psycasts too.</summary>
        public bool blackFlashAbilities = false;
        /// <summary>Yuta's starting stage (her call 2026-11-11; default the youngest): 1 start of JJK 0,
        /// 2 end of JJK 0, 3 Shinjuku Showdown, 4 adult (theoretical).</summary>
        public int yutaStage = 1;
        /// <summary>Psycast experience from real fights (BattleExperience), as a multiple. 0 = off.</summary>
        public float battleXp = 1f;
        /// <summary>Her call 2026-11-11: off (default) = characters keep their own ideoligion when they join;
        /// on = they take up the colony's main ideoligion as they join.</summary>
        public bool joinColonyIdeo = false;
        /// <summary>Her call 2026-11-11: on (default) = domains' sure hits land on everyone inside who isn't an ally,
        /// neutrals included; off = only hostiles.</summary>
        public bool domainsHitNeutrals = true;
        /// <summary>Which Maki is made (her call 2026-11-11): 1 before her awakening, 2 awakened (default, the one that
        /// existed first).</summary>
        public int makiForm = 2;
        /// <summary>How fast fingers and Death Paintings turn up (her call 2026-11-11): 0 slow, 1 normal (default), 2 fast.
        /// See SukunaPsycasts.RelicPace.</summary>
        public int relicPace = 1;
        /// <summary>Who Gojo's arrival brings (her pick 2026-11-06): young Gojo (default) or adult Gojo. Moved here from
        /// the old "Satoru Gojo" settings page 2026-11-12, when the three pages became one.</summary>
        public bool youngGojo = true;
        /// <summary>The light-blue Six Eyes drawn over the eyes. Auto = shown, unless Facial Animation is loaded.</summary>
        public GojoPsycasts.EyesMode sixEyes = GojoPsycasts.EyesMode.Auto;
        /// <summary>Which Yuji arrives (her calls 2026-11-12): 1 start, 2 Shibuya, 3 Culling Game, 4 Shinjuku, 5 Modulo.</summary>
        public int yujiStage = 1;
        /// <summary>True (default): Yuji's fingers give him levels and Shrine only once Sukuna has left him; false: from the first finger.</summary>
        public bool yujiShrineAfterSukuna = true;
        /// <summary>Enemy Sukuna's strength: 0 Weakened, 1 Canon (default), 2 Overwhelming, 3 King of Curses, 4 The Enlightened One.</summary>
        public int sukunaStrength = 1;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref icons, "icons", IconStyle.Pictures);
            Scribe_Values.Look(ref blackFlashChance, "blackFlashChance", 1f);
            Scribe_Values.Look(ref blackFlashAbilities, "blackFlashPsycasts", false);
            Scribe_Values.Look(ref yutaStage, "yutaStage", 1);
            Scribe_Values.Look(ref battleXp, "battleXp", 1f);
            Scribe_Values.Look(ref joinColonyIdeo, "joinColonyIdeo", false);
            Scribe_Values.Look(ref domainsHitNeutrals, "domainsHitNeutrals", true);
            Scribe_Values.Look(ref makiForm, "makiForm", 2);
            Scribe_Values.Look(ref relicPace, "relicPace", 1);
            Scribe_Values.Look(ref youngGojo, "youngGojo", true);
            Scribe_Values.Look(ref sixEyes, "sixEyes", GojoPsycasts.EyesMode.Auto);
            Scribe_Values.Look(ref yujiStage, "yujiStage", 1);
            Scribe_Values.Look(ref yujiShrineAfterSukuna, "yujiShrineAfterSukuna", true);
            Scribe_Values.Look(ref sukunaStrength, "sukunaStrength", 1);
        }
    }

    public class JujutsuMod : Mod
    {
        public static JujutsuSettings Settings;

        public JujutsuMod(ModContentPack content) : base(content) => Settings = GetSettings<JujutsuSettings>();

        public override string SettingsCategory() => "Jujutsu Kaisen - Psycasts, Characters, Items, Quests";

        private static Vector2 scroll;
        private static float viewHeight = 1200f;

        /// <summary>Scrolls: there are more options than fit the window.</summary>
        public override void DoSettingsWindowContents(Rect rect)
        {
            var view = new Rect(0f, 0f, rect.width - 20f, viewHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);
            var l = new Listing_Standard();
            l.Begin(view);
            l.Label("Ability icons");
            Option(l, IconStyle.Pictures, "Pictures", "The picture icons; abilities without a picture show a letter.");
            Option(l, IconStyle.KanjiForLetters, "Kanji instead of letters", "Keep the pictures, and show kanji where the icon is only a letter.");
            Option(l, IconStyle.KanjiForAll, "Kanji for everything", "Every ability shows its kanji.");
            l.GapLine();
            l.Label("Black Flash");
            int pct = Mathf.RoundToInt(Settings.blackFlashChance * 100f);
            l.Label((string)($"Chance: {pct}% of normal" + (pct == 0 ? " (never)" : "")), -1f, (TipSignal?)
                "Multiplies every Black Flash chance. 100% is the default.");
            Settings.blackFlashChance = Mathf.Round(l.Slider(Settings.blackFlashChance, 0f, 3f) * 20f) / 20f;
            l.CheckboxLabeled("Psycasts can trigger Black Flash too", ref Settings.blackFlashAbilities,
                "Off (default): only melee attacks can be a Black Flash. On: psycast damage can be one too. Gunshots never are.");
            l.GapLine();
            int bx = Mathf.RoundToInt(Settings.battleXp * 100f);
            l.Label((string)($"Psycast experience from battles: {bx}%" + (bx == 0 ? " (off)" : "")), -1f, (TipSignal?)
                "Every psycaster gains psycast experience from real fights: hurting enemies who are still standing, being hurt by them, and killing them. Those who only support (drafted, near the enemy, tending, rescuing or fighting fires) gain a little over time. Hunting, social fights and hitting downed or captured enemies give nothing. 100% is the default.");
            Settings.battleXp = Mathf.Round(l.Slider(Settings.battleXp, 0f, 3f) * 20f) / 20f;
            l.GapLine();
            l.CheckboxLabeled("Domains hit neutrals", ref Settings.domainsHitNeutrals,
                "On (default): a domain's sure hit lands on everyone inside who isn't an ally (your own faction or an allied one): enemies, neutral visitors and wild animals alike. Off: only those hostile to the caster. Malevolent Shrine cuts everyone either way.");
            l.CheckboxLabeled("Characters join the colony's ideoligion", ref Settings.joinColonyIdeo,
                "Off (default): Jujutsu Kaisen characters keep their own ideoligion when they join. On: they take up your colony's main ideoligion as soon as they join. Needs Ideology.");
            l.GapLine();
            l.Label("Yuta Okkotsu arrives as");
            YutaStage(l, 1, "Start of Jujutsu Kaisen 0", "Only Cursed Spirit Rika, whom he can't control, and no techniques. He grows from there.");
            YutaStage(l, 2, "End of Jujutsu Kaisen 0", "He controls Rika, can copy, and heals with Reverse Cursed Technique.");
            YutaStage(l, 3, "Shinjuku Showdown", "His copied techniques from the Culling Game and his domain.");
            YutaStage(l, 4, "Adult (theoretical)", "Everything unlocked. Shikigami Rika is as strong as her curse form, with no five-minute limit.");
            l.GapLine();
            l.Label("Yuji Itadori arrives as");
            YujiStageOption(l, 1, "Start of the series", "One finger, no techniques. The whole Sukuna questline is ahead.");
            YujiStageOption(l, 2, "Shibuya", "Fifteen fingers inside him, Black Flash and Cursed Energy Reinforcement, no cursed technique of his own. Sukuna can take over.");
            YujiStageOption(l, 3, "Culling Game", "Sukuna has already left him for someone else, with the fifteen fingers: the game starts at the hunt. Shrine's imprint, no Blood Manipulation.");
            YujiStageOption(l, 4, "Shinjuku", "As the Culling Game, plus all six Death Paintings eaten (Hemosage, Piercing Blood) and his domain. Choso comes with him.");
            YujiStageOption(l, 5, "Modulo", "Yuji at his pinnacle, with Sukuna still out there. Piercing Blood Scatter and World Cutting Slash.");
            l.CheckboxLabeled("Yuji's Shrine waits for Sukuna to leave", ref Settings.yujiShrineAfterSukuna,
                "On (default): while Sukuna is inside him, the fingers Yuji eats give him nothing; when Sukuna leaves, he gets all their levels at once, "
                + "spent on Shrine in order. Off: each finger gives its level as he eats it. Either way, World Cutting Slash waits until he is Modulo Yuji.");
            l.GapLine();
            l.Label("Enemy Sukuna's strength");
            for (int i = 0; i < SukunaPsycasts.SukunaStrength.Labels.Length; i++)
            {
                int idx = i;
                if (l.RadioButton($"{SukunaPsycasts.SukunaStrength.Labels[i]} ({SukunaPsycasts.SukunaStrength.Factors[i] * 100f:0}%)", Settings.sukunaStrength == i, 0f,
                        "Multiplies the damage enemy Sukuna deals, and divides the damage he takes. Canon is about even with Gojo.")) Settings.sukunaStrength = idx;
            }
            l.GapLine();
            l.Label("Satoru Gojo arrives as");
            if (l.RadioButton("Young Gojo", Settings.youngGojo, 0f,
                    "A 17-year-old student at psycast level 8 with Infinity, Blue, Blue Fist, Speed, Black Flash and Cursed Energy Reinforcement. "
                    + "You unlock the rest as he levels up.")) Settings.youngGojo = true;
            if (l.RadioButton("Adult Gojo", !Settings.youngGojo, 0f,
                    "The strongest sorcerer: psycast level 20 with every Limitless technique.")) Settings.youngGojo = false;
            l.GapLine();
            l.Label("Six Eyes look", -1f, (TipSignal?)"The light-blue eyes drawn on anyone with the Six Eyes. Only the look: the Six Eyes powers work either way.");
            bool fa = GojoPsycasts.SixEyesLook.FacialAnimation;
            if (l.RadioButton("Automatic" + (fa ? " (Facial Animation is on: its own eyes are coloured light blue)" : ""), Settings.sixEyes == GojoPsycasts.EyesMode.Auto, 0f,
                    "Light-blue eyes either way. Without Facial Animation this mod draws them; with it, Facial Animation's own eyes are "
                    + "coloured light blue instead, so they still blink and line up.")) Settings.sixEyes = GojoPsycasts.EyesMode.Auto;
            if (l.RadioButton("Always draw this mod's eyes", Settings.sixEyes == GojoPsycasts.EyesMode.On, 0f,
                    "Draw this mod's eye pictures even with Facial Animation (they may not line up with its eyes).")) Settings.sixEyes = GojoPsycasts.EyesMode.On;
            if (l.RadioButton("Never draw this mod's eyes", Settings.sixEyes == GojoPsycasts.EyesMode.Off, 0f,
                    "Don't draw this mod's eye pictures. With Facial Animation, its eyes are still coloured light blue.")) Settings.sixEyes = GojoPsycasts.EyesMode.Off;
            l.GapLine();
            l.Label("Maki Zenin arrives as");
            if (l.RadioButton("Before her awakening", Settings.makiForm == 1, 0f,
                "Her hair tied back, her glasses, an incomplete Heavenly Restriction (a tiny bit of cursed energy, a strong body), Playful Cloud.")) Settings.makiForm = 1;
            if (l.RadioButton("Awakened", Settings.makiForm == 2, 0f,
                "Short hair and burn scars, Heavenly Restriction: zero cursed energy, Dragon-Bone and the Split Soul Katana together.")) Settings.makiForm = 2;
            l.GapLine();
            l.Label("Finger and painting pace");
            if (l.RadioButton("Slow", Settings.relicPace == 0, 0f,
                "One finger or Death Painting per quest site, and rumours of them half as often.")) Settings.relicPace = 0;
            if (l.RadioButton("Normal", Settings.relicPace == 1, 0f,
                "Two to four per quest site, and rumours at the usual pace (a finger rumour every 25 to 40 days, a painting rumour every 18 to 28).")) Settings.relicPace = 1;
            if (l.RadioButton("Fast", Settings.relicPace == 2, 0f,
                "Three to six per quest site, and rumours half again as often.")) Settings.relicPace = 2;
            viewHeight = Mathf.Max(rect.height, l.CurHeight + 20f);
            l.End();
            Widgets.EndScrollView();
        }

        private static void YujiStageOption(Listing_Standard l, int stage, string label, string tip)
        {
            if (l.RadioButton(label, Settings.yujiStage == stage, 0f, tip)) Settings.yujiStage = stage;
        }

        private static void YutaStage(Listing_Standard l, int stage, string label, string tip)
        {
            if (l.RadioButton(label, Settings.yutaStage == stage, 0f, tip)) Settings.yutaStage = stage;
        }

        private static void Option(Listing_Standard l, IconStyle style, string label, string tip)
        {
            if (l.RadioButton(label, Settings.icons == style, 0f, tip) && Settings.icons != style)
            {
                Settings.icons = style;
                AbilityIcons.Apply();
                GojoPsycasts.AbilityIcons.Apply();     // Gojo's and Sukuna's icons follow the same setting
                SukunaPsycasts.AbilityIcons.Apply();
            }
        }
    }

    /// <summary>Swaps ability icons for kanji. Kanji textures sit in a Kanji folder next to the
    /// picture, under the same name. Other mods add their own def prefixes and letter icons.</summary>
    [StaticConstructorOnStartup]
    public static class AbilityIcons
    {
        public static readonly List<string> Prefixes = new List<string> { "JJK_" };
        /// <summary>Icons that are only a letter (kanji replace them in the middle setting).</summary>
        public static readonly HashSet<string> LetterIcons = new HashSet<string>
        {
            "Reinforcement", "ReverseCursedTechnique", "PositiveEnergy", "Blast", "Batto", "EveningMoon", "HazyMoon",
            "BoogieWoogie", "BoogieWoogieFlurry", "BoogieWoogieDeflect", "GraniteBlast", "GraniteRapidFire", "RyuDomain",
        };

        static AbilityIcons() => Apply();

        public static void Apply()
        {
            var style = JujutsuMod.Settings?.icons ?? IconStyle.Pictures;
            foreach (var def in DefDatabase<VEFAbilityDef>.AllDefs)
            {
                if (def.iconPath.NullOrEmpty() || !Prefixes.Any(p => def.defName.StartsWith(p))) continue;
                int slash = def.iconPath.LastIndexOf('/');
                string name = def.iconPath.Substring(slash + 1);
                bool kanji = style == IconStyle.KanjiForAll || (style == IconStyle.KanjiForLetters && LetterIcons.Contains(name));
                Texture2D tex = null;
                if (kanji) tex = ContentFinder<Texture2D>.Get(def.iconPath.Substring(0, slash) + "/Kanji/" + name, false);
                def.icon = tex ?? ContentFinder<Texture2D>.Get(def.iconPath, false) ?? def.icon;
            }
        }
    }

    /// <summary>Plays a sound at a cell.</summary>
    public static class Snd
    {
        public static void At(string defName, IntVec3 cell, Map map)
        {
            if (map == null) return;
            DefDatabase<SoundDef>.GetNamedSilentFail(defName)?.PlayOneShot(new TargetInfo(cell, map));
        }
    }

    /// <summary>Sorcerers study anomalies better: Anomaly's study efficiency x(1 + half their power).
    /// Added to the stat at startup, so the mod doesn't need Anomaly.</summary>
    public class StatPart_Sorcerer : StatPart
    {
        public const float PerPower = 0.5f;

        public static void Register()
        {
            var stat = DefDatabase<StatDef>.GetNamedSilentFail("StudyEfficiency");
            if (stat == null) return;
            if (stat.parts == null) stat.parts = new List<StatPart>();
            if (stat.parts.Exists(x => x.GetType().Name == nameof(StatPart_Sorcerer))) return;
            stat.parts.Add(new StatPart_Sorcerer { parentStat = stat });
        }

        private static float Factor(StatRequest req) =>
            req.Thing is Pawn p && Jujutsu.IsSorcerer(p) ? 1f + PerPower * Mathf.Max(0f, Jujutsu.Power(p)) : 1f;

        public override void TransformValue(StatRequest req, ref float val) => val *= Factor(req);

        public override string ExplanationPart(StatRequest req)
        {
            float f = Factor(req);
            return f > 1f ? "Sorcerer (cursed energy): x" + f.ToString("0.00") : null;
        }
    }

    /// <summary>The characters' own ideoligions. Each is made once per game with exactly the chosen
    /// memes: the game may add up to two random memes of its own, so it is regenerated until none are.</summary>
    public class CharacterIdeos : GameComponent
    {
        private static Dictionary<string, Ideo> made = new Dictionary<string, Ideo>();
        private List<string> keysWork; private List<Ideo> valsWork;

        public CharacterIdeos(Game game) { made = new Dictionary<string, Ideo>(); }

        /// <summary>The Christian structure's deity names are few, and making these ideoligions takes many tries: they ran
        /// out and the game logged an error (test runner 2026-11-12). Left out of the characters' ideoligions.</summary>
        private static System.Collections.Generic.List<MemeDef> NoChristian()
        {
            var m = DefDatabase<MemeDef>.GetNamedSilentFail("Structure_OriginChristian");
            return m == null ? null : new System.Collections.Generic.List<MemeDef> { m };
        }

        public static Ideo Get(string key, FactionDef forFaction, params string[] memeNames)
        {
            if (!ModsConfig.IdeologyActive || Find.IdeoManager == null) return null;
            if (made.TryGetValue(key, out var have) && have != null && Find.IdeoManager.IdeosListForReading.Contains(have)) return have;
            var forced = memeNames.Select(n => DefDatabase<MemeDef>.GetNamedSilentFail(n)).Where(m => m != null).ToList();
            if (forced.Count == 0) return null;
            Ideo best = null;
            for (int i = 0; i < 40; i++)
            {
                Ideo ideo;
                try { ideo = IdeoGenerator.GenerateIdeo(new IdeoGenerationParms(forFaction, disallowedMemes: NoChristian(), forcedMemes: forced)); }
                catch (System.Exception e) { Log.Warning("[JJK Core] ideoligion generation failed: " + e.Message); continue; }
                if (ideo == null) continue;
                bool all = forced.All(m => ideo.memes.Contains(m));
                bool extra = ideo.memes.Any(m => m.category != MemeCategory.Structure && !forced.Contains(m));
                if (all && !extra) { best = ideo; break; }
                if (best == null && all) best = ideo;
            }
            if (best == null) return null;
            Find.IdeoManager.Add(best);
            made[key] = best;
            return best;
        }

        public static void Give(Pawn p, Ideo ideo)
        {
            ideo = JujutsuFundamentals.IdeoJoin.Pick(p, ideo);
            if (p?.ideo == null || ideo == null || p.Ideo == ideo) return;
            p.ideo.SetIdeo(ideo);
        }

        public static bool Has(Ideo i) => i != null && made.ContainsValue(i);

        public override void ExposeData()
        {
            base.ExposeData();
            // an ideoligion the game has since removed (nobody follows it): forget it rather than save a dead reference
            if (Scribe.mode == LoadSaveMode.Saving && made != null)
                foreach (var k in made.Keys.ToList())
                    if (made[k] == null || Find.IdeoManager == null || !Find.IdeoManager.IdeosListForReading.Contains(made[k])) made.Remove(k);
            Scribe_Collections.Look(ref made, "jujutsuCharacterIdeos", LookMode.Value, LookMode.Reference, ref keysWork, ref valsWork);
            if (made == null) made = new Dictionary<string, Ideo>();
        }
    }

    /// <summary>The ideoligion option (JujutsuSettings.joinColonyIdeo).</summary>
    public static class IdeoJoin
    {
        public static bool On => JujutsuMod.Settings?.joinColonyIdeo == true;

        public static bool IsCharacterIdeo(Ideo i) =>
            CharacterIdeos.Has(i) || GojoPsycasts.CharacterIdeos.Has(i) || SukunaPsycasts.CharacterIdeos.Has(i);

        /// <summary>The colony's main ideoligion instead of <paramref name="ideo"/> when the option is on and the pawn is ours.</summary>
        public static Ideo Pick(Pawn p, Ideo ideo)
        {
            if (!On || p?.Faction == null || !p.Faction.IsPlayer) return ideo;
            return Faction.OfPlayer?.ideos?.PrimaryIdeo ?? ideo;
        }
    }

    /// <summary>A character joins the colony with the option on: their own ideoligion gives way to the colony's.</summary>
    [HarmonyLib.HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    public static class Patch_IdeoJoin
    {
        public static void Postfix(Pawn __instance, Faction newFaction)
        {
            if (!IdeoJoin.On || newFaction == null || !newFaction.IsPlayer || __instance?.ideo == null) return;
            if (!IdeoJoin.IsCharacterIdeo(__instance.Ideo)) return;
            var main = newFaction.ideos?.PrimaryIdeo;
            if (main != null && main != __instance.Ideo) __instance.ideo.SetIdeo(main);
        }
    }
}
