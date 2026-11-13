using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// The characters' looks and traits (her ask 2026-11-10), from canon where it is known:
    /// Kusakabe: tall, spiky black hair (manga), dust-brown peacoat over a white shirt, black trousers.
    /// Miwa: long light-blue hair, the Kyoto uniform (dark jacket over a white shirt).
    /// Todo: big and muscular, black hair tied back in a spiky ponytail, scar from the left hairline to
    /// the cheek, shirtless, martial-arts trousers. Ryu: muscular, a tall pompadour, a fur jacket.
    /// Traits follow their personalities: Kusakabe a coward who protects his students (nervous, kind), and tough and a brawler (her call);
    /// Miwa kind and hard-working; Todo a brawler, tough; Ryu lives for a fight (bloodlust), fires in
    /// bursts (trigger-happy), the cool guy girls adore (pretty).
    /// </summary>
    public static class Looks
    {
        public static readonly Color Skin = new Color(0.94f, 0.83f, 0.73f);
        public static readonly Color Black = new Color(0.16f, 0.15f, 0.15f);

        public static void Kusakabe(Pawn p)
        {
            Base(p, BodyTypeDefOf.Male, "Rockstar", Black);
            Backstory(p, "JJK_JujutsuStudent", "JJK_JujutsuTeacher");
            Traits(p, ("Nerves", -1), ("Kind", 0), ("Tough", 0), ("Brawler", 0));   // tough + brawler: her call 2026-11-10
            Dress(p, ("Apparel_CollarShirt", "Cloth", new Color(0.92f, 0.92f, 0.9f)),
                     ("Apparel_Duster", "Cloth", new Color(0.59f, 0.46f, 0.32f)),
                     ("Apparel_Pants", "Cloth", new Color(0.15f, 0.15f, 0.16f)));
        }

        public static void Miwa(Pawn p)
        {
            Base(p, BodyTypeDefOf.Female, "Long", new Color(0.6f, 0.78f, 0.92f));
            Backstory(p, "JJK_JujutsuStudent", "JJK_Sorcerer");
            Traits(p, ("Kind", 0), ("Industriousness", 1), ("PsychicSensitivity", -1));   // a failure at sorcery: psychically dull (her call 2026-11-10)
            Dress(p, ("Apparel_CollarShirt", "Cloth", new Color(0.92f, 0.92f, 0.9f)),
                     ("Apparel_Jacket", "Cloth", new Color(0.16f, 0.19f, 0.31f)),
                     ("Apparel_Pants", "Cloth", new Color(0.16f, 0.19f, 0.31f)));
        }

        public static void Todo(Pawn p)
        {
            Base(p, BodyTypeDefOf.Hulk, "JJK_TodoHair", Black);
            Backstory(p, "JJK_JujutsuStudent", "JJK_Sorcerer");
            if (p.style != null) p.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("JJK_TodoScar") ?? p.style.FaceTattoo;
            // her calls: brawler + tough (2026-11-10); too smart, psychically sensitive, perfect memory, nimble, jogger, iron-willed (2026-11-11)
            Traits(p, ("Brawler", 0), ("Tough", 0), ("TooSmart", 0), ("PsychicSensitivity", 1), ("PerfectMemory", 0),
                   ("Nimble", 0), ("SpeedOffset", 2), ("Nerves", 2));
            Dress(p, ("Apparel_Pants", "Cloth", new Color(0.2f, 0.2f, 0.22f)));
        }

        public static void Ryu(Pawn p)
        {
            Base(p, BodyTypeDefOf.Hulk, "JJK_RyuHair", Black);
            Backstory(p, "YouthSoldier99", "JJK_Sorcerer");   // youth soldier (her call 2026-11-10): he was a warrior, not a student
            Traits(p, ("Bloodlust", 0), ("ShootingAccuracy", -1), ("Beauty", 1));
            Dress(p, ("Apparel_BasicShirt", "Cloth", new Color(0.16f, 0.16f, 0.17f)),
                     ("Apparel_Jacket", "Leather_Bear", null),
                     ("Apparel_Pants", "Cloth", new Color(0.16f, 0.16f, 0.17f)));
        }

        /// <summary>Toji (2026-11-10): short messy black hair, the scar at the corner of his mouth, a tight black shirt and
        /// loose white trousers. Greedy (he works for money), tough and jogger (her calls 2026-11-11).</summary>
        public static void Toji(Pawn p)
        {
            Base(p, BodyTypeDefOf.Male, "Messy", Black);
            Backstory(p, "JJK_ZeninOutcast", "Assassin20");
            if (p.style != null) p.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("JJK_TojiScar") ?? p.style.FaceTattoo;
            Traits(p, ("JJK_HeavenlyRestriction", 0), ("Greedy", 0), ("Tough", 0), ("SpeedOffset", 2));   // her calls 2026-11-11: greedy, tough, jogger
            Dress(p, ("Apparel_BasicShirt", "Cloth", new Color(0.12f, 0.12f, 0.13f)),
                     ("Apparel_Pants", "Cloth", new Color(0.9f, 0.9f, 0.88f)));
        }

        /// <summary>Awakened Maki (2026-11-10): short black hair, the Jujutsu High uniform. Burn scars still to draw once
        /// she says where. Second trait is a proposal: iron-willed.</summary>
        public static void Maki(Pawn p)
        {
            Base(p, BodyTypeDefOf.Female, "Bob", Black);
            Backstory(p, "JJK_ZeninOutcast", "JJK_Sorcerer");   // sorcerer as an adult (her call 2026-11-11)
            Traits(p, ("JJK_HeavenlyRestriction", 0), ("Tough", 0), ("Brawler", 0), ("SpeedOffset", 2));   // her calls 2026-11-11: tough, brawler, jogger
            // awakened Maki's burn scars (her ask 2026-11-11; Jogo's fire in Shibuya), up both arms and both sides of her face
            if (p.style != null)
            {
                p.style.FaceTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("JJK_MakiScarsFace") ?? p.style.FaceTattoo;
                p.style.BodyTattoo = DefDatabase<TattooDef>.GetNamedSilentFail("JJK_MakiScarsBody") ?? p.style.BodyTattoo;
            }
            // just the shirt and trousers, no jacket (her call 2026-11-12)
            Dress(p, ("Apparel_CollarShirt", "Cloth", new Color(0.92f, 0.92f, 0.9f)),
                     ("Apparel_Pants", "Cloth", new Color(0.12f, 0.13f, 0.18f)));
        }

        /// <summary>Maki before her awakening (her call 2026-11-11: "look like this with glasses", the two forms
        /// distinct): her hair tied back in a ponytail (canon), her glasses, no burn scars, the same uniform. Traits as
        /// the awakened Maki's but the incomplete restriction (tough, brawler, jogger: proposal, same girl).</summary>
        public static void MakiYoung(Pawn p)
        {
            Base(p, BodyTypeDefOf.Female, DefDatabase<HairDef>.GetNamedSilentFail("BriefPonytail") != null ? "BriefPonytail" : "Ponytails", Black);
            Backstory(p, "JJK_ZeninOutcast", "JJK_Sorcerer");
            Traits(p, ("JJK_HeavenlyRestrictionPartial", 0), ("Tough", 0), ("Brawler", 0), ("SpeedOffset", 2),
                   ("PsychicSensitivity", -1));   // psychically dull (her call 2026-11-12)
            Dress(p, ("Apparel_CollarShirt", "Cloth", new Color(0.92f, 0.92f, 0.9f)),
                     ("Apparel_Jacket", "Cloth", new Color(0.12f, 0.13f, 0.18f)),
                     ("Apparel_Pants", "Cloth", new Color(0.12f, 0.13f, 0.18f)),
                     ("JJK_MakiGlasses", null, null));
        }

        /// <summary>Uraume (her ask 2026-11-11): short white bob with a red streak (her call), pale skin, slim; a
        /// black robe over white (her call 2026-11-11). Vanilla Bob, robe, shirt and trousers, tinted.
        /// Heian childhood, sorcerer adulthood. Trait is a proposal from canon: Iron-willed (cold, unreadable).</summary>
        public static void Uraume(Pawn p)
        {
            Base(p, BodyTypeDefOf.Thin, "JJK_UraumeHair", new Color(0.95f, 0.95f, 0.96f));   // white bob, red streak drawn in
            if (p?.story != null) p.story.skinColorOverride = new Color(0.96f, 0.94f, 0.92f);
            Backstory(p, "JJK_HeianSorcererChild", "JJK_Sorcerer");
            Traits(p, ("Nerves", 2), ("Tough", 0), ("Brawler", 0), ("SpeedOffset", 2));   // iron-willed + her calls 2026-11-11: tough, brawler, jogger
            Dress(p, ("Apparel_BasicShirt", "Cloth", new Color(0.95f, 0.95f, 0.95f)),
                     ("Apparel_Pants", "Cloth", new Color(0.95f, 0.95f, 0.95f)),
                     ("Apparel_Robe", "Cloth", new Color(0.1f, 0.1f, 0.11f)));   // black robe over white (her call 2026-11-11; was deep blue)
        }

        /// <summary>The Angel, Hana Kurusu (her ask 2026-11-11; canon): a chin-length blonde bob with blunt bangs, an
        /// oversized tan sweater (vanilla shirt, tan) over black leggings (vanilla trousers, black). Kind (canon:
        /// polite and devoted) is a proposal. Her childhood is left as rolled: she's a modern girl, not a student.</summary>
        public static void Angel(Pawn p)
        {
            Base(p, BodyTypeDefOf.Thin, "Bob", new Color(0.93f, 0.8f, 0.48f));
            Backstory(p, "JJK_HomelessChild", "");   // her call 2026-11-12
            Traits(p, ("Kind", 0));
            Dress(p, ("Apparel_BasicShirt", "Cloth", new Color(0.78f, 0.66f, 0.5f)),
                     ("Apparel_Pants", "Cloth", new Color(0.1f, 0.1f, 0.11f)));
        }

        private static void Base(Pawn p, BodyTypeDef body, string hair, Color hairColor)
        {
            if (p?.story == null) return;
            if (p.genes != null)
                foreach (var g in p.genes.GenesListForReading.Where(g => g.def.hairColorOverride != null || g.def.skinColorOverride != null).ToList())
                    p.genes.RemoveGene(g);
            p.story.bodyType = body;
            p.story.hairDef = DefDatabase<HairDef>.GetNamedSilentFail(hair) ?? p.story.hairDef;
            p.story.HairColor = hairColor;
            p.story.skinColorOverride = Skin;
            if (p.style != null)
            {
                p.style.beardDef = BeardDefOf.NoBeard;
                p.style.FaceTattoo = TattooDefOf.NoTattoo_Face;
                p.style.BodyTattoo = TattooDefOf.NoTattoo_Body;
            }
        }

        /// <summary>Childhood and adulthood (her call 2026-11-10). Skills are set by the makers themselves.</summary>
        private static void Backstory(Pawn p, string child, string adult)
        {
            if (p?.story == null) return;
            p.story.Childhood = DefDatabase<BackstoryDef>.GetNamedSilentFail(child) ?? p.story.Childhood;
            p.story.Adulthood = DefDatabase<BackstoryDef>.GetNamedSilentFail(adult) ?? p.story.Adulthood;
        }

        private static void Traits(Pawn p, params (string def, int degree)[] list)
        {
            if (p?.story?.traits == null) return;
            p.story.traits.allTraits.Clear();
            foreach (var e in list)
            {
                var t = DefDatabase<TraitDef>.GetNamedSilentFail(e.Item1);
                if (t != null) p.story.traits.GainTrait(new Trait(t, e.Item2, true));
            }
        }

        private static void Dress(Pawn p, params (string def, string stuff, Color? color)[] list)
        {
            if (p?.apparel == null) return;
            p.apparel.DestroyAll();
            foreach (var e in list)
            {
                string defName = e.Item1, stuffName = e.Item2;
                var color = e.Item3;
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                if (def == null) continue;
                var stuff = def.MadeFromStuff ? (DefDatabase<ThingDef>.GetNamedSilentFail(stuffName) ?? GenStuff.DefaultStuffFor(def)) : null;
                var a = (Apparel)ThingMaker.MakeThing(def, stuff);
                if (color.HasValue) a.SetColor(color.Value, false);
                p.apparel.Wear(a, false, true);
            }
        }

        public static void Refresh(Pawn p)
        {
            p.Notify_DisabledWorkTypesChanged();
            p.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(p);
        }
    }
}
