using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>
    /// Yuji's hits on a possessed or corrupted pawn (her call 2026-11-07) don't wound them: they separate
    /// the soul from whatever has hold of it, and the pawn is knocked out. Once free, they are an ordinary
    /// pawn again, so further hits (or any other attack) hurt as normal.
    ///
    /// What counts (her picks, Anomaly; looked up by name so the mod doesn't need Anomaly):
    ///   possession of any kind (<see cref="Possession"/>, this mod's and other mods');
    ///   ghouls, reverted to a normal person, their ghoul-only implants removed;
    ///   mind control: revenant and awoken hypnosis, psychic trance, the golden cube's hold
    ///     (interest, withdrawal, rage, sculpting), humanity break, inhumanized, void touched;
    ///   a metalhorror implant (removed: no emergence, the game only emerges it from inside);
    ///   fleshmass growths with their own nerves (fleshmass stomach and lung, flesh tentacle, flesh whip),
    ///     which give the natural part back;
    ///   the raised dead (shamblers, awoken corpses) are left a plain corpse, not brought back;
    ///   the unnatural corpse's awoken twin is ended for good (it stops coming back).
    /// </summary>
    public static class SoulSeparation
    {
        private static readonly string[] Corruptions =
        {
            "RevenantHypnosis", "AwokenHypnosis", "PsychicTrance", "CubeInterest", "CubeWithdrawal", "CubeRage",
            "Inhumanized", "VoidTouched", "MetalhorrorImplant",
            "FleshmassStomach", "FleshmassLung", "Tentacle", "FleshWhip",
        };
        private static readonly string[] GhoulOnly =
            { "GhoulPlating", "GhoulBarbs", "AdrenalHeart", "CorrosiveHeart", "MetalbloodHeart" };
        private static readonly string[] MindStates = { "HumanityBreak", "CubeSculpting" };
        private static readonly string[] RaisedDead = { "Shambler", "AwokenCorpse" };

        private static bool Raised(Pawn p) => p.IsMutant && RaisedDead.Contains(p.mutant.Def.defName);
        private static bool Ghoul(Pawn p) => p.IsMutant && p.mutant.Def.defName == "Ghoul";

        public static bool Corrupted(Pawn p)
        {
            if (p == null || p.Dead || p.health?.hediffSet == null) return false;
            if (Raised(p) || Ghoul(p) || Possession.Is(p)) return true;
            if (p.MentalStateDef != null && MindStates.Contains(p.MentalStateDef.defName)) return true;
            return p.health.hediffSet.hediffs.Any(h => Corruptions.Contains(h.def.defName));
        }

        public static void Free(Pawn p, Pawn yuji)
        {
            // the unnatural corpse's awoken twin (her call 2026-11-07): gone for good, the way the game
            // ends it after study (the twin skips away and the corpse disappears; it never comes back)
            if (p.IsMutant && p.mutant.Def.defName == "AwokenCorpse" && ModsConfig.AnomalyActive && Find.Anomaly != null
                && Find.Anomaly.TryGetUnnaturalCorpseTrackerForAwoken(p, out var tracker) && tracker != null)
            {
                if (p.Spawned) Messages.Message($"{yuji.LabelShortCap} tears the void out of the unnatural corpse. It will not come back.",
                                                p, MessageTypeDefOf.PositiveEvent, false);
                tracker.Notify_Finished();
                return;
            }
            if (Raised(p))
            {
                // the game's own death of a mutant reverts it, so what's left is an ordinary corpse
                if (p.Spawned) Messages.Message($"{yuji.LabelShortCap} separates what was left of {p.LabelShort} from the void. Only a body remains.",
                                                p, MessageTypeDefOf.NeutralEvent, false);
                p.Kill(null);
                return;
            }
            Possession.Free(p);
            if (Ghoul(p)) p.mutant.Revert();
            if (p.MentalStateDef != null && MindStates.Contains(p.MentalStateDef.defName)) p.MentalState.RecoverFromState();
            foreach (var h in p.health.hediffSet.hediffs
                         .Where(h => Corruptions.Contains(h.def.defName) || GhoulOnly.Contains(h.def.defName)).ToList())
                p.health.RemoveHediff(h);
            // knocked out
            if (!p.health.hediffSet.HasHediff(HediffDefOf.Anesthetic))
            {
                var ko = p.health.AddHediff(HediffDefOf.Anesthetic);
                ko.Severity = 1f;
            }
            if (p.Spawned) Messages.Message($"{yuji.LabelShortCap} separates {p.LabelShort}'s soul from what was corrupting it. {p.LabelShortCap} collapses.",
                                            p, MessageTypeDefOf.NeutralEvent, false);
        }
    }

    /// <summary>A hit from Yuji on a corrupted pawn frees them instead of wounding them.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_SoulSeparation
    {
        [HarmonyPriority(Priority.High)]
        public static bool Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (!(dinfo.Instigator is Pawn yuji) || yuji == __instance || !Yuji.Is(yuji)) return true;
            if (!SoulSeparation.Corrupted(__instance)) return true;
            SoulSeparation.Free(__instance, yuji);
            absorbed = true;
            return false;
        }
    }
}
