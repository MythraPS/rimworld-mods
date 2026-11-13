using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using VanillaPsycastsExpanded;

namespace SukunaPsycasts
{
    /// <summary>
    /// He is NOT single-minded. The strongest psycaster is what he prefers and what
    /// he will cross a map for, but he fights whatever intercepts him, kills weak
    /// things for the pleasure of it, eats humanlike corpses by preference, and
    /// cooks. Traits for the pawn itself are decided later, at model time.
    /// </summary>
    public class SukunaBehaviour : GameComponent
    {
        private static Dictionary<int, float> transformed = new Dictionary<int, float>();
        private static HashSet<int> everTransformed = new HashSet<int>();
        private HashSet<int> eyesSet = new HashSet<int>();   // Sukunas whose Facial Animation eyes were made red once

        public SukunaBehaviour(Game game)
        {
            transformed = new Dictionary<int, float>();
            everTransformed = new HashSet<int>();
            oldNames = new Dictionary<int, string>();
            oldIdeos = new Dictionary<int, Ideo>();
        }

        public static void MarkTransformed(Pawn p, float power)
        {
            if (p == null) return;
            transformed[p.thingIDNumber] = power;
            everTransformed.Add(p.thingIDNumber);
        }

        /// <summary>Remembered even after he dies, so the same pawn can never become
        /// him a second time.</summary>
        /// <summary>Yuji cut him out: this pawn is no longer Sukuna (and can never become him again).</summary>
        public static void Unmark(Pawn p) { if (p != null) transformed.Remove(p.thingIDNumber); }

        // The host's own name while Sukuna wears them (her call 2026-11-02: they become "Ryomen Sukuna").
        private static Dictionary<int, string> oldNames = new Dictionary<int, string>();

        public static void Rename(Pawn p)
        {
            if (p == null) return;
            if (!oldNames.ContainsKey(p.thingIDNumber) && p.Name is NameTriple t)
                oldNames[p.thingIDNumber] = t.First + "\t" + t.Nick + "\t" + t.Last;
            p.Name = new NameTriple("Ryomen", "Sukuna", "Sukuna");
        }

        public static void RestoreName(Pawn p)
        {
            if (p == null || !oldNames.TryGetValue(p.thingIDNumber, out var s)) return;
            var parts = s.Split('\t');
            if (parts.Length == 3) p.Name = new NameTriple(parts[0], parts[1], parts[2]);
            oldNames.Remove(p.thingIDNumber);
        }

        // The host's own ideoligion, kept the same way (2026-11-06): someone freed from Sukuna
        // shouldn't come back believing what he believes.
        private static Dictionary<int, Ideo> oldIdeos = new Dictionary<int, Ideo>();
        private List<int> ideoKeysWork; private List<Ideo> ideoValsWork;

        public static void RememberIdeo(Pawn p, Ideo kings)
        {
            if (p?.Ideo == null || p.Ideo == kings || oldIdeos.ContainsKey(p.thingIDNumber)) return;
            oldIdeos[p.thingIDNumber] = p.Ideo;
        }

        public static void RestoreIdeo(Pawn p)
        {
            if (p?.ideo == null || !oldIdeos.TryGetValue(p.thingIDNumber, out var ideo)) return;
            oldIdeos.Remove(p.thingIDNumber);
            if (ideo != null && Find.IdeoManager.IdeosListForReading.Contains(ideo)) p.ideo.SetIdeo(ideo);
        }

        public static bool HasEverTransformed(Pawn p) =>
            p != null && everTransformed.Contains(p.thingIDNumber);

        /// <summary>There is only one King of Curses. While one is alive anywhere,
        /// nobody else can turn.</summary>
        public static bool AnySukunaExists()
        {
            if (SukunaArc.FreeSukunaAlive()) return true;   // off the map, but alive
            foreach (var id in transformed.Keys)
            {
                var pawn = FindPawn(id);
                if (pawn != null && !pawn.Dead) return true;
            }
            return false;
        }

