using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class JJKRestrictedDefOf
    {
        public static IncidentDef JJK_TojiOffers, JJK_MakiJoins;
        static JJKRestrictedDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKRestrictedDefOf));
    }

    /// <summary>
    /// Toji Fushiguro and awakened Maki Zenin (her calls 2026-11-10): both have Heavenly Restriction. Toji offers his
    /// services for silver (hire him); Maki turns up and joins like a wanderer. Which Maki (before her awakening or
    /// awakened) is a setting (MakiForms, her call 2026-11-11).
    /// </summary>
    public class RestrictedTracker : GameComponent
    {
        public static RestrictedTracker Instance;
        public Pawn toji, maki;
        public bool tojiHired, makiArrived;
        /// <summary>An awakened Maki made before 2026-11-11 got her off-hand katana and scars on load (once).</summary>
        public bool makiScarred;

        public RestrictedTracker(Game game) { Instance = this; }

        /// <summary>Her report 2026-11-11: an existing awakened Maki lacked the katana and the scars. Once per save.</summary>
        public override void LoadedGame()
        {
            base.LoadedGame();
            if (makiScarred || maki == null || maki.Dead || !MakiForms.IsAwakened(maki)) return;
            makiScarred = true;
            bool hasKatana = maki.equipment?.Primary?.def == JJKToolDefOf.JJK_SplitSoulKatana
                             || maki.inventory?.innerContainer.Any(t => t.def == JJKToolDefOf.JJK_SplitSoulKatana) == true;
            if (!hasKatana) DualWield.GiveOffHand(maki, JJKToolDefOf.JJK_SplitSoulKatana);
            if (!maki.health.hediffSet.hediffs.Any(h => h.def == MakiScars.BurnDef && h.IsPermanent())) MakiScars.Give(maki);
        }

        public static bool IsToji(Pawn p) => p != null && Instance?.toji == p;
        public static bool IsMaki(Pawn p) => p != null && Instance?.maki == p;

        public Pawn Toji => toji ?? (toji = Restricted.MakeToji());
        public Pawn Maki => maki ?? (maki = MakiForms.Make());   // the form the "Maki arrives as" setting picks

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref toji, "toji", true);
            Scribe_References.Look(ref maki, "maki", true);
            Scribe_Values.Look(ref tojiHired, "tojiHired");
            Scribe_Values.Look(ref makiArrived, "makiArrived");
            Scribe_Values.Look(ref makiScarred, "makiScarred");
        }
    }

    public static class Restricted
    {
        public const int TojiPrice = 2000;   // proposal

        public static Pawn MakeToji()
        {
            var p = Characters.Make("Toji", "Toji", "Fushiguro", Gender.Male, null);
            Characters.Skill(p, SkillDefOf.Melee, 20, Passion.Major);
            Characters.Skill(p, SkillDefOf.Shooting, 12, Passion.Minor);
            Looks.Toji(p);
            Arm(p, JJKToolDefOf.JJK_InvertedSpear);
            Ability_Leap.Give(p);   // her call 2026-11-11
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): his own, a lone mercenary
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("toji", RimWorld.FactionDefOf.PlayerColony, "Individualist"));
            Looks.Refresh(p);
            return p;
        }

        public static Pawn MakeMaki()
        {
            var p = Characters.Make("Maki", "Maki", "Zenin", Gender.Female, 17f);
            Characters.Skill(p, SkillDefOf.Melee, 18, Passion.Major);
            Characters.Skill(p, SkillDefOf.Shooting, 10, Passion.Minor);
            Looks.Maki(p);
            Arm(p, JJKToolDefOf.JJK_DragonBone);
            DualWield.GiveOffHand(p, JJKToolDefOf.JJK_SplitSoulKatana);   // her call 2026-11-11: she dual wields both
            MakiScars.Give(p);                                            // covered in burn scars (her call 2026-11-11)
            if (RestrictedTracker.Instance != null) RestrictedTracker.Instance.makiScarred = true;
            Ability_Leap.Give(p);   // her call 2026-11-11
            PassiveSync.Sync(p);
            // ideoligion (my pick 2026-11-11, she adjusts after testing): the good guys share one
            JujutsuFundamentals.CharacterIdeos.Give(p, JujutsuFundamentals.CharacterIdeos.Get("jujutsu", RimWorld.FactionDefOf.PlayerColony, "Collectivist"));
            Looks.Refresh(p);
            return p;
        }

        private static void Arm(Pawn p, ThingDef weapon)
        {
            if (p.equipment == null || weapon == null) return;
            p.equipment.DestroyAllEquipment();
            p.equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(weapon));
        }

        /// <summary>Silver the colony can pay with on this map: unforbidden stacks lying on the map.</summary>
        public static List<Thing> Silver(Map map) =>
            map.listerThings.ThingsOfDef(ThingDefOf.Silver).Where(t => t.Spawned && !t.IsForbidden(Faction.OfPlayer)).ToList();

        public static int SilverCount(Map map) => Silver(map).Sum(t => t.stackCount);

        public static bool Pay(Map map, int amount)
        {
            if (SilverCount(map) < amount) return false;
            foreach (var t in Silver(map))
            {
                if (amount <= 0) break;
                int take = Mathf.Min(amount, t.stackCount);
                t.SplitOff(take).Destroy();
                amount -= take;
            }
            return true;
        }

        public static void HireToji(Map map)
        {
            var tr = RestrictedTracker.Instance;
            if (tr == null || tr.tojiHired) return;
            // the letter's map may be gone by the time she pays (abandoned, or the letter was postponed)
            if (map == null || !Find.Maps.Contains(map)) map = Find.AnyPlayerHomeMap;
            if (map == null) { Messages.Message("No home map for Toji to come to.", MessageTypeDefOf.RejectInput, false); return; }
            if (!Pay(map, TojiPrice)) { Messages.Message("Not enough silver.", MessageTypeDefOf.RejectInput, false); return; }
            tr.tojiHired = true;
            Characters.WalkInAndJoin(tr.Toji, map);
            Find.LetterStack.ReceiveLetter("Toji Fushiguro hired", "Toji Fushiguro has been paid and joins your colony.", LetterDefOf.PositiveEvent, tr.Toji);
        }

        public static void MakiJoins(Map map)
        {
            var tr = RestrictedTracker.Instance;
            if (tr == null || tr.makiArrived) return;
            tr.makiArrived = true;
            Characters.WalkInAndJoin(tr.Maki, map);
            Find.LetterStack.ReceiveLetter("Maki Zenin joins", "Maki Zenin walks in and joins your colony.", LetterDefOf.PositiveEvent, tr.Maki);
        }
    }

    public class IncidentWorker_TojiOffers : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var tr = RestrictedTracker.Instance;
            return tr != null && !tr.tojiHired && (tr.toji == null || !tr.toji.Dead) && parms.target is Map;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            var map = (Map)parms.target;
            var letter = (ChoiceLetter_TojiOffer)LetterMaker.MakeLetter("Toji offers his services",
                $"Toji Fushiguro, a sorcerer killer with no cursed energy, will work for you for {Restricted.TojiPrice} silver. He joins your colony if you pay.",
                def.letterDef);
            letter.map = map;
            letter.StartTimeout(GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter(letter);
            return true;
        }
    }

    public class ChoiceLetter_TojiOffer : ChoiceLetter
    {
        public Map map;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) { yield return Option_Close; yield break; }
                var pay = new DiaOption($"Pay {Restricted.TojiPrice} silver")
                {
                    action = () => { Restricted.HireToji(map); Find.LetterStack.RemoveLetter(this); },
                    resolveTree = true,
                };
                if (map == null || Restricted.SilverCount(map) < Restricted.TojiPrice)
                    pay.Disable($"needs {Restricted.TojiPrice} silver on the map");
                yield return pay;
                yield return Option_Reject;
                yield return Option_Postpone;
            }
        }

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && RestrictedTracker.Instance != null && !RestrictedTracker.Instance.tojiHired;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
        }
    }

    public class IncidentWorker_MakiJoins : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var tr = RestrictedTracker.Instance;
            return tr != null && !tr.makiArrived && parms.target is Map;
        }

        /// <summary>She asks to join (her call 2026-11-11: a real choice). She's made only if welcomed.</summary>
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            JoinOffer.Send(JoinOffer.Maki, (Map)parms.target, "Maki Zenin asks to join",
                "Maki Zenin, a sorcerer of Jujutsu High who fights with cursed tools, is at the edge of the colony and asks to join you."
                + "\n\nWelcome her and she walks in and joins. Refuse, and she may come by again another time.", def.letterDef);
            return true;
        }
    }
}
