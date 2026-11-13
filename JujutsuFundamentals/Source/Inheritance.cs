using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Inherited cursed techniques (her calls 2026-11-12). A newborn has a 10% chance per parent who has a cursed
    /// technique to be born with one of that parent's techniques, never more than one. The child can learn it: its
    /// path opens to them (they spend points like on any path), and the first ability of it is theirs free once they
    /// have a psylink. Six Eyes is separate (a Biotech gene, normal gene rules) and so is Limitless from it.
    ///
    /// The techniques and who has them:
    ///  - Limitless (Gojo): the Limitless path, without needing the Six Eyes.
    ///  - Shrine (Sukuna, Yuji, Modulo Yuji): the Shrine path, without eating a finger.
    ///  - Cursed Speech (Inumaki): the Cursed Speech path.
    ///  - Boogie Woogie (Todo), Granite Blast (Ryu): their own small paths.
    ///  - Sky Manipulation (Uro), Jacob's Ladder (the Angel), Copy (Yuta): Yuta's tree, where those nodes live; only
    ///    their own nodes are learnable there (Copy: Copy and Paste). Copy without Rika works by the inheritor's own
    ///    rules (CopyAlone).
    ///  - Blood Manipulation (Choso, Yuji, Modulo Yuji): Choso's blood (Hemogenic, refills on its own, not a
    ///    vampire), so Hemosage opens.
    ///  - Ice Formation (Uraume): Frostshaper (already open to anyone; they start with its first ability).
    /// </summary>
    public static class Innate
    {
        public const float Chance = 0.10f;
        /// <summary>Tests only: replaces Chance when set.</summary>
        public static float? chanceOverride;

        public const string Limitless = "Limitless", Shrine = "Shrine", CursedSpeech = "CursedSpeech", BoogieWoogie = "BoogieWoogie",
                            GraniteBlast = "GraniteBlast", Sky = "Sky", Blood = "Blood", Ice = "Ice", Copy = "Copy", JacobsLadder = "JacobsLadder";
        public static readonly string[] All = { Limitless, Shrine, CursedSpeech, BoogieWoogie, GraniteBlast, Sky, Blood, Ice, Copy, JacobsLadder };

        public static string Label(string k)
        {
            switch (k)
            {
                case Limitless: return "Limitless";
                case Shrine: return "Shrine";
                case CursedSpeech: return "Cursed Speech";
                case BoogieWoogie: return "Boogie Woogie";
                case GraniteBlast: return "Granite Blast";
                case Sky: return "Sky Manipulation";
                case Blood: return "Blood Manipulation";
                case Ice: return "Ice Formation";
                case Copy: return "Copy";
                case JacobsLadder: return "Jacob's Ladder";
            }
            return k ?? "?";
        }

        public static HediffDef Def => DefDatabase<HediffDef>.GetNamedSilentFail("JJK_InnateTechnique");
        public static void OpenPath(Pawn p, PsycasterPathDef path)
        {
            var psy = p?.health?.hediffSet?.hediffs?.OfType<Hediff_PsycastAbilities>().FirstOrDefault();
            if (path != null && psy?.unlockedPaths != null && !psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            psy?.previousUnlockedPaths?.Remove(path);   // VPE hides a path's buttons once it was open and closed again
        }

        public static PsycasterPathDef BoogiePath => DefDatabase<PsycasterPathDef>.GetNamedSilentFail("JJK_BoogieWoogiePath");
        public static PsycasterPathDef GranitePath => DefDatabase<PsycasterPathDef>.GetNamedSilentFail("JJK_GraniteBlastPath");

        public static Hediff_InnateTechnique Marker(Pawn p) =>
            p?.health?.hediffSet?.hediffs?.OfType<Hediff_InnateTechnique>().FirstOrDefault();

        public static bool Marked(Pawn p, string k) => k != null && Marker(p)?.technique == k;

        /// <summary>Copy without Rika: someone born with Copy who isn't Yuta.</summary>
        public static bool CopiesAlone(Pawn p) => p != null && !Yuta.Is(p) && Marked(p, Copy);

        /// <summary>Opens Yuta's tree (for the nodes of their own technique).</summary>
        public static bool OpensYutaPath(Pawn p) => YutaNodes(Marker(p)?.technique) != null;

        /// <summary>The techniques this pawn has and could pass on.</summary>
        public static List<string> Of(Pawn p)
        {
            var l = new List<string>();
            if (p == null) return l;
            var m = Marker(p)?.technique;
            if (m != null) l.Add(m);
            AddIf(l, GojoPsycasts.Satoru.IsGojo(p), Limitless);
            AddIf(l, SukunaPsycasts.SukunaBehaviour.IsSukuna(p) || SukunaPsycasts.ModuloYuji.Is(p)
                     || (SukunaPsycasts.SukunaArcDefOf.Sukuna_ShrineImprint != null && p.health?.hediffSet?.HasHediff(SukunaPsycasts.SukunaArcDefOf.Sukuna_ShrineImprint) == true), Shrine);
            AddIf(l, Inumaki.Is(p), CursedSpeech);
            AddIf(l, CharacterTracker.IsTodo(p), BoogieWoogie);
            AddIf(l, Ryu.Is(p), GraniteBlast);
            AddIf(l, Uro.Is(p), Sky);
            AddIf(l, SukunaPsycasts.ChosoTracker.Is(p) || SukunaPsycasts.ModuloYuji.Is(p) || SukunaPsycasts.Yuji.Is(p), Blood);
            AddIf(l, SukunaPsycasts.UraumeTracker.Is(p), Ice);
            AddIf(l, Yuta.Is(p), Copy);
            AddIf(l, AngelChar.Is(p), JacobsLadder);
            return l;
        }

        private static void AddIf(List<string> l, bool yes, string k) { if (yes && !l.Contains(k)) l.Add(k); }

        public static PsycasterPathDef Path(string k)
        {
            switch (k)
            {
                case Limitless: return GojoPsycasts.GojoDefOf.Gojo_Path;
                case Shrine: return SukunaPsycasts.SukunaDefOf.Sukuna_Path;
                case CursedSpeech: return YutaDefOf.JJK_CursedSpeech;
                case BoogieWoogie: return BoogiePath;
                case GraniteBlast: return GranitePath;
                case Sky: case Copy: case JacobsLadder: return YutaDefOf.JJK_YutaPath;
                case Blood: return DefDatabase<PsycasterPathDef>.GetNamedSilentFail("VPEH_Hemosage");
                case Ice: return DefDatabase<PsycasterPathDef>.GetNamedSilentFail("VPE_Frostshaper");
            }
            return null;
        }

        /// <summary>The nodes of Yuta's tree that belong to this technique (null: not on Yuta's tree).</summary>
        public static string[] YutaNodes(string k)
        {
            switch (k)
            {
                case Copy: return new[] { YutaAbilities.Copy, YutaAbilities.Paste };
                case Sky: return YutaAbilities.Sky;
                case JacobsLadder: return new[] { JacobsLadderName };
            }
            return null;
        }

        public const string JacobsLadderName = "JJK_JacobsLadder";

        private static Dictionary<AbilityExtension_Psycast, VEFAbilityDef> extOwner;
        public static VEFAbilityDef AbilityOf(AbilityExtension_Psycast ext)
        {
            if (extOwner == null)
            {
                extOwner = new Dictionary<AbilityExtension_Psycast, VEFAbilityDef>();
                foreach (var d in DefDatabase<VEFAbilityDef>.AllDefsListForReading)
                {
                    var e = d.GetModExtension<AbilityExtension_Psycast>();
                    if (e != null && !extOwner.ContainsKey(e)) extOwner[e] = d;
                }
            }
            return ext != null && extOwner.TryGetValue(ext, out var def) ? def : null;
        }

        /// <summary>The ability they start with: the first of their technique's nodes (lowest level, then order).</summary>
        public static VEFAbilityDef First(string k)
        {
            IEnumerable<VEFAbilityDef> nodes;
            var yn = YutaNodes(k);
            if (yn != null) nodes = yn.Select(YutaAbilities.Def).Where(d => d != null);
            else
            {
                var path = Path(k);
                if (path == null) return null;
                nodes = DefDatabase<VEFAbilityDef>.AllDefsListForReading.Where(d => d.GetModExtension<AbilityExtension_Psycast>()?.path == path);
            }
            return nodes.Select(d => new { d, e = d.GetModExtension<AbilityExtension_Psycast>() })
                        .OrderBy(x => x.e?.level ?? 99).ThenBy(x => x.e?.order ?? 99).Select(x => x.d).FirstOrDefault();
        }

        /// <summary>A node on Yuta's tree for someone born with one of its techniques: only that technique's nodes,
        /// and only their prerequisites within it (Copy without Manifest Rika). False: not decided here.</summary>
        public static bool YutaNodeFor(AbilityExtension_Psycast ext, Pawn pawn, out bool allowed)
        {
            allowed = false;
            if (pawn == null || ext == null || ext.path != YutaDefOf.JJK_YutaPath || Yuta.Is(pawn)) return false;
            var nodes = YutaNodes(Marker(pawn)?.technique);
            if (nodes == null) return false;
            var def = AbilityOf(ext);
            if (def == null || !nodes.Contains(def.defName)) return true;
            var comp = pawn.GetComp<CompAbilities>();
            allowed = ext.prerequisites == null || ext.prerequisites.All(pr => pr == null || !nodes.Contains(pr.defName) || (comp != null && comp.HasAbility(pr)));
            return true;
        }

        /// <summary>Born with it. Blood Manipulation brings Choso's blood with it.</summary>
        public static Hediff_InnateTechnique Mark(Pawn p, string k)
        {
            if (p?.health == null || Def == null || k == null || Marker(p) != null) return null;
            var h = (Hediff_InnateTechnique)HediffMaker.MakeHediff(Def, p);
            h.technique = k;
            p.health.AddHediff(h);
            if (k == Blood) GiveBlood(p);
            return h;
        }

        public static void GiveBlood(Pawn p)
        {
            if (!ModsConfig.BiotechActive || p?.genes == null || p.genes.GetFirstGeneOfType<Gene_Hemogen>() != null) return;
            var hemogenic = DefDatabase<GeneDef>.GetNamedSilentFail("Hemogenic");
            if (hemogenic != null) p.genes.AddGene(hemogenic, true);   // a xenogene: the technique, not the gene, is what passes on
        }

        /// <summary>A newborn: a 10% chance for each parent with a technique, one at most.</summary>
        public static string Inherit(Pawn baby, Pawn mother, Pawn father)
        {
            if (baby == null || baby.Dead || Marker(baby) != null) return null;
            float chance = chanceOverride ?? Chance;
            var pool = new List<KeyValuePair<Pawn, string>>();
            foreach (var parent in new[] { mother, father })
            {
                if (parent == null) continue;
                var ts = Of(parent);
                if (ts.Count == 0 || !Rand.Chance(chance)) continue;
                pool.Add(new KeyValuePair<Pawn, string>(parent, ts.RandomElement()));
            }
            if (pool.Count == 0) return null;
            var pick = pool.RandomElement();
            if (Mark(baby, pick.Value) == null) return null;
            if (baby.Faction == Faction.OfPlayer)
            {
                var first = First(pick.Value);
                Find.LetterStack.ReceiveLetter("Born with a cursed technique",
                    $"{baby.LabelShortCap} was born with {pick.Key.LabelShort}'s cursed technique, {Label(pick.Value)}.\n\n"
                    + $"Once {baby.gender.GetPronoun()} has a psylink, {baby.gender.GetPronoun()} can learn it like any psycast path"
                    + (first != null ? $", and starts with {first.LabelCap} for free" : "") + ".",
                    LetterDefOf.PositiveEvent, baby);
            }
            return pick.Value;
        }

        /// <summary>Their technique wakes with their psylink: its path opens and the first ability is theirs, free.</summary>
        public static bool Start(Pawn p, Hediff_InnateTechnique h)
        {
            var psy = p?.health?.hediffSet?.hediffs?.OfType<Hediff_PsycastAbilities>().FirstOrDefault();
            if (psy == null || h == null || h.started) return false;
            h.started = true;
            var path = Path(h.technique);
            if (path != null && psy.unlockedPaths != null && !psy.unlockedPaths.Contains(path)) psy.unlockedPaths.Add(path);
            var first = First(h.technique);
            var comp = p.GetComp<CompAbilities>();
            if (first != null && comp != null && !comp.HasAbility(first)) comp.GiveAbility(first);
            if (p.Faction == Faction.OfPlayer && first != null && comp != null && comp.HasAbility(first))
                Messages.Message($"{p.LabelShortCap}'s cursed technique wakes: {Label(h.technique)}. {first.LabelCap} comes to {p.gender.GetObjective()} on its own.",
                                 p, MessageTypeDefOf.PositiveEvent, false);
            return true;
        }
    }

    /// <summary>The mark of a technique someone was born with.</summary>
    public class Hediff_InnateTechnique : HediffWithComps
    {
        public string technique;
        public bool started;

        public override string LabelBase => def.label + ": " + Innate.Label(technique);

        public override string TipStringExtra
        {
            get
            {
                var first = Innate.First(technique);
                return $"Born with {Innate.Label(technique)}. Its path is open to them" + (first != null ? $"; {first.LabelCap} comes free with their psylink." : ".");
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref technique, "jjkInnateTechnique");
            Scribe_Values.Look(ref started, "jjkInnateStarted");
        }
    }

    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class Patch_InheritTechnique
    {
        public static void Postfix(Thing __result, Pawn geneticMother, Pawn father)
        {
            try { Innate.Inherit(__result as Pawn, geneticMother, father); }
            catch (Exception e) { Log.Warning("[JJK] inherited technique: " + e.Message); }
        }
    }

    /// <summary>Boogie Woogie's and Granite Blast's paths: Todo's and Ryu's, and anyone born with them.</summary>
    [HarmonyPatch(typeof(PsycasterPathDef), nameof(PsycasterPathDef.CanPawnUnlock))]
    public static class Patch_InnatePathGate
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(PsycasterPathDef __instance, Pawn pawn, ref bool __result)
        {
            if (__instance == null || pawn == null) return;
            if (__instance == Innate.BoogiePath) __result = CharacterTracker.IsTodo(pawn) || Innate.Marked(pawn, Innate.BoogieWoogie);
            else if (__instance == Innate.GranitePath) __result = Ryu.Is(pawn) || Innate.Marked(pawn, Innate.GraniteBlast);
        }
    }

    /// <summary>Wakes inborn techniques once a psylink comes, and keeps older saves in step: Todo's and Ryu's
    /// abilities now sit on their own paths, so those paths are opened for them.</summary>
    public class InnateKeeper : GameComponent
    {
        public InnateKeeper(Game game) { }

        public override void GameComponentTick()
        {
            int now = Find.TickManager.TicksGame;
            if (now % 250 != 137) return;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                    if (p.RaceProps.Humanlike) Check(p);
            if (now % 2500 == 137)
                foreach (var c in Find.WorldObjects.Caravans)
                    foreach (var p in c.PawnsListForReading)
                        if (p.RaceProps.Humanlike) Check(p);
        }

        public static void Check(Pawn p)
        {
            var h = Innate.Marker(p);
            if (h != null && !h.started) Innate.Start(p, h);
            var psy = p.health?.hediffSet?.hediffs?.OfType<Hediff_PsycastAbilities>().FirstOrDefault();
            if (psy?.unlockedPaths == null) return;
            if (CharacterTracker.IsTodo(p) && Innate.BoogiePath != null && !psy.unlockedPaths.Contains(Innate.BoogiePath)) Innate.OpenPath(p, Innate.BoogiePath);
            if (Ryu.Is(p) && Innate.GranitePath != null && !psy.unlockedPaths.Contains(Innate.GranitePath)) Innate.OpenPath(p, Innate.GranitePath);
            // a Six Eyes child from before 2026-11-12 got Limitless with the eyes: they keep it, as inborn Limitless
            if (h == null && GojoPsycasts.Gojo.HasSixEyes(p) && !GojoPsycasts.Satoru.IsGojo(p) && psy.unlockedPaths.Contains(GojoPsycasts.GojoDefOf.Gojo_Path))
            {
                var m = Innate.Mark(p, Innate.Limitless);
                if (m != null) m.started = true;
            }
        }
    }

    // ------------------------------------------------------------------ Copy without Rika (her rules 2026-11-12)

    /// <summary>
    /// Someone born with Copy (not Yuta) copies on their own, three ways:
    ///  1. killing a psycaster with a psycast: each of the dead one's techniques is copied with a chance of 20% x the
    ///     killer's psychic sensitivity, at least one (proposal);
    ///  2. eating part of a downed or held psycaster themselves (their Copy, the same part sizes as Rika's);
    ///  3. having a psycaster's organ put in them: the organ remembers its donor's techniques, worth as much as the
    ///     same part eaten (heart: all; lung, kidney, liver: three).
    /// </summary>
    public static class CopyAlone
    {
        public const float KillChancePerSensitivity = 0.2f;

        public static bool Psycaster(Pawn p) => p?.health?.hediffSet?.hediffs?.Any(h => h is Hediff_PsycastAbilities) == true;

        /// <summary>What a donor or victim could give anyone: never what's Yuta's alone, Shrine only from Yuji.</summary>
        public static List<VEFAbilityDef> Techniques(Pawn from)
        {
            var tc = from?.GetComp<CompAbilities>();
            if (tc == null) return new List<VEFAbilityDef>();
            bool yuji = SukunaPsycasts.Yuji.Is(from) || SukunaPsycasts.ModuloYuji.Is(from);
            return tc.LearnedAbilities.Select(a => a.def).Where(d => d != null && d.GetModExtension<AbilityExtension_Psycast>() != null).Distinct()
                     .Where(d => !YutaAbilities.IsYutaOnly(d) && !JacobsLadder.Is(d) && (yuji || !CopyRules.IsShrine(d))).ToList();
        }

        public static void FromKill(Pawn killer, Pawn victim)
        {
            var kc = killer?.GetComp<CompAbilities>();
            if (kc == null) return;
            var have = Techniques(victim).Where(d => !kc.HasAbility(d)).ToList();
            if (have.Count == 0) return;
            float chance = Mathf.Clamp01(KillChancePerSensitivity * killer.GetStatValue(StatDefOf.PsychicSensitivity));
            var gain = have.Where(d => Rand.Chance(chance)).ToList();
            if (gain.Count == 0) gain.Add(have.RandomElement());
            CopyRules.Learn(killer, gain, victim);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_CopyOnKill
    {
        public static void Prefix(Pawn __instance, DamageInfo? dinfo, ref Pawn __state)
        {
            __state = null;
            try
            {
                var killer = dinfo?.Instigator as Pawn ?? PsycastContext.Caster as Pawn;
                if (__instance == null || __instance.Dead || killer == null || killer == __instance || !Innate.CopiesAlone(killer)) return;
                if (PsycastContext.Caster != killer || !CopyAlone.Psycaster(__instance)) return;
                __state = killer;
            }
            catch (Exception e) { Log.Warning("[JJK] Copy on a kill: " + e.Message); }
        }

        public static void Postfix(Pawn __instance, Pawn __state)
        {
            if (__state == null) return;
            try { CopyAlone.FromKill(__state, __instance); }
            catch (Exception e) { Log.Warning("[JJK] Copy on a kill: " + e.Message); }
        }
    }

    /// <summary>Organs taken from a psycaster remember what they could do.</summary>
    public class OrganMemory : GameComponent
    {
        public static OrganMemory Instance;
        private Dictionary<int, List<string>> techniques = new Dictionary<int, List<string>>();
        private Dictionary<int, string> donors = new Dictionary<int, string>();

        public OrganMemory(Game game) { Instance = this; }

        public void Remember(Thing organ, Pawn donor, List<VEFAbilityDef> list)
        {
            if (organ == null || list == null || list.Count == 0) return;
            techniques[organ.thingIDNumber] = list.Select(d => d.defName).ToList();
            donors[organ.thingIDNumber] = donor?.LabelShort ?? "someone";
        }

        public List<VEFAbilityDef> Take(Thing organ, out string donor)
        {
            donor = null;
            if (organ == null || !techniques.TryGetValue(organ.thingIDNumber, out var names)) return null;
            techniques.Remove(organ.thingIDNumber);
            donors.TryGetValue(organ.thingIDNumber, out donor);
            donors.Remove(organ.thingIDNumber);
            return names.Select(n => DefDatabase<VEFAbilityDef>.GetNamedSilentFail(n)).Where(d => d != null).ToList();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Collections.Look(ref donors, "jjkOrganDonors", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.LoadingVars)
            {
                var keys = techniques.Keys.ToList();
                var vals = techniques.Values.Select(v => string.Join(",", v)).ToList();
                Scribe_Collections.Look(ref keys, "jjkOrganIds", LookMode.Value);
                Scribe_Collections.Look(ref vals, "jjkOrganTechniques", LookMode.Value);
                if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    techniques = new Dictionary<int, List<string>>();
                    if (keys != null && vals != null)
                        for (int i = 0; i < keys.Count && i < vals.Count; i++)
                            techniques[keys[i]] = vals[i].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                }
            }
            if (donors == null) donors = new Dictionary<int, string>();
            if (techniques == null) techniques = new Dictionary<int, List<string>>();
        }
    }

    [HarmonyPatch(typeof(MedicalRecipesUtility), nameof(MedicalRecipesUtility.SpawnNaturalPartIfClean))]
    public static class Patch_OrganRemembers
    {
        public static void Postfix(Pawn pawn, Thing __result)
        {
            if (__result == null || pawn == null || OrganMemory.Instance == null) return;
            try { if (CopyAlone.Psycaster(pawn)) OrganMemory.Instance.Remember(__result, pawn, CopyAlone.Techniques(pawn)); }
            catch (Exception e) { Log.Warning("[JJK] organ memory: " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(Recipe_InstallNaturalBodyPart), nameof(Recipe_InstallNaturalBodyPart.ApplyOnPawn))]
    public static class Patch_CopyFromOrgan
    {
        public static void Postfix(Pawn pawn, BodyPartRecord part, List<Thing> ingredients)
        {
            if (pawn == null || ingredients == null || OrganMemory.Instance == null) return;
            try
            {
                bool copies = Innate.CopiesAlone(pawn);
                foreach (var t in ingredients)
                {
                    var list = OrganMemory.Instance.Take(t, out string donor);
                    if (!copies || list == null || part == null) continue;
                    var comp = pawn.GetComp<CompAbilities>();
                    int worth = CopyRules.Worth(part);
                    if (comp == null || worth <= 0) continue;
                    var gain = list.Where(d => !comp.HasAbility(d)).InRandomOrder().Take(worth == CopyRules.All ? int.MaxValue : worth).ToList();
                    CopyRules.Learn(pawn, gain, null, donor);
                }
            }
            catch (Exception e) { Log.Warning("[JJK] Copy from an organ: " + e.Message); }
        }
    }
}
