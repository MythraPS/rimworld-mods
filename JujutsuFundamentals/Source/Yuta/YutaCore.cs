using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    // Yuta Okkotsu, Rika, Takako Uro and Toge Inumaki (her design 2026-11-11, design doc section 3d).
    // This file is the shared core; each part lives in its own folder under Source/Yuta:
    //   Speech/  the Cursed Speech path and Inumaki
    //   Sky/     Uro's Sky Manipulation, Dhruv's shikigami, Charles's G Warstaff, and Uro
    //   Rika/    Rika herself (both forms), her partial manifestations
    //   Copy/    Copy, Paste, healing others, the domain, Yuta himself and his starting stages

    [DefOf]
    public static class YutaDefOf
    {
        public static PsycasterPathDef JJK_YutaPath;
        public static PsycasterPathDef JJK_CursedSpeech;
        public static HediffDef JJK_VastCursedEnergy;
        static YutaDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(YutaDefOf));
    }

    /// <summary>Every ability on the two paths, by defName. Looked up by name so one part can name another's
    /// abilities before that part's defs exist.</summary>
    public static class YutaAbilities
    {
        // Yuta's path (JJK_YutaPath)
        public const string ManifestRika = "JJK_Yuta_ManifestRika", RikaArm = "JJK_Yuta_RikaArm", GWarstaff = "JJK_Yuta_GWarstaff",
                            RikaBite = "JJK_Yuta_RikaBite", RikaGrapple = "JJK_Yuta_RikaGrapple", Dhruv = "JJK_Yuta_Dhruv",
                            Copy = "JJK_Yuta_Copy", Flight = "JJK_Yuta_Flight", Redirect = "JJK_Yuta_Redirect",
                            RctOutput = "JJK_Yuta_RctOutput", Paste = "JJK_Yuta_Paste", ThinIceBreaker = "JJK_Yuta_ThinIceBreaker",
                            Domain = "JJK_Yuta_Domain";
        // Cursed Speech (JJK_CursedSpeech)
        public const string Stop = "JJK_CS_Stop", RunAway = "JJK_CS_RunAway", DontMove = "JJK_CS_DontMove", BlastAway = "JJK_CS_BlastAway",
                            Sleep = "JJK_CS_Sleep", GetCrushed = "JJK_CS_GetCrushed", Explode = "JJK_CS_Explode", Die = "JJK_CS_Die";

        public static readonly string[] Sky = { Flight, Redirect, ThinIceBreaker };
        public static readonly string[] Speech = { Stop, RunAway, DontMove, BlastAway, Sleep, GetCrushed, Explode, Die };

        /// <summary>Only Yuta can ever have these (her calls 2026-11-11): Rika, Copy, Paste, healing others, his domain.
        /// Paste can't give them away. (Die was here until her call later that day: Inumaki has all of Cursed Speech.)</summary>
        public static readonly string[] YutaOnly = { ManifestRika, RikaArm, RikaBite, RikaGrapple, Copy, Paste, RctOutput, Domain };

        public static VEFAbilityDef Def(string name) => DefDatabase<VEFAbilityDef>.GetNamedSilentFail(name);
        public static bool Is(VEFAbilityDef d, string name) => d != null && d.defName == name;
        public static bool IsYutaOnly(VEFAbilityDef d) => d != null && YutaOnly.Contains(d.defName);
    }

    /// <summary>Who's who, and the pawns themselves. Each part keeps any other state in its own GameComponent.</summary>
    public class YutaTracker : GameComponent
    {
        public static YutaTracker Instance;

        public Pawn yuta, uro, inumaki;
        /// <summary>Rika as she is on the map right now (either form), or null.</summary>
        public Pawn rika;
        public bool yutaArrived, uroArrived, inumakiArrived;
        /// <summary>Adult Yuta (stage 4): Shikigami Rika as strong as her curse form, no five-minute limit. Set by the Copy part.</summary>
        public bool yutaAdult;

        public YutaTracker(Game game) { Instance = this; }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_References.Look(ref yuta, "yuta", true);
            Scribe_References.Look(ref uro, "uro", true);
            Scribe_References.Look(ref inumaki, "inumaki", true);
            Scribe_References.Look(ref rika, "rika");
            Scribe_Values.Look(ref yutaArrived, "yutaArrived");
            Scribe_Values.Look(ref uroArrived, "uroArrived");
            Scribe_Values.Look(ref inumakiArrived, "inumakiArrived");
            Scribe_Values.Look(ref yutaAdult, "yutaAdult");
        }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 37) return;
            if (yuta != null && !yuta.Dead) VastCursedEnergy.Sync(yuta);
        }
    }

    public static partial class Yuta
    {
        /// <summary>Yuta's Cursed Energy Reinforcement is especially strong (her design): x2.5 (proposal; x1.75 until her call
        /// 2026-11-11 "needs to be stronger"; Ryu's is x1.5). Psychic hypersensitivity raises it further through his power.</summary>
        public const float ReinforcementFactor = 2.5f;
        public static readonly Color Purple = new Color(0.62f, 0.3f, 0.95f);

        public static bool Is(Pawn p) => p != null && YutaTracker.Instance?.yuta == p;
        public static bool Adult(Pawn p) => Is(p) && YutaTracker.Instance.yutaAdult;

        /// <summary>Starting stage from the mod settings: 1 start of JJK 0, 2 end of JJK 0, 3 Shinjuku, 4 adult (theoretical).</summary>
        public static int StartStage => Mathf.Clamp(JujutsuMod.Settings?.yutaStage ?? 1, 1, 4);

        static Yuta()
        {
            Jujutsu.ReinforcementFactors.Add(p => Is(p) ? ReinforcementFactor : 1f);
            Jujutsu.EnergyColors.Add(p => Is(p) ? (Color?)Purple : null);
            Jujutsu.Sorcerers.Add(Is);
        }

        /// <summary>Runs the static constructor (hooks) at startup.</summary>
        public static void Init() { }
    }

    public static partial class Uro
    {
        public static bool Is(Pawn p) => p != null && YutaTracker.Instance?.uro == p;
        /// <summary>Uro can only ever have her Sky Manipulation and her own domain (her calls 2026-11-11).</summary>
        public static bool Allowed(VEFAbilityDef d) => d != null && (YutaAbilities.Sky.Contains(d.defName) || d.defName == "JJK_UroDomainExpansion");
    }

    public static partial class Inumaki
    {
        public static bool Is(Pawn p) => p != null && YutaTracker.Instance?.inumaki == p;
        /// <summary>Inumaki can only ever have Cursed Speech, all of it, and Cursed Energy Reinforcement (her calls 2026-11-11).</summary>
        public static bool Allowed(VEFAbilityDef d) => d != null && (YutaAbilities.Speech.Contains(d.defName) || d == JJKAbilityDefOf.JJK_Reinforcement);
    }

    /// <summary>Rika. The Rika part spawns and dismisses her; this is what the other parts read.</summary>
    public static partial class Rika
    {
        public static Pawn Current => YutaTracker.Instance?.rika;
        /// <summary>Is Rika fully manifested on the map beside this Yuta? (Copy and Die need her.)</summary>
        public static bool Manifested(Pawn yuta)
        {
            var r = Current;
            return yuta != null && r != null && !r.Dead && r.Spawned && yuta.Spawned && r.Map == yuta.Map;
        }
    }

    /// <summary>Yuta's huge cursed energy reserve, the same hediff whether innate (Yuta) or lent by his ring
    /// (anyone else), so it never stacks.</summary>
    public static class VastCursedEnergy
    {
        public static bool Should(Pawn p) => Yuta.Is(p) || YutaRing.On(p);

        public static void Sync(Pawn p)
        {
            if (p?.health == null || YutaDefOf.JJK_VastCursedEnergy == null) return;
            var h = p.health.hediffSet.GetFirstHediffOfDef(YutaDefOf.JJK_VastCursedEnergy);
            if (Should(p)) { if (h == null) p.health.AddHediff(YutaDefOf.JJK_VastCursedEnergy); }
            else if (h != null) p.health.RemoveHediff(h);
        }
    }

    /// <summary>Psyfocus that refills on its own (Yuta's reserve, her call 2026-11-11: running out should take real
    /// effort). Proposal: 2% of the bar every 250 ticks, about 20% an in-game hour.</summary>
    public class HediffCompProperties_PsyfocusRegen : HediffCompProperties
    {
        public int interval = 250;
        public float amount = 0.02f;
        public HediffCompProperties_PsyfocusRegen() { compClass = typeof(HediffComp_PsyfocusRegen); }
    }

    public class HediffComp_PsyfocusRegen : HediffComp
    {
        private HediffCompProperties_PsyfocusRegen Props => (HediffCompProperties_PsyfocusRegen)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            var p = Pawn;
            if (p == null || p.Dead || !p.IsHashIntervalTick(Props.interval)) return;
            var tr = p.psychicEntropy;
            if (tr == null || tr.CurrentPsyfocus >= 1f) return;
            tr.OffsetPsyfocusDirectly(Mathf.Min(Props.amount, 1f - tr.CurrentPsyfocus));
        }
    }

    public class Apparel_YutaRing : Apparel
    {
        public override void Notify_Equipped(Pawn pawn) { base.Notify_Equipped(pawn); VastCursedEnergy.Sync(pawn); }
        public override void Notify_Unequipped(Pawn pawn) { base.Notify_Unequipped(pawn); VastCursedEnergy.Sync(pawn); }
    }

    /// <summary>The path gates: Yuta's path is his alone; Cursed Speech opens for Yuta and Inumaki; Uro and Inumaki
    /// can't open anything else (her call 2026-11-11: they can't gain any other abilities).</summary>
    [HarmonyPatch(typeof(PsycasterPathDef), nameof(PsycasterPathDef.CanPawnUnlock))]
    public static class Patch_YutaPathGate
    {
        public static void Postfix(PsycasterPathDef __instance, Pawn pawn, ref bool __result)
        {
            if (Uro.Is(pawn)) { __result = false; return; }
            // the Angel: Fundamentals, and Yuta's tree only for Jacob's Ladder (her call 2026-11-11)
            if (AngelChar.Is(pawn)) { __result = __instance == YutaDefOf.JJK_YutaPath; return; }   // Jacob's Ladder only (2026-11-12: no Fundamentals)
            if (Inumaki.Is(pawn)) { __result = __instance == YutaDefOf.JJK_CursedSpeech; return; }
            // born with one of the techniques on these trees (her call 2026-11-12)
            if (__instance == YutaDefOf.JJK_YutaPath) __result = Yuta.Is(pawn) || Innate.OpensYutaPath(pawn);
            else if (__instance == YutaDefOf.JJK_CursedSpeech) __result = Yuta.Is(pawn) || Innate.Marked(pawn, Innate.CursedSpeech);
        }
    }

    /// <summary>A node only Yuta can learn (Die) shows as not ready for anyone else.</summary>
    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.PrereqsCompleted), new[] { typeof(Pawn) })]
    public static class Patch_YutaOnlyNode
    {
        public static void Postfix(AbilityExtension_Psycast __instance, Pawn pawn, ref bool __result)
        {
            if (Innate.YutaNodeFor(__instance, pawn, out bool ok)) { __result = ok; return; }
            if (__result && !Yuta.Is(pawn) && YutaOnlyNodes.Has(__instance)) __result = false;
            if (__result && AngelChar.Is(pawn) && __instance.path == YutaDefOf.JJK_YutaPath && !YutaOnlyNodes.IsLadder(__instance)) __result = false;
        }
    }

    [HarmonyPatch(typeof(AbilityExtension_Psycast), nameof(AbilityExtension_Psycast.PrereqsCompleted), new[] { typeof(CompAbilities) })]
    public static class Patch_YutaOnlyNode2
    {
        public static void Postfix(AbilityExtension_Psycast __instance, CompAbilities compAbilities, ref bool __result)
        {
            if (Innate.YutaNodeFor(__instance, compAbilities?.Pawn, out bool ok)) { __result = ok; return; }
            if (__result && !Yuta.Is(compAbilities?.Pawn) && YutaOnlyNodes.Has(__instance)) __result = false;
            if (__result && AngelChar.Is(compAbilities?.Pawn) && __instance.path == YutaDefOf.JJK_YutaPath && !YutaOnlyNodes.IsLadder(__instance)) __result = false;
        }
    }

    public static class YutaOnlyNodes
    {
        private static HashSet<AbilityExtension_Psycast> set;
        public static bool IsLadder(AbilityExtension_Psycast e) => e != null && e == JacobsLadder.Def?.GetModExtension<AbilityExtension_Psycast>();
        public static bool Has(AbilityExtension_Psycast e)
        {
            if (set == null)
            {
                set = new HashSet<AbilityExtension_Psycast>();
                foreach (var n in YutaAbilities.YutaOnly)
                {
                    var x = YutaAbilities.Def(n)?.GetModExtension<AbilityExtension_Psycast>();
                    if (x != null) set.Add(x);
                }
            }
            return set.Contains(e);
        }
    }

    /// <summary>Uro and Inumaki never gain an ability outside their own (psytrainers, Paste, anything).</summary>
    [HarmonyPatch(typeof(CompAbilities), nameof(CompAbilities.GiveAbility))]
    public static class Patch_RestrictedLearners
    {
        public static bool Prefix(CompAbilities __instance, VEFAbilityDef abilityDef)
        {
            var p = __instance?.Pawn;
            if (Uro.Is(p)) return Uro.Allowed(abilityDef);
            if (Inumaki.Is(p)) return Inumaki.Allowed(abilityDef);
            if (AngelChar.Is(p)) return AngelChar.Allowed(abilityDef);
            // Jacob's Ladder is the Angel's and, through his tree or Copy, Yuta's: nobody else (her call 2026-11-11)
            if (JacobsLadder.Is(abilityDef)) return Yuta.Is(p) || Innate.Marked(p, Innate.JacobsLadder);   // and anyone born with it (2026-11-12)
            // set YutaTracker.yuta before giving Yuta his abilities, or this turns them away
            // Sukuna has RCT: Output too (her call 2026-11-11)
            if (abilityDef?.defName == YutaAbilities.RctOutput && SukunaPsycasts.SukunaBehaviour.IsSukuna(p)) return true;
            // born with Copy: Copy and Paste (her call 2026-11-12)
            if (Innate.CopiesAlone(p) && (abilityDef?.defName == YutaAbilities.Copy || abilityDef?.defName == YutaAbilities.Paste)) return true;
            return Yuta.Is(p) || !YutaAbilities.IsYutaOnly(abilityDef);
        }
    }

    [StaticConstructorOnStartup]
    public static class YutaStartup
    {
        static YutaStartup() => Yuta.Init();
    }
}
