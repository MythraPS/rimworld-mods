using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using VanillaPsycastsExpanded;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace SukunaPsycasts
{
    [DefOf]
    public static class SukunaArcDefOf
    {
        public static MentalStateDef Sukuna_Possessed;
        public static JobDef Sukuna_ForceFinger;
        public static HediffDef Sukuna_Collapse;
        public static HediffDef Sukuna_ShrineImprint;
        public static HediffDef Sukuna_SoulSevered;
        public static HediffDef Sukuna_JujutsuPinnacle;
        public static HediffDef Sukuna_BloodPoison;
        public static DamageDef Sukuna_SoulSlash;
        public static HediffDef Sukuna_SoulWound;
        public static VEFAbilityDef Sukuna_BenevolentShrine;
        public static VEFAbilityDef Sukuna_PiercingBloodScatter;
        public static ThingDef Sukuna_BloodBullet;
        public static ThingDef Sukuna_DeathPainting;
        public static IncidentDef Sukuna_PaintingRumour;
        public static SitePartDef Sukuna_Lair;
        public static QuestScriptDef Sukuna_Painting_Ruin;
        public static QuestScriptDef Sukuna_Painting_Held;
        public static QuestScriptDef Sukuna_Painting_Convoy;

        static SukunaArcDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(SukunaArcDefOf));
    }

    public enum ArcStage { None, Escaping, Hunting, Lair, Done }

    /// <summary>
    /// The questline after Yuji joins (her call 2026-11-01). From 10 fingers on, Sukuna may take
    /// Yuji over for a minute: he tears off Yuji's pinky and forces it down someone else's throat,
    /// and that person becomes the four-armed King. Sukuna eats every loose finger on the map and
    /// walks off it, leaving Yuji out cold. Yuji grows (meditation, finger and painting quests,
    /// Death Paintings for Hemosage) until both Shrine and Hemosage are full, which gives him
    /// Benevolent Shrine and shows where Sukuna is. Cutting Sukuna's soul loose finishes him; the
    /// host lives. Yuji then takes his Modulo form.
    /// </summary>
    public class SukunaArc : GameComponent
    {
        public static SukunaArc Instance => Current.Game?.GetComponent<SukunaArc>();

        public ArcStage stage = ArcStage.None;
        public Pawn sukuna;                 // the body he escaped in
        public Faction hostFaction;         // the host's faction before he took them
        public HairDef hostHair;
        public Color hostHairColor = Color.white;
        public int fingersDestroyed;        // cut out of him for good: never come back into the world
        public int paintingsEaten;
        public int nextPaintingTick = -1;
        public int lairRetryTick = -1;
        public Site lair;

        public static readonly FloatRange PaintingEveryDays = new FloatRange(18f, 28f);
        public const float TakeoverMtbDays = 10f;
        public const int TakeoverFrom = 10;

        private readonly List<Pawn> toExorcise = new List<Pawn>();

        public SukunaArc(Game game) { }

        public static Pawn Yuji_ => QuestlineTracker.Instance?.yuji;

        public static bool FreeSukunaAlive()
        {
            var a = Instance;
            return a != null && a.sukuna != null && !a.sukuna.Dead && !a.sukuna.Discarded && a.stage != ArcStage.Done;
        }

        // ------------------------------------------------------------------ ticking

        public override void GameComponentTick()
        {
            if (toExorcise.Count > 0)
            {
                var list = toExorcise.ToList();
                toExorcise.Clear();
                foreach (var p in list) Exorcise(p);
            }

            int now = Find.TickManager.TicksGame;
            if (now % 250 == 0) Steer();
            if (now % GenTicks.TickLongInterval != 0) return;

            var yuji = Yuji_;
            var q = QuestlineTracker.Instance;
            bool joined = q != null && q.stage == YujiStage.Joined && yuji != null && !yuji.Dead;

            // Paintings start turning up once Yuji is in the colony.
            if (joined && Hemosage != null)
            {
                if (nextPaintingTick < 0) nextPaintingTick = now + Days(PaintingEveryDays);
                if (now >= nextPaintingTick)
                {
                    nextPaintingTick = now + Days(PaintingEveryDays);
                    var map = Find.AnyPlayerHomeMap;
                    if (map != null && PaintingCensus.Remaining() > 0) Fire(SukunaArcDefOf.Sukuna_PaintingRumour, map);
                }
            }

            switch (stage)
            {
                case ArcStage.None:
                    if (joined && Scaling.FingersEaten(yuji) >= TakeoverFrom
                        && Rand.MTBEventOccurs(TakeoverMtbDays, GenDate.TicksPerDay, GenTicks.TickLongInterval))
                        TryTakeover(yuji, false);
                    break;
                case ArcStage.Escaping:
                    if (SukunaDead()) { SukunaFell(null); break; }
                    if (!sukuna.Spawned)
                    {
                        stage = ArcStage.Hunting;
                        Find.LetterStack.ReceiveLetter("Sukuna is gone",
                            ("The King of Curses has left in the body he stole, with every finger he could find. Yuji is the only one who can "
                             + "follow him, and he is not strong enough yet.\n\nYuji keeps the Shrine technique Sukuna carved into him. "
                             + "Once he has learned all of Shrine and all of Hemosage (meditate, take on finger and painting quests, and "
                             + "let him swallow the Death Paintings), he will find a technique of his own, and he will know where Sukuna is.")
                                .Formatted(sukuna.LabelShortCap),
                            LetterDefOf.NegativeEvent, yuji);
                    }
                    break;
                case ArcStage.Hunting:
                    if (SukunaDead()) { SukunaFell(null); break; }
                    if (yuji != null && !yuji.Dead && Masters(yuji))
                    {
                        Awaken(yuji);
                        OpenLair();
                    }
                    break;
                case ArcStage.Lair:
                    if (SukunaDead()) { SukunaFell(null); break; }
                    if ((lair == null || lair.Destroyed) && !sukuna.Spawned)
                    {
                        // The site closed with him still out there: he turns up somewhere else a few days later.
                        if (lairRetryTick < 0) lairRetryTick = now + 3 * GenDate.TicksPerDay;
                        else if (now >= lairRetryTick) { lairRetryTick = -1; OpenLair(); }
                    }
                    break;
            }
        }

        private bool SukunaDead() => sukuna == null || sukuna.Dead || sukuna.Discarded;

        private static int Days(FloatRange r) => Mathf.RoundToInt(r.RandomInRange * GenDate.TicksPerDay);

        private static void Fire(IncidentDef def, Map map)
        {
            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
            parms.forced = true;
            def.Worker.TryExecute(parms);
        }

        /// <summary>While he is still on the home map: fingers first (SukunaBehaviour does that),
        /// then whoever stands in his way, then out.</summary>
        private void Steer()
        {
            if (stage != ArcStage.Escaping || sukuna == null || !sukuna.Spawned || sukuna.Dead || sukuna.Downed) return;
            if (sukuna.CurJobDef == JobDefOf.Ingest || sukuna.CurJobDef == JobDefOf.AttackMelee) return;
            var map = sukuna.Map;
            var blocker = GenRadial.RadialDistinctThingsAround(sukuna.Position, map, 2.9f, true)
                .OfType<Pawn>()
                .FirstOrDefault(p => p != sukuna && !p.Dead && !p.Downed && p.HostileTo(sukuna));
            if (blocker != null)
            {
                var job = JobMaker.MakeJob(JobDefOf.AttackMelee, blocker);
                job.expiryInterval = 300;
                sukuna.jobs.StartJob(job, JobCondition.InterruptForced);
            }
        }

        // ------------------------------------------------------------------ the takeover

        /// <summary>The nearest person Sukuna can reach: anyone but Yuji, friend or foe.</summary>
        public static Pawn PickHost(Pawn yuji)
        {
            if (yuji?.Map == null) return null;
            return yuji.Map.mapPawns.AllPawnsSpawned
                .Where(p => p != yuji && !p.Dead && p.RaceProps.Humanlike && !p.DevelopmentalStage.Juvenile()
                            && !Yuji.Is(p) && !SukunaBehaviour.IsSukuna(p) && !SukunaBehaviour.HasEverTransformed(p)
                            && yuji.CanReach(p, PathEndMode.Touch, Danger.Deadly))
                .OrderBy(p => p.Position.DistanceToSquared(yuji.Position))
                .FirstOrDefault();
        }

        public bool TryTakeover(Pawn yuji, bool forced)
        {
            if (yuji == null || !yuji.Spawned || yuji.Dead || yuji.Downed) return false;
            if (!forced && (stage != ArcStage.None || SukunaBehaviour.AnySukunaExists())) return false;
            if (PickHost(yuji) == null) return false;
            yuji.jobs?.StopAll();
            if (!yuji.mindState.mentalStateHandler.TryStartMentalState(SukunaArcDefOf.Sukuna_Possessed,
                    forced: true, forceWake: true, transitionSilently: true)) return false;
            Find.LetterStack.ReceiveLetter("Sukuna has taken over Yuji",
                "Yuji's eyes have changed. The markings are back on his face, and the one looking out of it is the King of Curses.\n\n"
                + "He only has a minute before Yuji takes his body back, and he knows exactly what to do with it.",
                LetterDefOf.ThreatBig, yuji);
            return true;
        }

        /// <summary>The pinky goes down the host's throat: Yuji's fingers become theirs, they turn into the
        /// four-armed King, and he heads off the map. Yuji drops.</summary>
        public void Transfer(Pawn yuji, Pawn host)
        {
            if (yuji == null || host == null || host.Dead) return;
            var vessel = yuji.health.hediffSet.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            int fingers = Mathf.Max(1, vessel == null ? 1 : Mathf.RoundToInt(vessel.Severity));
            if (vessel != null) yuji.health.RemoveHediff(vessel);
            Imprint(yuji);
            ModuloYuji.RemoveFinger(yuji, "left pinky");

            string hostName = host.LabelShortCap;
            hostFaction = host.Faction;
            hostHair = host.story?.hairDef;
            hostHairColor = host.story?.HairColor ?? Color.white;
            if (host.guest != null && (host.IsPrisoner || host.IsSlave)) host.guest.SetGuestStatus(null);
            host.jobs?.StopAll();

            VesselMaker.Make(host, fingers);
            Transformation.Become(host, sendLetter: false, forceHostile: true);
            if (!SukunaBehaviour.IsSukuna(host)) { Log.Warning("[Ryomen Sukuna] takeover: host did not turn"); return; }
            FourArmed.Take(host, letter: false);

            sukuna = host;
            Find.WorldPawns.ForcefullyKeptPawns.Add(host);
            stage = ArcStage.Escaping;
            if (host.Spawned)
            {
                host.GetLord()?.RemovePawn(host);
                LordMaker.MakeNewLord(host.Faction, new LordJob_ExitMapBest(LocomotionUrgency.Jog, false, true), host.Map, new[] { host });
            }

            if (yuji.InMentalState) yuji.MentalState.RecoverFromState();
            yuji.health.AddHediff(SukunaArcDefOf.Sukuna_Collapse);

            Find.LetterStack.ReceiveLetter("The King of Curses is free",
                ("Sukuna tore off Yuji's little finger and forced it down {0}'s throat. With it went every finger Yuji had swallowed: "
                 + "{1} of them. {0} is gone. What stands up is the King in his true form, four arms and all.\n\n"
                 + "He will eat every loose finger on the map, cut down whoever gets in his way, and leave. Yuji is out cold.")
                    .Formatted(hostName, fingers),
                LetterDefOf.ThreatBig, host);
        }

        // ------------------------------------------------------------------ Yuji's growth

        public static PsycasterPathDef Hemosage => DefDatabase<PsycasterPathDef>.GetNamedSilentFail("VPEH_Hemosage");

        public static void Imprint(Pawn p)
        {
            if (p?.health != null && !p.health.hediffSet.HasHediff(SukunaArcDefOf.Sukuna_ShrineImprint))
                p.health.AddHediff(SukunaArcDefOf.Sukuna_ShrineImprint);
        }

        public static bool HasBenevolent(Pawn p) =>
            p?.GetComp<CompAbilities>()?.HasAbility(SukunaArcDefOf.Sukuna_BenevolentShrine) == true;

        /// <summary>Every Shrine technique (Benevolent Shrine stands in for Malevolent) and, when the
        /// Hemosage mod is there, every Hemosage technique.</summary>
        public static bool Masters(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) return false;
            var shrine = SukunaDefOf.Sukuna_Path?.abilities;
            if (shrine == null) return false;
            foreach (var a in shrine)
                if (!comp.HasAbility(a) && !(a == SukunaAbilityDefOf.Sukuna_MalevolentShrine && HasBenevolent(p))) return false;
            var hemo = Hemosage?.abilities;
            if (hemo != null && hemo.Any(a => !comp.HasAbility(a))) return false;
            return true;
        }

        /// <summary>Benevolent Shrine in place of Malevolent Shrine.</summary>
        public static void GrantBenevolent(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp == null) return;
            if (!comp.HasAbility(SukunaArcDefOf.Sukuna_BenevolentShrine)) comp.GiveAbility(SukunaArcDefOf.Sukuna_BenevolentShrine);
            comp.LearnedAbilities.RemoveAll(a => a.def == SukunaAbilityDefOf.Sukuna_MalevolentShrine);
        }

        private void Awaken(Pawn yuji)
        {
            if (HasBenevolent(yuji)) return;
            GrantBenevolent(yuji);
            Find.LetterStack.ReceiveLetter("Benevolent Shrine",
                "Yuji has mastered Shrine and his brother's blood, and between them he has found a technique of his own. "
                + "Domain Expansion: Benevolent Shrine replaces Malevolent Shrine. It cuts no matter at all, only the souls of "
                + "those hostile to him and his friends, and it can cut the King of Curses loose from the body he stole. "
                + "So can Yuji's fists, his slashes and his Black Flashes.\n\nAnd now he can feel where Sukuna is.",
                LetterDefOf.PositiveEvent, yuji);
        }

        /// <summary>Dev mode: open the lair now, whatever Yuji has learned.</summary>
        public void DebugOpenLair() => OpenLair();

        private void OpenLair()
        {
            var home = Find.AnyPlayerHomeMap;
            if (home == null || SukunaDead()) return;
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, home.Tile, 4, 12)) return;
            var site = SiteMaker.MakeSite(SukunaArcDefOf.Sukuna_Lair, tile, SukunaFaction.Get(), true, null);
            if (site == null) return;
            Find.WorldObjects.Add(site);
            lair = site;
            stage = ArcStage.Lair;
            Find.LetterStack.ReceiveLetter("Sukuna has been found",
                "Yuji can feel him: the King of Curses is waiting not far away, marked on the world map. Take Yuji there "
                + "by caravan or shuttle, with whoever else you trust, and end it.\n\nBenevolent Shrine, Yuji's punches, slashes "
                + "and Black Flashes all cut Sukuna's soul loose. When it is cut all the way, the fingers go with him and the host lives.",
                LetterDefOf.PositiveEvent, site);
        }

        // ------------------------------------------------------------------ the end of him

        /// <summary>Yuji's soul cuts on the King. Applied from damage hooks; the exorcism itself
        /// waits for the next tick so it never runs in the middle of damage being applied.</summary>
        public static void Sever(Pawn sukunaPawn, float amount)
        {
            if (sukunaPawn == null || sukunaPawn.Dead || amount <= 0f || !SukunaBehaviour.IsSukuna(sukunaPawn)) return;
            var h = sukunaPawn.health.hediffSet.GetFirstHediffOfDef(SukunaArcDefOf.Sukuna_SoulSevered)
                    ?? sukunaPawn.health.AddHediff(SukunaArcDefOf.Sukuna_SoulSevered);
            h.Severity = Mathf.Min(1f, h.Severity + amount);
            var arc = Instance;
            if (h.Severity >= 1f && arc != null && !arc.toExorcise.Contains(sukunaPawn)) arc.toExorcise.Add(sukunaPawn);
        }

        /// <summary>Sukuna torn out of his host. The fingers are destroyed, the host lives and wakes up
        /// as themselves.</summary>
        public void Exorcise(Pawn host)
        {
            if (host == null || host.Dead || !SukunaBehaviour.IsSukuna(host)) return;
            var vessel = host.health.hediffSet.GetFirstHediffOfDef(SukunaDefOf.Sukuna_Vessel);
            int fingers = vessel == null ? 0 : Mathf.RoundToInt(vessel.Severity);
            if (vessel != null) host.health.RemoveHediff(vessel);
            fingersDestroyed += fingers;
            foreach (var def in new[] { SukunaDefOf.Sukuna_FourArmed, SukunaDefOf.Sukuna_ExtraArms, SukunaDefOf.Sukuna_BellyMouth, SukunaDefOf.Sukuna_CurseMarks, SukunaDefOf.Sukuna_Mask, SukunaArcDefOf.Sukuna_SoulSevered, SukunaArcDefOf.Sukuna_ShrineImprint })
            {
                var h = def == null ? null : host.health.hediffSet.GetFirstHediffOfDef(def);
                if (h != null) host.health.RemoveHediff(h);
            }

            var comp = host.GetComp<CompAbilities>();
            var path = SukunaDefOf.Sukuna_Path;
            if (comp != null && path?.abilities != null) comp.LearnedAbilities.RemoveAll(a => path.abilities.Contains(a.def));
            var psy = host.health.hediffSet.hediffs.Find(h => h is Hediff_PsycastAbilities) as Hediff_PsycastAbilities;
            psy?.unlockedPaths?.Remove(path);
            var selfish = DefDatabase<TraitDef>.GetNamedSilentFail("Sukuna_Selfish");
            var st = selfish == null ? null : host.story?.traits?.GetTrait(selfish);
            if (st != null) host.story.traits.RemoveTrait(st);
            if (host.style != null) host.style.FaceTattoo = TattooDefOf.NoTattoo_Face;

            bool escaped = host == sukuna;
            if (escaped && hostHair != null) { host.story.hairDef = hostHair; host.story.HairColor = hostHairColor; }
            SukunaBehaviour.Unmark(host);
            SukunaBehaviour.RestoreName(host);
            SukunaBehaviour.RestoreIdeo(host);

            host.GetLord()?.RemovePawn(host);
            // The escaped King's host goes back where they came from; anyone else leaves the King of Curses'
            // faction for none, and a friendly test Sukuna simply stays in the colony.
            var back = escaped ? hostFaction : (host.Faction == SukunaFaction.Existing() ? null : host.Faction);
            if (back != null && back.defeated) back = null;
            if (host.Faction != back) host.SetFaction(back);
            host.health.AddHediff(SukunaArcDefOf.Sukuna_Collapse);
            host.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(host);
            if (host.Map != null) SlashUtility.BlackSparks(host);

            if (escaped) { Find.WorldPawns.ForcefullyKeptPawns.Remove(host); Victory(host, fingers); }
            else Find.LetterStack.ReceiveLetter("Sukuna cut loose",
                ("Yuji has cut the King of Curses out of {0}. The {1} fingers went with him and are gone for good. "
                 + "{0} is unconscious, but alive, and themselves again.").Formatted(host.LabelShortCap, fingers),
                LetterDefOf.PositiveEvent, host);
        }

        /// <summary>Yuji killed the King outright: the fingers are destroyed with him, and it is Yuji's
        /// victory all the same.</summary>
        public static void YujiKilled(Pawn body, int fingers)
        {
            var arc = Instance;
            if (arc == null) return;
            arc.fingersDestroyed += fingers;
            if (body.MapHeld != null && body.PositionHeld.IsValid)
                FleckMaker.ThrowSmoke(body.PositionHeld.ToVector3Shifted(), body.MapHeld, 2f);
            if (body != arc.sukuna || arc.stage == ArcStage.Done)
            {
                Find.LetterStack.ReceiveLetter("Sukuna destroyed",
                    ("Yuji finished off the King of Curses, and the {0} fingers inside him went with him. They are gone for good.").Formatted(fingers),
                    LetterDefOf.PositiveEvent, body.Corpse != null ? new LookTargets(body.Corpse) : LookTargets.Invalid);
                return;
            }
            Find.WorldPawns.ForcefullyKeptPawns.Remove(body);
            arc.stage = ArcStage.Done;
            var yuji = Yuji_;
            if (yuji == null || yuji.Dead) return;
            ModuloYuji.Ascend(yuji);
            Find.LetterStack.ReceiveLetter("The pinnacle of jujutsu",
                ("Yuji killed the King of Curses, and the last {0} fingers were destroyed with him. Ryomen Sukuna is dead.\n\n"
                 + "Yuji Itadori has reached the pinnacle of jujutsu. His hair has gone white, and his body has stopped keeping "
                 + "track of the years. Everything he learned on the way has settled into him (his psycaster stats now carry "
                 + "sixty upgrades), and his blood has become something of its own: Piercing Blood that scatters, hunts down "
                 + "every enemy in reach and puts them to sleep for three days.").Formatted(fingers),
                LetterDefOf.PositiveEvent, yuji);
        }

        /// <summary>He died the ordinary way. Only Yuji's soul cuts make that a victory.</summary>
        private void SukunaFell(Pawn _)
        {
            if (sukuna != null) Find.WorldPawns.ForcefullyKeptPawns.Remove(sukuna);
            stage = ArcStage.Done;
            Find.LetterStack.ReceiveLetter("Sukuna is dead",
                "The body the King of Curses escaped in has died, and the fingers inside it fell out with it. "
                + "Yuji never got to settle things with him himself.",
                LetterDefOf.NeutralEvent, Yuji_);
        }

        private void Victory(Pawn host, int fingers)
        {
            stage = ArcStage.Done;
            var yuji = Yuji_;
            if (yuji == null || yuji.Dead)
            {
                Find.LetterStack.ReceiveLetter("Sukuna is gone", "The King of Curses has been cut out of his host for good.", LetterDefOf.PositiveEvent, host);
                return;
            }
            ModuloYuji.Ascend(yuji);
            Find.LetterStack.ReceiveLetter("The pinnacle of jujutsu",
                ("Yuji cut the King of Curses out of {0}'s body, and the last {1} fingers went with him. Ryomen Sukuna is dead. "
                 + "{0} will wake up as themselves.\n\nYuji Itadori has reached the pinnacle of jujutsu. His hair has gone white, and "
                 + "his body has stopped keeping track of the years. Everything he learned on the way has settled into him "
                 + "(his psycaster stats now carry sixty upgrades), and his blood has become something of its own: "
                 + "Piercing Blood that scatters, hunts down every enemy in reach and puts them to sleep for three days.")
                    .Formatted(host.LabelShortCap, fingers),
                LetterDefOf.PositiveEvent, yuji);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stage, "sukunaArcStage", ArcStage.None);
            Scribe_References.Look(ref sukuna, "sukunaArcSukuna", true);
            Scribe_References.Look(ref hostFaction, "sukunaArcHostFaction");
            Scribe_Defs.Look(ref hostHair, "sukunaArcHostHair");
            Scribe_Values.Look(ref hostHairColor, "sukunaArcHostHairColor", Color.white);
            Scribe_Values.Look(ref fingersDestroyed, "sukunaArcFingersDestroyed", 0);
            Scribe_Values.Look(ref paintingsEaten, "sukunaArcPaintingsEaten", 0);
            Scribe_Values.Look(ref nextPaintingTick, "sukunaArcNextPainting", -1);
            Scribe_Values.Look(ref lairRetryTick, "sukunaArcLairRetry", -1);
            Scribe_References.Look(ref lair, "sukunaArcLair");
        }
    }

    // ---------------------------------------------------------------------- possession

    /// <summary>Sukuna at the wheel for one minute. He goes for the nearest person and forces the
    /// finger on them; if the minute runs out first, Yuji takes his body back and nothing happened.</summary>
    public class MentalState_Possessed : MentalState
    {
        public const int Minute = 3600;
        public Pawn host;

        public override void PostStart(string reason)
        {
            base.PostStart(reason);
            host = SukunaArc.PickHost(pawn);
            if (pawn.style != null) pawn.style.FaceTattoo = SukunaQuestDefOf.Sukuna_Markings;
            SukunaBehaviour.Rename(pawn);      // her call 2026-11-06: while Sukuna has him, he goes by Sukuna's name
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public override void PostEnd()
        {
            base.PostEnd();
            if (pawn.style != null && pawn.style.FaceTattoo == SukunaQuestDefOf.Sukuna_Markings)
                pawn.style.FaceTattoo = Yuji.Is(pawn) && !ModuloYuji.Is(pawn) ? SukunaQuestDefOf.Sukuna_YujiMarks : TattooDefOf.NoTattoo_Face;
            SukunaBehaviour.RestoreName(pawn);
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public override void MentalStateTick(int delta)
        {
            base.MentalStateTick(delta);
            if (Age >= Minute) { RecoverFromState(); return; }
            if (!pawn.IsHashIntervalTick(30, delta)) return;
            if (host == null || host.Dead || !host.Spawned || host.Map != pawn.Map) host = SukunaArc.PickHost(pawn);
            if (host == null) return;
            if (pawn.CurJobDef == SukunaArcDefOf.Sukuna_ForceFinger && pawn.CurJob.targetA.Thing == host) return;
            pawn.jobs.StartJob(JobMaker.MakeJob(SukunaArcDefOf.Sukuna_ForceFinger, host), JobCondition.InterruptForced);
        }

        public override RandomSocialMode SocialModeMax() => RandomSocialMode.Off;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref host, "sukunaPossessedHost");
        }
    }

    public class JobDriver_ForceFinger : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            var hold = Toils_General.Wait(90, TargetIndex.A);
            hold.WithProgressBarToilDelay(TargetIndex.A);
            hold.FailOn(() => !pawn.Position.AdjacentTo8WayOrInside(job.targetA.Cell));
            yield return hold;
            yield return Toils_General.Do(() => SukunaArc.Instance?.Transfer(pawn, (Pawn)job.targetA.Thing));
        }
    }

    /// <summary>Dev mode: start the takeover now (Yuji must have joined and be on the map).</summary>
    public class IncidentWorker_TakeoverYuji : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            var y = SukunaArc.Yuji_;
            return y != null && y.Spawned && SukunaArc.Instance?.stage == ArcStage.None;
        }

        protected override bool TryExecuteWorker(IncidentParms parms) =>
            SukunaArc.Instance?.TryTakeover(SukunaArc.Yuji_, true) == true;
    }

    // ---------------------------------------------------------------------- the lair

    public class SitePartWorker_SukunaLair : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            var s = SukunaArc.Instance?.sukuna;
            if (s == null || s.Dead || s.Spawned) return;
            if (Find.WorldPawns.Contains(s)) Find.WorldPawns.RemovePawn(s);
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 12, c => c.Standable(map) && !c.Fogged(map), out var cell)
                && !CellFinder.TryFindRandomCellNear(map.Center, map, 30, c => c.Standable(map), out cell))
                cell = map.Center;
            GenSpawn.Spawn(s, cell, map);
            SukunaFaction.Claim(s);
            VesselMaker.ArmForAI(s);
        }
    }

    // ---------------------------------------------------------------------- hooks

    /// <summary>Yuji's hits on the King, once he has Benevolent Shrine: fists and slashes cut 5% of
    /// the soul loose, a Black Flash 15%.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_SoulCut
    {
        public const float PerHit = 0.05f, PerBlackFlash = 0.15f;

        public static void Postfix(Pawn __instance, DamageInfo dinfo)
        {
            if (__instance == null || __instance.Dead || !SukunaBehaviour.IsSukuna(__instance)) return;
            if (!(dinfo.Instigator is Pawn y) || !Yuji.Is(y) || !SukunaArc.HasBenevolent(y)) return;
            if (dinfo.Def == SukunaArcDefOf.Sukuna_SoulSlash) return;   // the domain does its own
            SukunaArc.Sever(__instance, BlackFlashLog.Was(__instance) ? PerBlackFlash : PerHit);
        }
    }

    /// <summary>Remembers which pawn the last Black Flash landed on, this tick.</summary>
    public static class BlackFlashLog
    {
        private static int victimId = -1, tick = -1;
        public static void Mark(Pawn victim) { victimId = victim?.thingIDNumber ?? -1; tick = Find.TickManager.TicksGame; }
        public static bool Was(Pawn victim) => victim != null && victim.thingIDNumber == victimId && tick == Find.TickManager.TicksGame;
    }

    /// <summary>Finger and painting quests won while Sukuna is out there each give Yuji a psycast level.</summary>
    [HarmonyPatch(typeof(Quest), nameof(Quest.End))]
    public static class Patch_QuestLevelsYuji
    {
        public static void Postfix(Quest __instance, QuestEndOutcome outcome)
        {
            if (outcome == QuestEndOutcome.Fail) return;
            var root = __instance?.root;
            if (root == null) return;
            if (root != SukunaQuestDefOf.Sukuna_Finger_Ruin && root != SukunaQuestDefOf.Sukuna_Finger_Held && root != SukunaQuestDefOf.Sukuna_Finger_Convoy
                && root != SukunaArcDefOf.Sukuna_Painting_Ruin && root != SukunaArcDefOf.Sukuna_Painting_Held && root != SukunaArcDefOf.Sukuna_Painting_Convoy) return;
            var arc = SukunaArc.Instance;
            if (arc == null || (arc.stage != ArcStage.Hunting && arc.stage != ArcStage.Lair)) return;
            var yuji = SukunaArc.Yuji_;
            if (yuji == null || yuji.Dead) return;
            var psy = PsycastSetup.Ensure(yuji);
            if (psy == null) return;
            psy.ChangeLevel(1);
            Messages.Message("Yuji grows stronger: psycast level " + psy.level + ".", yuji, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
