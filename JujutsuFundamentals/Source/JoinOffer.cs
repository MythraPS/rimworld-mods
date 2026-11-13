using System.Collections.Generic;
using RimWorld;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// "X asks to join" as a real choice (her call 2026-11-11: Gojo, Maki and Inumaki asked to join but had no choice).
    /// Same shape as Yuta's and Toji's letters: Welcome / Reject / Postpone, one day to answer. Nobody is made until
    /// they're welcomed, so a refusal leaves nothing behind. Refused or ignored, they may ask again later (their
    /// incident's minRefireDays, 15), like Yuta.
    /// </summary>
    public static class JoinOffer
    {
        public const string Gojo = "gojo", Maki = "maki", Inumaki = "inumaki", Angel = "angel";

        public static void Send(string who, Map map, string title, string text, LetterDef def, LookTargets look = null)
        {
            var letter = (ChoiceLetter_JoinOffer)LetterMaker.MakeLetter(title, text, def, look ?? LookTargets.Invalid);
            letter.who = who;
            letter.map = map;
            letter.StartTimeout(GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter(letter);
        }

        /// <summary>Can this one still come? (Answers on a stale letter do nothing.)</summary>
        public static bool Open(string who)
        {
            switch (who)
            {
                case Gojo: return !GojoPsycasts.Satoru.Exists();
                case Maki: return RestrictedTracker.Instance != null && !RestrictedTracker.Instance.makiArrived;
                case Inumaki:
                    var t = YutaTracker.Instance;
                    return t != null && !t.inumakiArrived && (t.inumaki == null || !t.inumaki.Dead);
                case Angel:
                    var a = PrisonRealmQuest.Instance?.angel;
                    return a != null && !a.Dead && a.Faction != Faction.OfPlayer;
            }
            return false;
        }

        public static void Accept(string who, Map map)
        {
            // the letter's map may be gone by the time it's answered (a site the player left): they come home instead
            if (map == null || !Find.Maps.Contains(map)) map = Find.AnyPlayerHomeMap;
            if (map == null || !Open(who)) return;
            switch (who)
            {
                case Gojo: GojoPsycasts.IncidentWorker_GojoArrives.Arrive(map); break;
                case Maki: Restricted.MakiJoins(map); break;
                case Inumaki: global::JujutsuFundamentals.Inumaki.Joins(map); break;
                case Angel: AngelChar.Join(map); break;
            }
        }
    }

    public class ChoiceLetter_JoinOffer : ChoiceLetter
    {
        public string who;
        public Map map;

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly) { yield return Option_Close; yield break; }
                yield return new DiaOption(who == JoinOffer.Maki || who == JoinOffer.Angel ? "Welcome her" : "Welcome him")
                {
                    action = () => { JoinOffer.Accept(who, map); Find.LetterStack.RemoveLetter(this); },
                    resolveTree = true,
                };
                yield return Option_Reject;
                yield return Option_Postpone;
            }
        }

        public override bool CanShowInLetterStack => base.CanShowInLetterStack && JoinOffer.Open(who);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref who, "who");
            Scribe_References.Look(ref map, "map");
        }
    }
}
