using RimWorld;
using Verse;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    // Split across three classes on purpose. Def types live in separate databases,
    // so `Sukuna_BlackFlash` is legally BOTH an AbilityDef and a HediffDef — but a
    // single DefOf class cannot hold two static fields with the same name.

    [DefOf]
    public static class SukunaDefOf
    {
        public static HediffDef Sukuna_Vessel;
        public static HediffDef Sukuna_FourArmed;
        public static HediffDef Sukuna_ExtraArms;
        public static HediffDef Sukuna_BellyMouth;
        public static HediffDef Sukuna_CurseMarks;
        public static HediffDef Sukuna_Mask;
        public static TraitDef Sukuna_Selfish;
        public static HediffDef Sukuna_ReverseCursedTechnique;
        public static HediffDef Sukuna_BlackFlash;
        public static HediffDef Sukuna_PseudoInfinity;
        public static HediffDef Sukuna_MalevolentShrine;
        public static HediffDef Sukuna_ThornsShield;
        public static RulePackDef Sukuna_DamageEvent_Thorns;

        public static DamageDef Sukuna_Slash;
        public static DamageDef Sukuna_Dismantle;
        public static DamageDef Sukuna_WorldSlash;
        public static DamageDef Sukuna_Flame;

        public static VanillaPsycastsExpanded.PsycasterPathDef Sukuna_Path;

        static SukunaDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SukunaDefOf));
    }

    [DefOf]
    public static class SukunaAbilityDefOf
    {
        public static VEFAbilityDef Sukuna_Cleave;
        public static VEFAbilityDef Sukuna_AirStep;
        public static VEFAbilityDef Sukuna_Dismantle;
        public static VEFAbilityDef Sukuna_SlashDefenseThorns;
        public static VEFAbilityDef Sukuna_SlashDefensePseudoInfinity;
        public static VEFAbilityDef Sukuna_ReverseCursedTechnique;
        public static VEFAbilityDef Sukuna_MalevolentShrine;
        public static VEFAbilityDef Sukuna_BlackFlash;
        public static VEFAbilityDef Sukuna_FireArrow;
        public static VEFAbilityDef Sukuna_WorldCuttingSlash;

        static SukunaAbilityDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SukunaAbilityDefOf));
    }

    [DefOf]
    public static class SukunaThingDefOf
    {
        public static ThingDef Sukuna_Finger;
        public static ThingDef Sukuna_DismantleSlash;
        public static ThingDef Sukuna_FireArrow;

        static SukunaThingDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SukunaThingDefOf));
    }
}
