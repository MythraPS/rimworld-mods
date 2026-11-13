using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using LudeonTK;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Maki's two forms (her call 2026-11-11: "Distinguish the two forms").
    ///  - Awakened (Restricted.MakeMaki): short hair, burn scars, Heavenly Restriction: zero cursed energy, Dragon-Bone
    ///    and the Split Soul Katana dual wielded.
    ///  - Before awakening (MakeYoung): ponytail (canon), her glasses, no scars, the incomplete restriction (a tiny bit of
    ///    cursed energy, about half the body; sure hits reach her), Playful Cloud, with her naginata carried as well,
    ///    no dual wielding.
    /// The "Maki arrives as" setting picks which one is made.
    /// </summary>
    public static class MakiForms
    {
        public const int BeforeAwakening = 1, Awakened = 2;

        public static int Setting => JujutsuMod.Settings?.makiForm == BeforeAwakening ? BeforeAwakening : Awakened;

        public static TraitDef Partial => DefDatabase<TraitDef>.GetNamedSilentFail("JJK_HeavenlyRestrictionPartial");

        public static bool IsAwakened(Pawn p) => RestrictedTracker.IsMaki(p) && ZeroCursedEnergy.Restricted(p);

        public static Pawn Make() => Make(Setting);

        public static Pawn Make(int form) => form == BeforeAwakening ? MakeYoung() : Restricted.MakeMaki();

        /// <summary>Maki before her awakening. Skills a step below the awakened Maki's (proposal); age 16, a second-year.</summary>
        public static Pawn MakeYoung()
        {
            var p = Characters.Make("Maki", "Maki", "Zenin", Gender.Female, 16f);
            Characters.Skill(p, SkillDefOf.Melee, 14, Passion.Major);
            Characters.Skill(p, SkillDefOf.Shooting, 8, Passion.Minor);
            Looks.MakiYoung(p);
            if (p.equipment != null && JJKToolDefOf.JJK_PlayfulCloud != null)
            {
                p.equipment.DestroyAllEquipment();
                p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(JJKToolDefOf.JJK_PlayfulCloud));
            }
            // her naginata, carried as well (her call 2026-11-12)
            var nag = DefDatabase<ThingDef>.GetNamedSilentFail("JJK_Naginata");
            if (nag != null && p.inventory != null) p.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(nag));
            Ability_Leap.Give(p);   // her call 2026-11-11 (Maki leaps)
            PassiveSync.Sync(p);
            CharacterIdeos.Give(p, CharacterIdeos.Get("jujutsu", FactionDefOf.PlayerColony, "Collectivist"));
            Looks.Refresh(p);
            return p;
        }
    }

    /// <summary>
    /// Maki's grudge against the Zen'in clan (her call 2026-11-11). A clan member: surname Zenin / Zen'in, or a Zen'in
    /// backstory, or anyone a later part adds to ZeninClan (Naoya, Naobito, Ogi...). Not Mai, Megumi, Tsumiki or Toji.
    /// </summary>
    public class ThoughtWorker_MakiZeninGrudge : ThoughtWorker
    {
        /// <summary>More ways to recognise a Zen'in (one line each in other parts).</summary>
        public static readonly List<Func<Pawn, bool>> ZeninClan = new List<Func<Pawn, bool>>();
        /// <summary>The Zen'in she doesn't hate.</summary>
        public static readonly List<Func<Pawn, bool>> Spared = new List<Func<Pawn, bool>> { RestrictedTracker.IsToji };
        private static readonly string[] SparedFirstNames = { "Mai", "Megumi", "Tsumiki", "Toji" };
        private static readonly string[] ZeninBackstories = { "JJK_ZeninOutcast" };

        public static bool IsZenin(Pawn p)
        {
            if (p?.story == null) return false;
            if (p.Name is NameTriple n && n.Last != null)
            {
                string last = n.Last.Replace("'", "").Replace("’", "").ToLowerInvariant();
                if (last == "zenin") return true;
            }
            if (ZeninBackstories.Contains(p.story.Childhood?.defName) || ZeninBackstories.Contains(p.story.Adulthood?.defName)) return true;
            return ZeninClan.Any(f => f(p));
        }

        public static bool IsSpared(Pawn p)
        {
            if (Spared.Any(f => f(p))) return true;
            return p.Name is NameTriple n && SparedFirstNames.Contains(n.First);
        }

        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (!RestrictedTracker.IsMaki(p) || other == null || other == p || other.Dead) return false;
            if (!other.RaceProps.Humanlike || !IsZenin(other) || IsSpared(other)) return false;
            return true;
        }
    }

    public static class MakiFormsDebug
    {
        private const string Cat = "Jujutsu Kaisen";

        [DebugAction(Cat, "Maki: spawn here, before awakening (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Young() => Spawn(MakiForms.BeforeAwakening);

        [DebugAction(Cat, "Maki: spawn here, awakened (joins)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Awake() => Spawn(MakiForms.Awakened);

        private static void Spawn(int form)
        {
            var tr = RestrictedTracker.Instance;
            if (tr == null) return;
            if (tr.maki != null && !tr.maki.Dead)
            {
                Messages.Message("There is already a Maki in this game. Use \"Toji/Maki: reset (forget both)\" first.", MessageTypeDefOf.RejectInput, false);
                return;
            }
            var p = MakiForms.Make(form);
            tr.maki = p;
            tr.makiArrived = true;
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            GenSpawn.Spawn(p, UI.MouseCell(), Find.CurrentMap);
            if (p.Faction != Faction.OfPlayer) p.SetFaction(Faction.OfPlayer);
            Messages.Message($"{p.LabelShort} is here and has joined.", p, MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
