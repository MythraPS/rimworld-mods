using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// What Yuji thinks of the colony moves with what the colony does. RimWorld
    /// already records the morally loaded acts as history events (the same events
    /// ideoligion precepts react to); this listens to them. Names that do not exist
    /// without a DLC are simply never seen.
    /// </summary>
    public static class YujiRegard
    {
        private static readonly Dictionary<string, (float delta, string reason)> Events =
            new Dictionary<string, (float, string)>
        {
            // Good
            { "CharityFulfilled_Beggars",            (8f,  "you gave to beggars") },
            { "CharityFulfilled_HospitalityRefugees", (10f, "you sheltered refugees") },
            { "CharityFulfilled_RefugeePodCrash",    (8f,  "you rescued a crash survivor") },
            { "CharityFulfilled_ShuttleCrashRescue", (8f,  "you rescued shuttle crash survivors") },
            { "CharityFulfilled_WandererJoins",      (4f,  "you took in a wanderer") },
            { "CharityFulfilled_IntroWimp",          (4f,  "you took in someone who needed help") },
            { "CharityFulfilled_ThreatReward_Joiner",(4f,  "you took in someone being hunted") },
            { "FriendlyExitedMapTended",             (3f,  "you treated a stranger's wounds") },
            { "PeaceTalksSuccess",                   (3f,  "you made peace") },
            { "PeaceTalksTriumph",                   (5f,  "you made peace") },
            { "GaveGift",                            (2f,  "you sent a gift") },
            { "AcceptedDeserter",                    (3f,  "you protected a deserter") },

            // Bad
            { "CharityRefused_Beggars",              (-6f, "you turned beggars away") },
            { "CharityRefused_Beggars_Betrayed",     (-12f,"you betrayed beggars") },
            { "CharityRefused_HospitalityRefugees",  (-6f, "you turned refugees away") },
            { "CharityRefused_RefugeePodCrash",      (-6f, "you left a crash survivor to die") },
            { "CharityRefused_ShuttleCrashRescue",   (-6f, "you left shuttle crash survivors to die") },
            { "CharityRefused_Pilgrims_Betrayed",    (-12f,"you betrayed pilgrims") },
            { "ExecutedPrisonerInnocent",            (-15f,"you executed an innocent prisoner") },
            { "ExecutedPrisonerGuilty",              (-3f, "you executed a prisoner") },
            { "ExecutedGuest",                       (-20f,"you executed a guest") },
            { "ExecutedColonist",                    (-20f,"you executed one of your own") },
            { "HarvestedOrganFromGuest",             (-15f,"you harvested a guest's organs") },
            { "HarvestedOrganFromColonist",          (-15f,"you harvested a colonist's organs") },
            { "HarvestedOrgan",                      (-12f,"you harvested a prisoner's organs") },
            { "SoldOrgan",                           (-6f, "you sold human organs") },
            { "SoldSlave",                           (-12f,"you sold a person") },
            { "EnslavedPrisoner",                    (-8f, "you enslaved a prisoner") },
            { "AteHumanMeatDirect",                  (-10f,"your people ate human flesh") },
            { "ButcheredHuman",                      (-8f, "you butchered a human body") },
            { "InnocentPrisonerDied",                (-5f, "an innocent prisoner died in your care") },
            { "PerformedHarmfulSurgery",             (-8f, "you cut into someone against their will") },
            { "AttackedCaravan",                     (-8f, "you attacked a caravan") },
            { "GhoulInfusionPerformed",              (-6f, "you turned someone into a ghoul") },
            { "PsychicRitualPerformed",              (-5f, "you performed a dark ritual") },
        };

        /// <summary>Several events fire once per participant; one act should count once.</summary>
        private static int lastTick = -1;
        private static string lastDef;

        public static void OnEvent(HistoryEvent ev)
        {
            var tracker = QuestlineTracker.Instance;
            if (tracker == null || ev.def == null) return;
            if (!Events.TryGetValue(ev.def.defName, out var e)) return;

            // Only the colony's own acts count. Events without a doer (charity,
            // diplomacy) are about the player by construction.
            if (ev.args.TryGetArg(HistoryEventArgsNames.Doer, out Pawn doer)
                && doer != null && doer.Faction != Faction.OfPlayer && !doer.IsColonist)
                return;

            int now = Find.TickManager.TicksGame;
            if (now == lastTick && lastDef == ev.def.defName) return;
            lastTick = now; lastDef = ev.def.defName;

            tracker.ChangeRegard(e.delta, e.reason);
        }
    }

    [HarmonyPatch(typeof(HistoryEventsManager), nameof(HistoryEventsManager.RecordEvent))]
    public static class Patch_RecordEvent_Yuji
    {
        public static void Postfix(HistoryEvent historyEvent)
        {
            try { YujiRegard.OnEvent(historyEvent); }
            catch (System.Exception ex) { Log.ErrorOnce("[Ryomen Sukuna] Yuji regard: " + ex, 0x5A7A1); }
        }
    }

    /// <summary>
    /// Yuji is immune to the curse: the fingers make him stronger without making him
    /// hungrier. Takes the vessel's hunger factor back out for him alone.
    /// </summary>
    [HarmonyPatch(typeof(HediffSet), nameof(HediffSet.GetHungerRateFactor))]
    public static class Patch_YujiHunger
    {
        public static void Postfix(HediffSet __instance, HediffDef ignore, ref float __result)
        {
            var pawn = __instance.pawn;
            if (!Yuji.Is(pawn) || ignore == SukunaDefOf.Sukuna_Vessel) return;
            var vessel = __instance.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            float f = vessel?.CurStage?.hungerRateFactor ?? 1f;
            if (f > 0f) __result /= f;
        }
    }
}