        private static Pawn FindPawn(int id)
        {
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
                foreach (var p in maps[m].mapPawns.AllPawnsSpawned)
                    if (p.thingIDNumber == id) return p;
            return null;
        }

        public static bool IsSukuna(Pawn p) =>
            p != null && transformed.ContainsKey(p.thingIDNumber);

        public static float PowerOf(Pawn p) =>
            p != null && transformed.TryGetValue(p.thingIDNumber, out float v) ? v : 0f;

        /// <summary>Preferred target: the strongest psycaster on the map, scored off
        /// psycast level plus psylink level. He does not walk PAST a closer target to
        /// reach it — that check lives in the job giver, not here.</summary>
        public static Pawn PreferredTarget(Pawn sukuna)
        {
            if (sukuna?.Map == null) return null;
            Pawn best = null;
            float bestScore = -1f;
            foreach (var p in sukuna.Map.mapPawns.AllPawnsSpawned)
            {
                if (p == sukuna || p.Dead || p.Downed) continue;
                if (!p.RaceProps.Humanlike) continue;
                float score = ScoreOf(p);
                if (score > bestScore) { bestScore = score; best = p; }
            }
            return bestScore > 0f ? best : null;
        }

        private static float ScoreOf(Pawn p)
        {
            float score = 0f;
            var psy = p.health?.hediffSet?.hediffs
                        ?.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            if (psy != null)
            {
                score += psy.level * 2f;
                if (psy.psylink != null) score += psy.psylink.level;
            }
            return score;
        }

