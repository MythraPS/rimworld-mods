using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    [DefOf]
    public static class SukunaQuestDefOf
    {
        public static HediffDef Sukuna_YujiPhysique;
        public static FactionDef Sukuna_KingOfCurses;
        public static HairDef Sukuna_YujiHair;
        public static HairDef Sukuna_KingHair;
        public static HairDef Sukuna_VesselHair;
        public static HairDef Sukuna_ModuloHair;
        public static TattooDef Sukuna_Markings;
        public static TattooDef Sukuna_KingMarkings;
        public static TattooDef Sukuna_YujiMarks;
        public static PawnKindDef Sukuna_Yuji;
        public static BackstoryDef Sukuna_YujiChildhood;
        public static BackstoryDef Sukuna_YujiAdulthood;

        public static PawnKindDef Sukuna_ModuloYuji;
        public static BackstoryDef Sukuna_ModuloAdulthood;
        public static TattooDef Sukuna_ModuloScars;
        public static IncidentDef Sukuna_ModuloYujiArrives;

        public static IncidentDef Sukuna_YujiEncounter;
        public static IncidentDef Sukuna_FingerRumour;
        public static IncidentDef Sukuna_VesselRaid;
        public static IncidentDef Sukuna_KingRaid;
        public static LetterDef Sukuna_YujiLetter;

        public static QuestScriptDef Sukuna_Finger_Ruin;
        public static QuestScriptDef Sukuna_Finger_Held;
        public static QuestScriptDef Sukuna_Finger_Convoy;

        static SukunaQuestDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SukunaQuestDefOf));
    }
}
