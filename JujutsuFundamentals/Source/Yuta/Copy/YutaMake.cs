using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class YutaCopyDefOf
    {
        public static IncidentDef JJK_YutaAsks;
        public static HediffDef JJK_RctOutputHealing, JJK_MutualLoveDomain, JJK_MutualLoveMark;
        public static ThingDef JJK_MutualLoveKatana;
        static YutaCopyDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(YutaCopyDefOf));
    }

    /// <summary>The Copy part's own saved state.</summary>
    public class YutaCopyState : GameComponent
    {
        public static YutaCopyState Instance;
        /// <summary>The stage Yuta was made at (1–4), 0 if not made yet.</summary>
        public int madeStage;
        /// <summary>He came asking the colony to help unravel Rika's curse (stage 1).</summary>
        public bool cameForHelp;
        /// <summary>Sukuna fingers Rika has eaten, held inside Yuta (FingersHeld).</summary>
        public int fingersHeld;

        public YutaCopyState(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Values.Look(ref madeStage, "yutaMadeStage");
            Scribe_Values.Look(ref cameForHelp, "yutaCameForHelp");
            Scribe_Values.Look(ref fingersHeld, "yutaFingersHeld");
        }
    }

    public static partial class Yuta
    {
        // Canon: born 7 March 2001; JJK 0 is late 2017 (first year, 16), the Shinjuku Showdown is December 2018 (17).
        // The adult stage has no canon age: 25 is a placeholder (question for her).
        public static float AgeAt(int stage) => stage >= 4 ? 25f : stage == 3 ? 17f : 16f;
        public static int LevelAt(int stage) => stage >= 4 ? 20 : stage == 3 ? 16 : stage == 2 ? 8 : 1;   // proposal
        private static int MeleeAt(int stage) => stage >= 4 ? 20 : stage == 3 ? 16 : stage == 2 ? 10 : 4;   // proposal

        private static readonly Color White = new Color(0.94f, 0.94f, 0.92f);

        /// <summary>Makes Yuta at a stage (1 start of JJK 0, 2 end of JJK 0, 3 Shinjuku, 4 adult) and records him
        /// as the game's Yuta before any of his techniques are given (the core refuses Yuta-only ones otherwise).</summary>
        public static Pawn Make(int stage)
        {
            stage = Mathf.Clamp(stage, 1, 4);
            var t = YutaTracker.Instance;
            if (t == null) return null;
            float age = AgeAt(stage);
            var p = Characters.Make("Yuta", "Yuta", "Okkotsu", Gender.Male, age);
            t.yuta = p;
            t.yutaAdult = stage == 4;
            if (YutaCopyState.Instance != null) YutaCopyState.Instance.madeStage = stage;

            Characters.Skill(p, SkillDefOf.Melee, MeleeAt(stage), Passion.Major);
            Look(p);

            var psy = Characters.Psycaster(p, LevelAt(stage));
            var yutaPath = YutaDefOf.JJK_YutaPath;
            var speech = YutaDefOf.JJK_CursedSpeech;
            var fund = JJKDefOf.JJK_CursedEnergyFundamentals;
            if (psy != null && yutaPath != null && !psy.unlockedPaths.Contains(yutaPath)) psy.unlockedPaths.Add(yutaPath);
            switch (stage)
            {
                case 1:
                    // nothing learned, and no point to spend yet (proposal): Manifest Rika comes with his first level
                    if (psy != null) psy.points = 0;
                    break;
                case 2:
                    // her call 2026-11-11: stage 2 and on has Black Flash and Cursed Energy Blast too
                    LearnSome(p, fund, JJKAbilityDefOf.JJK_Reinforcement, JJKAbilityDefOf.JJK_ReverseCursedTechnique,
                              JJKAbilityDefOf.JJK_Blast, JJKAbilityDefOf.JJK_BlackFlash);
                    LearnSome(p, yutaPath, Defs(YutaAbilities.ManifestRika, YutaAbilities.RikaArm, YutaAbilities.RikaBite,
                                                YutaAbilities.RikaGrapple, YutaAbilities.Copy, YutaAbilities.RctOutput));
                    // the one command he says himself in JJK 0: "Die", through Rika's megaphone, on Geto's curses
                    if (speech != null) LearnSome(p, speech, Defs(YutaAbilities.Die));
                    break;
                case 3:
                    Characters.Learn(p, fund);
                    if (yutaPath != null)
                        LearnSome(p, yutaPath, yutaPath.abilities.Where(a => a.defName != YutaAbilities.Paste && a.defName != YutaAbilities.Flight && !JacobsLadder.Is(a)).ToArray());
                    if (speech != null) Characters.Learn(p, speech);
                    break;
                default:
                    Characters.Learn(p, fund);
                    if (yutaPath != null) Characters.Learn(p, yutaPath);
                    if (speech != null) Characters.Learn(p, speech);
                    break;
            }

            Arm(p);
            // the good guys share one ideoligion (coordinator's pick 2026-11-11)
            CharacterIdeos.Give(p, CharacterIdeos.Get("jujutsu", FactionDefOf.PlayerColony, "Collectivist"));
            VastCursedEnergy.Sync(p);
            PassiveSync.Sync(p);
            Looks.Refresh(p);
            return p;
        }

        private static VEFAbilityDef[] Defs(params string[] names) => names.Select(YutaAbilities.Def).Where(d => d != null).ToArray();

        /// <summary>Characters.Learn with an empty list learns the whole path, so an empty list (every def missing)
        /// only unlocks it here.</summary>
        private static void LearnSome(Pawn p, PsycasterPathDef path, params VEFAbilityDef[] defs)
        {
            if (path == null) return;
            defs = defs.Where(d => d != null).ToArray();
            if (defs.Length > 0) { Characters.Learn(p, path, defs); return; }
            var psy = Characters.Psycaster(p, 1);
            if (psy != null && !psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
        }

        /// <summary>Black hair (vanilla Mop, the closest to his), the white Jujutsu High top with black trousers
        /// (the Sega figure in yuta-images/yuta_ring.jpg; vanilla collar shirt and jacket in white, trousers in black), his ring. Childhood and traits are left as
        /// generated except Kind and psychically hypersensitive (her call 2026-11-11).</summary>
        private static void Look(Pawn p)
        {
            if (p.story != null)
            {
                if (p.genes != null)
                    foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null || g.def.skinColorOverride != null).ToList())
                        p.genes.RemoveGene(g);
                p.story.bodyType = BodyTypeDefOf.Thin;
                p.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail("Mop") ?? p.story.hairDef;
                p.story.HairColor = Looks.Black;
                p.story.skinColorOverride = Looks.Skin;
                // her call 2026-11-11: jujutsu students are always the childhood; sorcerer as an adult
                p.story.Childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_JujutsuStudent") ?? p.story.Childhood;
                p.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail("JJK_Sorcerer") ?? p.story.Adulthood;
                // Kind (proposal from the sources: warm and considerate to his friends); the other slot is left as rolled
                var kind = DefDatabase<TraitDef>.GetNamedSilentFail("Kind");
                if (kind != null && p.story.traits != null && !p.story.traits.HasTrait(kind))
                {
                    if (p.story.traits.allTraits.Count >= 3) p.story.traits.RemoveTrait(p.story.traits.allTraits.Last());
                    p.story.traits.GainTrait(new Trait(kind, 0, true));
                }
                var sens = DefDatabase<TraitDef>.GetNamedSilentFail("PsychicSensitivity");
                if (sens != null && p.story.traits != null)
                {
                    foreach (var tr in p.story.traits.allTraits.Where(x => x.def == sens).ToList()) p.story.traits.RemoveTrait(tr);
                    p.story.traits.GainTrait(new Trait(sens, 2, true));   // psychically hypersensitive: her call 2026-11-11
                }
            }
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
            if (p.apparel == null) return;
            p.apparel.DestroyAll();
            Wear(p, DefDatabase<ThingDef>.GetNamedSilentFail("JJK_YutaShirt") != null ? "JJK_YutaShirt" : "Apparel_CollarShirt", Color.white);   // his dark trousers are drawn on it (2026-11-12)
            Wear(p, "Apparel_Jacket", White);
            Wear(p, "Apparel_Pants", Looks.Black);
            var ring = JJKToolDefOf2.JJK_YutaRing;
            if (ring != null) p.apparel.Wear((Apparel)ThingMaker.MakeThing(ring), false, true);
        }

        private static void Wear(Pawn p, string defName, Color color)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            var stuff = def.MadeFromStuff ? (DefDatabase<ThingDef>.GetNamedSilentFail("Cloth") ?? GenStuff.DefaultStuffFor(def)) : null;
            var a = (Apparel)ThingMaker.MakeThing(def, stuff);
            a.SetColor(color, false);
            p.apparel.Wear(a, false, true);
        }

        /// <summary>He fights with ordinary katanas; vanilla's longsword is the closest.</summary>
        private static void Arm(Pawn p)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("MeleeWeapon_LongSword");
            if (def == null || p.equipment == null) return;
            p.equipment.DestroyAllEquipment();
            p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(def, ThingDefOf.Steel));
        }
    }

    /// <summary>
    /// Yuta's arrival (her calls 2026-11-11). He asks to join: accept and he joins; decline and he leaves on good
    /// terms and may ask again later. At stage 1 (start of JJK 0) he comes asking for help unravelling Rika's curse,
    /// meaning: train him in psycasts; no friends needed (canon: Gojo took him in before he had any). At stages 2–4
    /// he only comes when one of his friends is already a colonist, and the letter names them.
    /// </summary>
    public static class YutaArrival
    {
        /// <summary>Who counts as Yuta's friend. One line adds another (Miguel, Megumi, Nobara, Panda...).</summary>
        public static readonly List<Func<Pawn, bool>> YutaFriends = new List<Func<Pawn, bool>>
        {
            p => SukunaPsycasts.Yuji.Is(p) || SukunaPsycasts.ModuloYuji.Is(p),
            RestrictedTracker.IsMaki,
            Inumaki.Is,
            CharacterTracker.IsTodo,
            CharacterTracker.IsMiwa,
            GojoPsycasts.Satoru.IsGojo,
        };

        public static bool IsFriend(Pawn p) => p != null && YutaFriends.Any(f => { try { return f(p); } catch { return false; } });

        /// <summary>His friends who are alive colonists on a home map.</summary>
        public static List<Pawn> FriendsHere()
        {
            var list = new List<Pawn>();
            foreach (var m in Find.Maps)
            {
                if (!m.IsPlayerHome) continue;
                foreach (var p in m.mapPawns.FreeColonistsSpawned)
                    if (!p.Dead && IsFriend(p) && !list.Contains(p)) list.Add(p);
            }
            return list;
        }

        /// <summary>The stage he comes at: the one he was made at, or the setting if he isn't made yet.</summary>
        public static int Stage => YutaCopyState.Instance?.madeStage > 0 ? YutaCopyState.Instance.madeStage : Yuta.StartStage;

        public static bool CanAsk(bool ignoreFriends)
        {
            var t = YutaTracker.Instance;
            if (t == null || t.yutaArrived) return false;
            if (t.yuta != null && (t.yuta.Dead || t.yuta.Spawned)) return false;
            return ignoreFriends || Stage == 1 || FriendsHere().Count > 0;
        }

        public static bool Ask(Map map)
        {
            var t = YutaTracker.Instance;
            if (t == null || map == null) return false;
            if (t.yuta == null) Yuta.Make(Yuta.StartStage);
            var yuta = t.yuta;
            if (yuta == null) return false;
            string text;
            if (Stage == 1)
                text = "Yuta Okkotsu, a boy haunted by the curse of Rika Orimoto, asks to join your colony. He wants help unravelling the curse: "
                     + "Rika acts on her own whenever he is in danger, and he can't control her. Training him in psycasts until he learns his "
                     + "domain will unravel it.\n\nAccept, and he joins. Decline, and he leaves on good terms; he may ask again later.";
            else
            {
                var friends = FriendsHere();
                string names = friends.Count == 0 ? "his friends" : GenText.ToCommaList(friends.Select(f => f.LabelShort), true);
                text = $"Yuta Okkotsu has come looking for {names}, and asks to join your colony.\n\n"
                     + "Accept, and he joins. Decline, and he leaves on good terms; he may ask again later.";
            }
            var letter = (ChoiceLetter_YutaAsks)LetterMaker.MakeLetter("Yuta Okkotsu asks to join", text, YutaCopyDefOf.JJK_YutaAsks.letterDef);
            letter.map = map;
            var look = friendsTarget(map);
            if (look.IsValid) letter.lookTargets = new LookTargets(look);
            letter.StartTimeout(GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }

        private static TargetInfo friendsTarget(Map map)
        {
            var f = FriendsHere().FirstOrDefault(p => p.Map == map);
            return f != null ? new TargetInfo(f) : TargetInfo.Invalid;
        }

        public static void Accept(Map map)
        {
            var t = YutaTracker.Instance;
            if (t?.yuta == null || t.yutaArrived || t.yuta.Dead) return;
            // the letter's map can be gone by the time it's answered (abandoned, or a postponed letter): any home map
            if (map == null || !Find.Maps.Contains(map)) map = Find.AnyPlayerHomeMap;
            if (map == null) return;   // nowhere to arrive: not marked arrived, so he can ask again
            t.yutaArrived = true;
            if (YutaCopyState.Instance != null && YutaCopyState.Instance.madeStage == 1) YutaCopyState.Instance.cameForHelp = true;
            Characters.WalkInAndJoin(t.yuta, map);
            Messages.Message("Yuta Okkotsu joins your colony.", t.yuta, MessageTypeDefOf.PositiveEvent, false);
        }
    }

    public class ChoiceLetter_YutaAsks : ChoiceLetter
    {
        public Map map;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) { yield return Option_Close; yield break; }
                yield return new DiaOption("Accept")
                {
                    action = () => { YutaArrival.Accept(map); Find.LetterStack.RemoveLetter(this); },
                    resolveTree = true,
                };
                yield return new DiaOption("Decline")
                {
                    action = () =>
                    {
                        Messages.Message("Yuta Okkotsu leaves on good terms.", MessageTypeDefOf.NeutralEvent, false);
                        Find.LetterStack.RemoveLetter(this);
                    },
                    resolveTree = true,
                };
                yield return Option_Postpone;
            }
        }

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && YutaTracker.Instance != null && !YutaTracker.Instance.yutaArrived;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
        }
    }

    public class IncidentWorker_YutaAsks : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms) => parms.target is Map && YutaArrival.CanAsk(false);

        protected override bool TryExecuteWorker(IncidentParms parms) => YutaArrival.Ask((Map)parms.target);
    }
}