        /// <summary>
        /// His FIRST priority is the rest of himself. A loose finger anywhere on the
        /// map outranks any target, because every one he swallows makes him more of
        /// what he was. Fighting, killing and eating come after that.
        /// </summary>
        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 15 == 7) EatHeldFingers();
            if (Find.TickManager.TicksGame % 250 != 0) return;
            GiveRctOutput();
            KamutokeAI();
            var maps = Find.Maps;
            for (int m = 0; m < maps.Count; m++)
            {
                var map = maps[m];
                for (int i = 0; i < map.mapPawns.AllPawnsSpawned.Count; i++)
                {
                    var p = map.mapPawns.AllPawnsSpawned[i];
                    if (IsSukuna(p) && !p.Dead)
                    {
                        FourArmed.Check(p); Hypersensitive.Ensure(p);
                        // red eyes set once (her call 2026-11-07: Facial Animation's face tab can change them after)
                        if (!eyesSet.Contains(p.thingIDNumber) && SukunaEyes.Tint(p)) eyesSet.Add(p.thingIDNumber);
                        // older saves get his own look; a gun he picked up is dropped again
                        if (p.story != null && (p.story.skinColorOverride != Yuji.Skin || p.gender != Gender.Male
                            || (p.genes != null && p.genes.Xenotype != XenotypeDefOf.Baseliner))) SukunaLook.Apply(p);
                        else SukunaLook.DropWeapon(p);
                    }   // in case a hit was missed; older saves
                    if (!IsSukuna(p) || p.Dead || p.Downed) continue;
                    if (p.Faction != null && p.Faction.IsPlayer) continue;   // a friendly test Sukuna takes orders
                    if (p.CurJobDef == JobDefOf.Ingest || p.CurJobDef == JobDefOf.AttackMelee) continue;
                    TrySeekFinger(p, map);
                }
            }
        }

        /// <summary>When Sukuna gets a finger he eats it at once (her call 2026-11-11): any finger in his pack or his
        /// hands, picked up, taken in a raid, or handed to him, goes down. On the map or off it (the escaped King on
        /// his way to the lair). No takeover roll: he is already Sukuna.</summary>
        /// <summary>Every Sukuna has Reverse Cursed Technique: Output, healing others (her call 2026-11-11), like Yuta.</summary>
        public static void GiveRctOutput()
        {
            var def = JujutsuFundamentals.YutaAbilities.Def(JujutsuFundamentals.YutaAbilities.RctOutput);
            if (def == null) return;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (!IsSukuna(p)) continue;
                    var comp = p.GetComp<VEF.Abilities.CompAbilities>();
                    if (comp != null && !comp.HasAbility(def)) comp.GiveAbility(def);
                }
        }

        /// <summary>An enemy Sukuna holding Kamutoke calls its lightning when foes are near (the game won't press the button for him).</summary>
        public static void KamutokeAI()
        {
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (!IsSukuna(p) || p.Faction == Faction.OfPlayer) continue;
                    p.equipment?.Primary?.TryGetComp<CompKamutoke>()?.TryStrikeForAI(p);
                }
        }

        public static void EatHeldFingers()
        {
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                    if (IsSukuna(p) && !p.Dead) EatHeld(p);
            var king = SukunaArc.Instance?.sukuna;
            if (king != null && !king.Dead && !king.Spawned && IsSukuna(king)) EatHeld(king);
        }

        public static void EatHeld(Pawn p)
        {
            var held = new List<Thing>();
            if (p.inventory != null) held.AddRange(p.inventory.innerContainer.Where(t => t.def == SukunaThingDefOf.Sukuna_Finger));
            var carried = p.carryTracker?.CarriedThing;
            if (carried != null && carried.def == SukunaThingDefOf.Sukuna_Finger) held.Add(carried);
            if (held.Count == 0) return;
            int n = 0;
            bool was = CompCursedFinger.Silent;
            CompCursedFinger.Silent = true;
            try
            {
                foreach (var t in held)
                {
                    int count = t.stackCount;
                    if (!t.Destroyed) t.Destroy();
                    for (int i = 0; i < count; i++) { CompCursedFinger.Consume(p, rollTakeover: false); n++; }
                }
            }
            finally { CompCursedFinger.Silent = was; }
            if (n > 0 && p.Spawned)
            {
                MoteMaker.ThrowText(p.DrawPos, p.Map, n > 1 ? $"Swallows {n} fingers" : "Swallows a finger", new Color(0.85f, 0.15f, 0.15f), 3f);
                if (p.Map.IsPlayerHome || p.Faction == Faction.OfPlayer)
                    Messages.Message($"{p.LabelShortCap} swallowed {(n > 1 ? $"{n} fingers" : "a finger")} as soon as he had {(n > 1 ? "them" : "it")}. He is stronger for it.",
                        p, MessageTypeDefOf.NegativeEvent, false);
            }
        }

        private static void TrySeekFinger(Pawn sukuna, Map map)
        {
            Thing finger = GenClosest.ClosestThingReachable(
                sukuna.Position, map,
                ThingRequest.ForDef(SukunaThingDefOf.Sukuna_Finger),
                Verse.AI.PathEndMode.ClosestTouch,
                TraverseParms.For(sukuna), 9999f,
                t => t != null && !t.IsForbidden(sukuna) && sukuna.CanReserve(t));
            if (finger == null) return;

            var job = JobMaker.MakeJob(JobDefOf.Ingest, finger);
            job.count = 1;
            sukuna.jobs.StartJob(job, JobCondition.InterruptForced);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref transformed, "sukunaTransformed",
                                    LookMode.Value, LookMode.Value);
            if (transformed == null) transformed = new Dictionary<int, float>();
            Scribe_Collections.Look(ref oldNames, "sukunaOldNames", LookMode.Value, LookMode.Value);
            if (oldNames == null) oldNames = new Dictionary<int, string>();
            Scribe_Collections.Look(ref oldIdeos, "sukunaOldIdeos", LookMode.Value, LookMode.Reference, ref ideoKeysWork, ref ideoValsWork);
            if (oldIdeos == null) oldIdeos = new Dictionary<int, Ideo>();

            var ever = new List<int>(everTransformed);
            Scribe_Collections.Look(ref ever, "sukunaEverTransformed", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                everTransformed = ever == null ? new HashSet<int>() : new HashSet<int>(ever);
            var eyes = new List<int>(eyesSet);
            Scribe_Collections.Look(ref eyes, "sukunaFAEyes", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.LoadingVars) eyesSet = eyes == null ? new HashSet<int>() : new HashSet<int>(eyes);
        }
    }
}
