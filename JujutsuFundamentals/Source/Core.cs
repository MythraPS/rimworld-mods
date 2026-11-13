using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

using Ability = VEF.Abilities.Ability;

namespace JujutsuFundamentals
{
    // Def types live in separate databases, so JJK_BlackFlash is both an AbilityDef and a HediffDef.
    // One DefOf class can't hold two fields with the same name, hence two classes.
    [DefOf]
    public static class JJKDefOf
    {
        public static HediffDef JJK_Reinforcement;
        public static HediffDef JJK_BlackFlash;
        public static HediffDef JJK_BlackFlashZone;
        public static HediffDef JJK_ReverseCursedTechnique;
        public static HediffDef JJK_DomainAmplification;
        public static HediffDef JJK_HollowWickerBasket;
        public static HediffDef JJK_SimpleDomain;
        public static HediffDef JJK_Batto;
        public static HediffDef JJK_HazyMoon;
        public static VanillaPsycastsExpanded.PsycasterPathDef JJK_CursedEnergyFundamentals;
        public static VanillaPsycastsExpanded.PsycasterPathDef JJK_NewShadowStyle;
        static JJKDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKDefOf));
    }

    [DefOf]
    public static class JJKAbilityDefOf
    {
        public static VEFAbilityDef JJK_Reinforcement;
        public static VEFAbilityDef JJK_Blast;
        public static VEFAbilityDef JJK_AirStep;
        public static VEFAbilityDef JJK_BlackFlash;
        public static VEFAbilityDef JJK_DomainAmplification;
        public static VEFAbilityDef JJK_HollowWickerBasket;
        public static VEFAbilityDef JJK_ReverseCursedTechnique;
        public static VEFAbilityDef JJK_SimpleDomain;
        public static VEFAbilityDef JJK_Batto;
        public static VEFAbilityDef JJK_EveningMoon;
        public static VEFAbilityDef JJK_HazyMoon;
        static JJKAbilityDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(JJKAbilityDefOf));
    }

    /// <summary>
    /// What every technique here scales on, and the hooks the character mods (Sukuna, Gojo and the
    /// ones to come) use to change it for their own pawns without this mod knowing about them.
    /// Every list is filled by the other mods at startup.
    /// </summary>
    public static class Jujutsu
    {
        /// <summary>Multipliers on Power: Sukuna's fingers, Modulo Yuji's x2, and so on.</summary>
        public static readonly List<Func<Pawn, float>> PowerFactors = new List<Func<Pawn, float>>();
        /// <summary>Multipliers on the Black Flash chance (Gojo: x2).</summary>
        public static readonly List<Func<Pawn, float>> BlackFlashChanceFactors = new List<Func<Pawn, float>>();
        /// <summary>Pawns who have Black Flash without learning it (Yuji).</summary>
        public static readonly List<Func<Pawn, bool>> BlackFlashInnate = new List<Func<Pawn, bool>>();
        /// <summary>A forced Black Flash chance, e.g. Modulo Yuji's "always" toggle. Return a value below 0 for none.</summary>
        public static readonly List<Func<Pawn, float>> BlackFlashChanceOverride = new List<Func<Pawn, float>>();
        /// <summary>Multipliers on Reverse Cursed Technique healing (Gojo's Six Eyes: x1.5).</summary>
        public static readonly List<Func<Pawn, float>> RctHealFactors = new List<Func<Pawn, float>>();
        /// <summary>Multipliers on its psyfocus and neural heat cost (Six Eyes: x0.5; poison raises it).</summary>
        public static readonly List<Func<Pawn, float>> RctCostFactors = new List<Func<Pawn, float>>();
        /// <summary>Lost body parts that never regrow (Yuji's two fingers).</summary>
        public static readonly List<Func<Pawn, BodyPartRecord, bool>> RctKeepsLost = new List<Func<Pawn, BodyPartRecord, bool>>();
        /// <summary>Called after a part regrows, so a mod can undo what it must (Yuji's fingers come back with the hand).</summary>
        public static readonly List<Action<Pawn>> RctAfterRegrow = new List<Action<Pawn>>();
        /// <summary>Who may use their own cursed technique with Domain Amplification on (her call: only Sukuna).</summary>
        public static readonly List<Func<Pawn, bool>> AmplificationExempt = new List<Func<Pawn, bool>>();
        /// <summary>What Domain Amplification takes off a target, beyond what this mod already strips (Gojo's Infinity).</summary>
        public static readonly List<Action<Pawn, Pawn>> AmplificationStrips = new List<Action<Pawn, Pawn>>();
        /// <summary>Pawns off the ground in a way the game doesn't know about (Gojo's flight, Air Step).</summary>
        public static readonly List<Func<Pawn, bool>> Airborne = new List<Func<Pawn, bool>>();
        /// <summary>Pawns who count as sorcerers without knowing any technique here (vessels, Yuji, Gojo).</summary>
        public static readonly List<Func<Pawn, bool>> Sorcerers = new List<Func<Pawn, bool>>();
        /// <summary>Extra poisons that make Reverse Cursed Technique cost more (Yuji's blood).</summary>
        public static readonly List<Func<Pawn, bool>> Poisoned = new List<Func<Pawn, bool>>();
        /// <summary>The colour of a pawn's cursed energy on a glowing weapon (Yuta: purple). Null = not theirs to say.</summary>
        public static readonly List<Func<Pawn, Color?>> EnergyColors = new List<Func<Pawn, Color?>>();
        /// <summary>Multipliers on Cursed Energy Reinforcement's strength (Ryu's is extra strong).</summary>
        public static readonly List<Func<Pawn, float>> ReinforcementFactors = new List<Func<Pawn, float>>();
        /// <summary>Extra stat changes on Cursed Energy Reinforcement: (pawn, strength k, offsets, factors).</summary>
        public static readonly List<Action<Pawn, float, List<StatModifier>, List<StatModifier>>> ReinforcementExtras =
            new List<Action<Pawn, float, List<StatModifier>, List<StatModifier>>>();

        public static float Sensitivity(Pawn p) =>
            p == null ? 1f : Mathf.Max(0f, p.GetStatValue(StatDefOf.PsychicSensitivity));

        /// <summary>Psychic sensitivity times whatever the character mods add.</summary>
        public static float Power(Pawn p)
        {
            float v = Sensitivity(p);
            foreach (var f in PowerFactors) v *= f(p);
            return v;
        }

        public static float Product(List<Func<Pawn, float>> list, Pawn p)
        {
            float v = 1f;
            foreach (var f in list) v *= f(p);
            return v;
        }

        public static bool Any(List<Func<Pawn, bool>> list, Pawn p)
        {
            foreach (var f in list) if (f(p)) return true;
            return false;
        }

        public static bool Knows(Pawn p, VEFAbilityDef def)
        {
            var comp = p?.GetComp<CompAbilities>();
            return comp != null && def != null && comp.HasAbility(def);
        }

        public static bool Has(Pawn p, HediffDef def) =>
            def != null && p?.health?.hediffSet?.HasHediff(def) == true;

        /// <summary>On the ground: not flying (1.6 flight), and no mod says otherwise.</summary>
        public static bool OnGround(Pawn p) => p != null && !p.Flying && !Any(Airborne, p);

        public static bool IsSorcerer(Pawn p)
        {
            if (p == null) return false;
            if (Any(Sorcerers, p)) return true;
            var comp = p.GetComp<CompAbilities>();
            if (comp == null) return false;
            foreach (var a in comp.LearnedAbilities)
            {
                var path = a.def.GetModExtension<VanillaPsycastsExpanded.AbilityExtension_Psycast>()?.path;
                if (path != null && (path == JJKDefOf.JJK_CursedEnergyFundamentals || path == JJKDefOf.JJK_NewShadowStyle)) return true;
            }
            return false;
        }

        public static bool HoldsMeleeWeapon(Pawn p) => p?.equipment?.Primary?.def.IsMeleeWeapon == true;

        /// <summary>Raise Cursed Energy Output: x1.5 to the techniques' power, range and radius (her call 2026-11-10,
        /// as Sukuna's raised output already did for Shrine).</summary>
        public const float RaisedFactor = 1.5f;
        public static bool Raised(Pawn p) => Hediff_Reinforcement.Of(p)?.Raised == true;
        public static float Boost(Pawn p) => Raised(p) ? RaisedFactor : 1f;

        /// <summary>Learn techniques of a path directly (no points), unlocking the path.</summary>
        public static void Learn(Pawn p, VanillaPsycastsExpanded.PsycasterPathDef path, params VEFAbilityDef[] defs)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null || path == null) return;
            var psy = p.health.hediffSet.hediffs.Find(h => h is VanillaPsycastsExpanded.Hediff_PsycastAbilities) as VanillaPsycastsExpanded.Hediff_PsycastAbilities;
            if (psy != null && !psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            foreach (var d in defs.Length > 0 ? (IEnumerable<VEFAbilityDef>)defs : path.abilities)
                if (d != null && !comp.HasAbility(d)) comp.GiveAbility(d);
            PassiveSync.Sync(p);
        }

        public static void LearnAllFundamentals(Pawn p) => Learn(p, JJKDefOf.JJK_CursedEnergyFundamentals);
        public static void LearnUpToSimpleDomain(Pawn p) =>
            Learn(p, JJKDefOf.JJK_NewShadowStyle, JJKAbilityDefOf.JJK_HollowWickerBasket, JJKAbilityDefOf.JJK_SimpleDomain);
        public static void LearnBasket(Pawn p) => Learn(p, JJKDefOf.JJK_NewShadowStyle, JJKAbilityDefOf.JJK_HollowWickerBasket);
    }

    [StaticConstructorOnStartup]
    public static class Startup
    {
        public static Harmony H;

        static Startup()
        {
            H = new Harmony("mythraps.jujutsufundamentals");
            H.PatchAll(Assembly.GetExecutingAssembly());
            PsycastContext.PatchCasts(H);
            PsycastContext.PatchImpacts(H);
            SidearmsCompat.Patch(H);
            NoMagicClass.Patch(H);
            AmplificationStrip.CollectBuffs();
            StatPart_Sorcerer.Register();
            Log.Message("[JJK Core] ready.");
        }
    }

    /// <summary>Empties the static lists of live things when a game is started or loaded. A pawn from the previous
    /// game still reads as spawned and its Map resolves by index to the new game's map, so a leftover Simple Domain
    /// or open domain would otherwise be drawn, cut projectiles and clash on the new map.
    /// Game components are made before the maps and hediffs load, so the new game's own entries come after this.</summary>
    public class StaticReset : GameComponent
    {
        public StaticReset(Game game)
        {
            Domains.Active.Clear();
            TestDomains.Open.Clear();
            Hediff_SimpleDomain.Active.Clear();
            EveningMoonStrike.Reset();
            PsycastContext.Reset();
            Beams.Reset();
        }
    }

    /// <summary>A tree node that is never cast: the technique works through a hediff kept in step by PassiveSync.</summary>
    public class Ability_Passive : Ability
    {
        public override bool ShowGizmoOnPawn() => false;

        public override bool IsEnabledForPawn(out string reason)
        {
            reason = "Passive: always active once learned.";
            return false;
        }

        public override void Cast(RimWorld.Planet.GlobalTargetInfo[] targets) { }
    }
}
