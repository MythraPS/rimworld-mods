using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace JujutsuFundamentals
{
    /// <summary>
    /// The game never picks one of the mod's characters as a relative of a pawn it is generating (found by the test
    /// runner 2026-11-12). They are made with no relatives, and canon gives them none the game should invent. It also
    /// stops a crash: the game's parent code assumes a three-part name, and Choso and Uraume have one-word names, so a
    /// raider or wanderer given one of them as a parent failed to generate. Any humanlike with a one-word name is kept
    /// out for the same reason.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class NoInventedRelatives
    {
        public static bool Character(Pawn p)
        {
            if (p == null) return false;
            if (p.RaceProps?.Humanlike == true && p.Name is NameSingle) return true;
            return GojoPsycasts.Satoru.IsGojo(p) || SukunaPsycasts.SukunaBehaviour.IsSukuna(p) || SukunaPsycasts.Yuji.Is(p)
                   || SukunaPsycasts.ModuloYuji.Is(p) || SukunaPsycasts.ChosoTracker.Is(p) || SukunaPsycasts.UraumeTracker.Is(p)
                   || Yuta.Is(p) || Uro.Is(p) || Inumaki.Is(p) || AngelChar.Is(p)
                   || CharacterTracker.IsKusakabe(p) || CharacterTracker.IsMiwa(p) || CharacterTracker.IsTodo(p) || Ryu.Is(p)
                   || RestrictedTracker.IsToji(p) || RestrictedTracker.IsMaki(p);
        }

        static NoInventedRelatives()
        {
            var h = new Harmony("mythraps.jujutsu.relatives");
            var post = new HarmonyMethod(typeof(NoInventedRelatives), nameof(Postfix));
            var types = new List<Type> { typeof(PawnRelationWorker) };
            types.AddRange(GenTypes.AllSubclasses(typeof(PawnRelationWorker)));
            foreach (var t in types)
            {
                var m = t.GetMethod(nameof(PawnRelationWorker.GenerationChance), BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (m == null || m.IsAbstract) continue;
                try { h.Patch(m, postfix: post); }
                catch (Exception e) { Log.Warning($"[JJK Core] relatives: could not patch {t.Name}: {e.Message}"); }
            }
        }

        public static void Postfix(Pawn generated, Pawn other, ref float __result)
        {
            if (__result > 0f && (Character(other) || Character(generated))) __result = 0f;
        }
    }

    /// <summary>
    /// The game sometimes reuses a pawn waiting in the world for a raid, an outpost or a visitor instead of making a new
    /// one ("redressing" it). The mod's characters wait there until their event, so they could turn up as somebody
    /// else's raider, or (test runner 2026-11-12) Miwa as a guard at her own outpost, dead before anyone reached her.
    /// They are never reused.
    /// </summary>
    [HarmonyPatch(typeof(PawnGenerator), "IsValidCandidateToRedress")]
    public static class Patch_CharactersNeverRedressed
    {
        public static void Postfix(Pawn pawn, ref bool __result)
        {
            if (__result && NoInventedRelatives.Character(pawn)) __result = false;
        }
    }
}
