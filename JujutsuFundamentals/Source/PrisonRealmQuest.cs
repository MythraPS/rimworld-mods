using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Sound;

namespace JujutsuFundamentals
{
    /// <summary>
    /// Gojo sealed in the Prison Realm (her design 2026-11-11, reworked the same evening). Numbers are proposals.
    ///  1. Capture: a chance that grows through the year and is certain on Halloween's day in the RimWorld calendar
    ///     (5th of Decembary: Oct 31 is 83% of the way through the real year, day 50 of 60). At home, colony or not
    ///     alone, a powerful raid comes (x1.75 threat points) with one raider carrying the Prison Realm, whose one job
    ///     is to get close to Gojo and seal him (Verb range, 3 s wind-up while he stays in range and sight). Kill or
    ///     down the carrier first and the attempt fails: the captors pull the realm back and try again next Halloween.
    ///     Away from home (a caravan, another map) he's simply sealed. Once he's been sealed it never happens again.
    ///     If the player holds the one Prison Realm, nobody can use it on him. CaptureRaiders lets Kenjaku join later.
    ///  2. Hunt the captors: those holding the realm split up: 3 sites on the world map, each with one strong group
    ///     (Uraume with one of them if he's alive and free; PrisonRealmCaptors for the disaster curses and Kenjaku
    ///     later). Beat all three within 10 days and the realm is taken back, brought home, and Gojo walks out.
    ///     Entirely optional: ignoring them, nothing attacks the colony. One message at 2 days left, no more.
    ///  3. Time runs out: the captors get away, and one letter opens a site where the Angel (Hana Kurusu) is. Reach her
    ///     and she asks to join, once (her call: no spam). Whoever in the colony first knows Jacob's Ladder (the Angel,
    ///     or a Yuta who learns it, even years later) gets the realm turned up beside them; only the Ladder opens it.
    ///     Refuse the Angel and Gojo stays sealed until a Yuta learns the Ladder.
    /// </summary>
    public class PrisonRealmQuest : GameComponent, IThingHolder
    {
        public static PrisonRealmQuest Instance;

        // Attempt is last so older saves' stage values keep their meaning
        public enum Stage { None, Sealed, AngelSearch, Done, Attempt }

        public const int HalloweenDay = 49;                          // 0-based day of the 60-day year: 5th of Decembary
        public const float RaidFactor = 1.75f, DeadlineDays = 10f, WindUpTicks = 180f;
        public const int CaptorSites = 3, CheckEvery = 2500;
        public const float SiteGroupPoints = 1.0f;                   // x the storyteller's raid points, each site
        public const int UraumeSite = 1;                             // the second site

        /// <summary>Later characters who help seal Gojo (Kenjaku). Called with (home map, the raid's faction); returns
        /// pawns not yet spawned, who arrive with the raid.</summary>
        public static readonly List<Func<Map, Faction, IEnumerable<Pawn>>> CaptureRaiders =
            new List<Func<Map, Faction, IEnumerable<Pawn>>>();

        /// <summary>Later characters who guard the sealed realm (the disaster curses, Kenjaku). Called for each captor
        /// site with (site map, its faction, site index); returns pawns not yet spawned, placed with that group.</summary>
        public static readonly List<Func<Map, Faction, int, IEnumerable<Pawn>>> PrisonRealmCaptors =
            new List<Func<Map, Faction, int, IEnumerable<Pawn>>>();

        public Stage stage;
        public Pawn gojo, angel;
        public int sealedTick = -1, failedYear = -1, windStart = -1, waitSince = -1, skipReady;
        public bool warned, delivered, angelAsked, angelGuarded;
        public Pawn carrier;
        public List<Pawn> raidPawns = new List<Pawn>();
        public WorldObject site;                                     // the Angel's site
        public List<WorldObject> captorSites = new List<WorldObject>();
        public List<int> captorCleared = new List<int>();             // tile ids of beaten sites
        private ThingOwner<Thing> held;

        public PrisonRealmQuest(Game game)
        {
            Instance = this;
            held = new ThingOwner<Thing>(this, false, LookMode.Deep);
            SitePartWorker_CaptorSite.Reset();   // groups from another game (or the save before a reload) must not come back
        }

        public IThingHolder ParentHolder => null;
        public void GetChildHolders(List<IThingHolder> outChildren) => ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, held);
        public ThingOwner GetDirectlyHeldThings() => held;

        /// <summary>The Prison Realm holding Gojo, while the captors have it.</summary>
        public Apparel_PrisonRealm HeldRealm => held.Count > 0 ? held[0] as Apparel_PrisonRealm : null;

        public static bool GojoLocked(Apparel_PrisonRealm r) =>
            r != null && Instance != null && Instance.stage == Stage.AngelSearch && r.Prisoner != null && r.Prisoner == Instance.gojo;

        private int Now => Find.TickManager.TicksGame;

        public override void GameComponentTick()
        {
            if (stage == Stage.Attempt) { if (Now % 30 == 7) TickAttempt(); return; }
            if (Now % 250 != 77) return;
            switch (stage)
            {
                case Stage.None:
                    if (Now % CheckEvery == 2077 % CheckEvery) TryCapture(false);
                    break;
                case Stage.Sealed: TickHunt(); break;
                case Stage.AngelSearch: TickAngel(); break;
            }
        }

        // ------------------------------------------------------------------ 1. capture

        public static Pawn PlayerGojo() =>
            PawnsFinder.AllMapsWorldAndTemporary_Alive.FirstOrDefault(p => GojoPsycasts.Satoru.IsGojo(p) && p.Faction == Faction.OfPlayer);

        /// <summary>Is the realm in the player's hands (worn, carried, in a caravan, on a home map, or planted by them)?</summary>
        private static bool PlayerHas(Apparel_PrisonRealm r)
        {
            if (r.MapHeld != null && r.MapHeld.IsPlayerHome) return true;
            for (IThingHolder h = r.ParentHolder; h != null; h = h.ParentHolder)
            {
                if (h is Pawn p && p.Faction == Faction.OfPlayer) return true;
                if (h is Caravan c && c.Faction == Faction.OfPlayer) return true;
                if (h is Thing t && t.Faction == Faction.OfPlayer) return true;
                if (h is Pawn_InventoryTracker inv && inv.pawn.Faction == Faction.OfPlayer) return true;
                if (h is Pawn_ApparelTracker app && app.pawn.Faction == Faction.OfPlayer) return true;
            }
            return false;
        }

        private static float Longitude()
        {
            var home = Find.AnyPlayerHomeMap;
            return home != null ? Find.WorldGrid.LongLatOf(home.Tile).x : 0f;
        }

        /// <summary>Days until Halloween's day this year (0 on the day; after it, the days until next year's).</summary>
        public static int DaysToHalloween()
        {
            int day = GenDate.DayOfYear(Find.TickManager.TicksAbs, Longitude());
            int d = HalloweenDay - day;
            return d < 0 ? d + GenDate.DaysPerYear : d;
        }

        /// <summary>The chance of it happening on a given day (proposal): 0.3% most of the year, rising from 5% to 40%
        /// over the last quadrum before Halloween, certain on the day itself.</summary>
        public static float DayChance(int daysTo)
        {
            if (daysTo <= 0) return 1f;
            if (daysTo <= GenDate.DaysPerQuadrum) return Mathf.Lerp(0.40f, 0.05f, (daysTo - 1) / (float)(GenDate.DaysPerQuadrum - 1));
            return 0.003f;
        }

        public bool TryCapture(bool force)
        {
            if (stage != Stage.None) return false;
            var g = PlayerGojo();
            if (g == null || g.Dead) return false;
            int year = GenDate.Year(Find.TickManager.TicksAbs, Longitude());
            if (!force)
            {
                if (failedYear == year) return false;                     // they failed this year: next Halloween
                int days = DaysToHalloween();
                float perCheck = days <= 0 ? 1f : 1f - Mathf.Pow(1f - DayChance(days), CheckEvery / (float)GenDate.TicksPerDay);
                if (!Rand.Chance(perCheck)) return false;
            }
            // only on a map or in a caravan: kidnapped, in a pod in flight, in a casket or away on a quest, something
            // else holds him (sealing him there would leave him in two places)
            if (!g.Spawned && g.GetCaravan() == null) return false;
            var realm = Apparel_PrisonRealm.Instance;
            if (realm != null && (realm.Prisoner != null || PlayerHas(realm))) return false;   // it's not theirs to use
            if (realm != null)
            {
                if (realm.Spawned) realm.DeSpawn();
                realm.holdingOwner?.Remove(realm);
            }
            else realm = ThingMaker.MakeThing(JJKToolDefOf.JJK_PrisonRealm) as Apparel_PrisonRealm;
            if (realm == null) return false;

            if (g.Spawned && g.Map.IsPlayerHome) return StartRaid(g, realm);
            // away from home: sealed where he is
            held.TryAdd(realm);
            var caravan = g.GetCaravan();
            if (caravan != null)
            {
                caravan.RemovePawn(g);
                if (!caravan.PawnsListForReading.Any()) caravan.Destroy();
            }
            if (!realm.Seal(g)) { held.Remove(realm); return false; }
            Sealed(g, $"While {g.LabelShort} was away from the colony, someone opened the Prison Realm on him.");
            return true;
        }

        /// <summary>A powerful raid with one raider who carries the Prison Realm, sent for Gojo.</summary>
        private bool StartRaid(Pawn g, Apparel_PrisonRealm realm)
        {
            var map = g.Map;
            var faction = EnemyFaction();
            if (faction == null) { HoldOrDestroy(realm); return false; }
            var before = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
            var raid = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            raid.faction = faction;
            raid.points = Mathf.Max(raid.points * RaidFactor, 500f);
            raid.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            raid.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            raid.forced = true;
            if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(raid)) { HoldOrDestroy(realm); return false; }
            raidPawns = map.mapPawns.AllPawnsSpawned.Where(p => !before.Contains(p) && p.Faction == faction).ToList();
            foreach (var f in CaptureRaiders)
            {
                try
                {
                    foreach (var p in f(map, faction) ?? Enumerable.Empty<Pawn>())
                        if (p != null && !p.Spawned && !p.Dead) { Place(p, faction, map, raidPawns.FirstOrDefault()?.Position); raidPawns.Add(p); }
                }
                catch (Exception e) { Log.Warning("[JJK] CaptureRaiders: " + e.Message); }
            }
            carrier = raidPawns.Where(p => p.RaceProps.Humanlike && p.apparel != null && !p.Downed).RandomElementWithFallback();
            // the raid already came: don't send another every hour of Halloween, wait for next year
            if (carrier == null) { HoldOrDestroy(realm); failedYear = GenDate.Year(Find.TickManager.TicksAbs, Longitude()); return false; }
            carrier.apparel.Wear(realm, false, true);
            // the Malice Cloak (her design 2026-11-11): thrown where Gojo will see it, so the Six Eyes look away from the carrier
            if (MaliceCloakDefOf.JJK_MaliceCloak != null && MaliceCloak.Worn(carrier) == null)
                carrier.apparel.Wear((Apparel)ThingMaker.MakeThing(MaliceCloakDefOf.JJK_MaliceCloak), true);
            Carrier.Prepare(carrier);
            gojo = g;
            stage = Stage.Attempt;
            windStart = -1;
            Find.LetterStack.ReceiveLetter("They've come for Gojo",
                "A strong raid has come, and somewhere among them, unseen, is one who carries the Prison Realm. They aren't here "
                + $"to win the fight: they're here to seal {g.LabelShort}, and the rest are here to keep everyone busy while the "
                + "carrier sneaks close, skips to his side and opens it. It takes a few seconds to open.\n\n"
                + "Down or kill the carrier before it finishes and they'll have failed, for this year.",
                LetterDefOf.ThreatBig, new LookTargets(new[] { (Thing)g }));
            return true;
        }

        private void HoldOrDestroy(Apparel_PrisonRealm realm)
        {
            if (realm != null && !realm.Destroyed && realm.holdingOwner == null && !realm.Spawned) realm.Destroy();
        }

        /// <summary>The carrier (her call 2026-11-11: "they will try to sneak up on Gojo and capture him with that as
        /// their priority"): out of the raid's lord, unseen, closing in; within skip range they skip to his side and open
        /// the realm (3 s, while he stays in range and sight). They only fight when cornered in melee. If Gojo isn't on
        /// the map they wait, hidden; after 2 days of that they give up (the attempt fails).</summary>
        private void TickAttempt()
        {
            var realm = carrier?.apparel?.WornApparel.OfType<Apparel_PrisonRealm>().FirstOrDefault();
            if (gojo == null || gojo.Dead || carrier == null || carrier.Dead || carrier.Downed || !carrier.Spawned || realm == null
                || carrier.Faction == Faction.OfPlayer || carrier.IsPrisoner)
            {
                AttemptFailed(realm);
                return;
            }
            carrier.GetLord()?.RemovePawn(carrier);          // nothing else gives them orders
            // cornered: someone is on them in melee; let them fight back (and be seen)
            if (carrier.mindState?.meleeThreat != null && carrier.mindState.MeleeThreatStillThreat) { Carrier.Reveal(carrier); return; }

            if (!gojo.Spawned || gojo.Map != carrier.Map || !carrier.CanReach(gojo, Verse.AI.PathEndMode.Touch, Danger.Deadly))
            {
                windStart = -1;
                Carrier.Hide(carrier);
                if (waitSince < 0) waitSince = Now;
                if (Now - waitSince > 2 * GenDate.TicksPerDay) { AttemptFailed(realm, gaveUp: true); return; }
                if (carrier.CurJobDef != JobDefOf.Wait) carrier.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 600), Verse.AI.JobCondition.InterruptForced);
                return;
            }
            waitSince = -1;
            float range = realm.def.Verbs?.FirstOrDefault()?.range ?? 15.9f;
            bool inRange = carrier.Position.InHorDistOf(gojo.Position, range) && GenSight.LineOfSight(carrier.Position, gojo.Position, carrier.Map);
            if (windStart >= 0)
            {
                if (!inRange) { windStart = -1; Carrier.Hide(carrier); skipReady = Now + Carrier.SkipCooldown; return; }
                if (Now - windStart < WindUpTicks) return;
                realm.holdingOwner?.Remove(realm);
                held.TryAdd(realm);
                if (!realm.Seal(gojo)) { AttemptFailed(null); return; }
                // the carrier gets away with it, unseen, calling the cloak back; the rest keep fighting
                MaliceCloak.Recall(carrier);
                Carrier.Hide(carrier);
                var leave = JobMaker.MakeJob(JobDefOf.Goto, CellFinder.RandomEdgeCell(carrier.Map));
                leave.exitMapOnArrival = true;
                leave.locomotionUrgency = Verse.AI.LocomotionUrgency.Sprint;
                carrier.jobs.StartJob(leave, Verse.AI.JobCondition.InterruptForced);
                Sealed(gojo, "The carrier came out of nowhere at his side. The Prison Realm opened, and Satoru Gojo is sealed inside it.");
                return;
            }
            // close enough: first the Malice Cloak, thrown where he'll see it (his Six Eyes can't look away), then the skip
            if (Now >= skipReady && carrier.Position.InHorDistOf(gojo.Position, Carrier.SkipRange) && ThrowCloak(carrier, gojo))
            {
                skipReady = Now + CloakLead;
                return;
            }
            // close enough: skip to his side, show themselves, and open it
            // (already at his side, e.g. in a cramped room with no free cell next to him: no skip needed)
            if (Now >= skipReady && carrier.Position.InHorDistOf(gojo.Position, Carrier.SkipRange)
                && (Carrier.Beside(carrier, gojo) || Carrier.SkipNextTo(carrier, gojo)))
            {
                Carrier.Reveal(carrier);
                windStart = Now;
                carrier.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait_Combat, (int)WindUpTicks + 30), Verse.AI.JobCondition.InterruptForced);
                FleckMaker.Static(carrier.Position, carrier.Map, FleckDefOf.PsycastAreaEffect, 1.5f);
                MoteMaker.ThrowText(carrier.DrawPos, carrier.Map, "The Prison Realm opens", new Color(0.8f, 0.5f, 1f), 2.5f);
                return;
            }
            // otherwise: unseen, walking in
            Carrier.Hide(carrier);
            var cur = carrier.CurJob;
            if (cur == null || cur.def != JobDefOf.Goto || cur.targetA.Cell.DistanceTo(gojo.Position) > 3f)
            {
                var job = JobMaker.MakeJob(JobDefOf.Goto, gojo.Position);
                job.locomotionUrgency = Verse.AI.LocomotionUrgency.Jog;
                job.expiryInterval = 120;
                carrier.jobs.StartJob(job, Verse.AI.JobCondition.InterruptForced);
            }
        }

        /// <summary>Ticks between throwing the cloak and skipping in (proposal: 2 s, for Gojo to turn to it).</summary>
        public const int CloakLead = 120;

        /// <summary>The carrier throws the Malice Cloak (if still worn) to a spot 6–9 cells from Gojo, on his far side
        /// from the carrier, that he can see and the carrier can reach with a throw. False if there's no cloak or spot.</summary>
        private static bool ThrowCloak(Pawn carrier, Pawn gojo)
        {
            var cloak = MaliceCloak.Worn(carrier);
            if (cloak == null) return false;
            var map = carrier.Map;
            var away = (gojo.Position - carrier.Position).ToVector3();
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = new Vector3(1f, 0f, 0f);
            away.Normalize();
            for (int i = 0; i < 12; i++)
            {
                float d = Rand.Range(6f, 9f), turn = Rand.Range(-70f, 70f);
                var dir = Quaternion.Euler(0f, turn, 0f) * away;
                var c = (gojo.Position.ToVector3Shifted() + dir * d).ToIntVec3();
                if (!c.InBounds(map) || !c.Standable(map)) continue;
                if (!c.InHorDistOf(carrier.Position, Apparel_MaliceCloak.ThrowRange)) continue;
                if (!GenSight.LineOfSight(gojo.Position, c, map, true)) continue;
                return MaliceCloak.Throw(carrier, cloak, c);
            }
            return false;
        }

        private void AttemptFailed(Apparel_PrisonRealm realm, bool gaveUp = false)
        {
            if (carrier != null && !carrier.Destroyed) Carrier.Reveal(carrier);
            stage = Stage.None;
            failedYear = GenDate.Year(Find.TickManager.TicksAbs, Longitude());
            // the captors pull the realm back: it isn't left as loot (her call left open; one realm, theirs)
            if (realm != null && !realm.Destroyed)
            {
                if (realm.Spawned) realm.DeSpawn();
                realm.holdingOwner?.Remove(realm);
                realm.Destroy();
            }
            carrier = null;
            raidPawns.Clear();
            windStart = -1;
            Find.LetterStack.ReceiveLetter("Gojo wasn't sealed",
                (gaveUp ? "The one carrying the Prison Realm waited, hidden, but Gojo never came back to them, and they gave up. "
                        : "The one carrying the Prison Realm was stopped before it could open on him. ")
                + "Whoever sent them pulled it back. They'll try again another year.", LetterDefOf.PositiveEvent, gojo);
        }

        private void Sealed(Pawn g, string how)
        {
            gojo = g;
            stage = Stage.Sealed;
            sealedTick = Now;
            carrier = null;
            raidPawns.Clear();
            OpenCaptorSites();
            Find.LetterStack.ReceiveLetter("Gojo has been sealed",
                how + " He's out of reach inside it.\n\nThose holding it have split up: " + CaptorSites + " groups, marked on the "
                + $"world map. Beat all of them within {DeadlineDays:0} days and the Prison Realm is yours again, and he walks out. "
                + "Nobody will come after the colony either way: if you leave them be, they get away with it, and only one "
                + "person can open it then.",
                LetterDefOf.ThreatBig, new LookTargets(captorSites.Select(x => new GlobalTargetInfo(x))));
        }

        // ------------------------------------------------------------------ 2. hunt the captors

        private static Faction EnemyFaction() =>
            Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer)
                            && f.def.pawnGroupMakers != null && f != SukunaPsycasts.SukunaFaction.Existing())
                .RandomElementWithFallback();

        public static SitePartDef CaptorDef => DefDatabase<SitePartDef>.GetNamedSilentFail("JJK_CaptorSite");

        private void OpenCaptorSites()
        {
            captorSites.Clear();
            captorCleared.Clear();
            var home = Find.AnyPlayerHomeMap;
            if (home == null || CaptorDef == null) return;
            for (int i = 0; i < CaptorSites; i++)
            {
                var faction = EnemyFaction();
                if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, home.Tile, 3, 12)) continue;
                var s = SiteMaker.MakeSite(CaptorDef, tile, faction, true, StorytellerUtility.DefaultThreatPointsNow(home) * SiteGroupPoints);
                if (s == null) continue;
                Find.WorldObjects.Add(s);
                captorSites.Add(s);
            }
        }

        public int CaptorIndex(WorldObject s) => captorSites.IndexOf(s);

        /// <summary>A captor site's group is beaten: nobody of theirs left standing, all of them hostile no more.</summary>
        public void Notify_SiteBeaten(WorldObject s)
        {
            if (s == null || captorCleared.Contains(s.Tile)) return;
            captorCleared.Add(s.Tile);
            int left = captorSites.Count(x => x != null && !captorCleared.Contains(x.Tile));
            Messages.Message(left > 0 ? $"A group holding Gojo is beaten ({CaptorSites - left} of {CaptorSites})."
                                      : "Every group holding Gojo is beaten.", MessageTypeDefOf.PositiveEvent, false);
        }

        /// <summary>The game removes a site when the player's people leave it. A group not yet beaten is still out
        /// there, so its site is put back on the same tile (the same group waits there).</summary>
        private void KeepSites()
        {
            if (CaptorDef == null) return;
            var home = Find.AnyPlayerHomeMap;
            for (int i = 0; i < captorSites.Count; i++)
            {
                var old = captorSites[i];
                if (old == null || !old.Destroyed || captorCleared.Contains(old.Tile)) continue;
                if (Find.WorldObjects.AnyWorldObjectAt(old.Tile)) continue;
                var faction = old.Faction != null && !old.Faction.defeated ? old.Faction : EnemyFaction();
                var s = SiteMaker.MakeSite(CaptorDef, old.Tile, faction, true,
                    home != null ? StorytellerUtility.DefaultThreatPointsNow(home) * SiteGroupPoints : (float?)null);
                if (s == null) continue;
                Find.WorldObjects.Add(s);
                captorSites[i] = s;
            }
        }

        private void TickHunt()
        {
            KeepSites();
            // beaten sites go once nobody of yours is there
            foreach (var s in captorSites.ToList())
                if (s != null && captorCleared.Contains(s.Tile) && !(s is MapParent mp && mp.HasMap) && !s.Destroyed)
                    s.Destroy();
            // every group beaten (sites beaten and gone may have dropped out of the list at a save)
            if (captorCleared.Count > 0 && captorSites.All(x => x != null && captorCleared.Contains(x.Tile))) { Freed(); return; }
            if (Now >= sealedTick + DeadlineDays * GenDate.TicksPerDay) { TimeUp(); return; }
            if (!warned && Now >= sealedTick + (DeadlineDays - 2f) * GenDate.TicksPerDay)
            {
                warned = true;
                int left = captorSites.Count(x => x != null && !captorCleared.Contains(x.Tile));
                Messages.Message($"Two days left to beat the groups holding Gojo ({left} to go).", MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void Freed()
        {
            var realm = HeldRealm;
            var map = Find.AnyPlayerHomeMap ?? Find.Maps.FirstOrDefault(m => m.mapPawns.FreeColonistsSpawnedCount > 0);
            if (realm != null && map == null) return;   // nowhere of yours to bring it yet: tried again next check
            stage = Stage.Done;
            foreach (var s in captorSites) if (s != null && !s.Destroyed && !(s is MapParent mp && mp.HasMap)) s.Destroy();
            if (realm == null || map == null) return;
            held.Remove(realm);
            GenSpawn.Spawn(realm, DropCellFinder.TradeDropSpot(map), map);
            realm.Release();
            if (gojo != null && gojo.Faction != Faction.OfPlayer) gojo.SetFaction(Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("Gojo is free",
                "Every group holding him is beaten, and the Prison Realm is yours again. It opens, and Satoru Gojo steps out as if nothing happened.",
                LetterDefOf.PositiveEvent, gojo);
        }

        public static void Place(Pawn p, Faction faction, Map map, IntVec3? near)
        {
            if (p.Faction != faction) p.SetFaction(faction);
            if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
            var at = near ?? CellFinder.RandomEdgeCell(map);
            if (!CellFinder.TryFindRandomCellNear(at, map, 6, c => c.Standable(map), out var cell)) cell = at;
            GenSpawn.Spawn(p, cell, map);
            var lord = map.lordManager.lords.FirstOrDefault(l => l.faction == faction);
            if (lord != null) lord.AddPawn(p);
            else LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, new[] { p });
        }

        public static bool UraumeIs(Pawn p) => SukunaPsycasts.UraumeTracker.Is(p);

        /// <summary>Uraume, if he's alive, free and not the player's.</summary>
        public static Pawn UraumeFor()
        {
            var t = SukunaPsycasts.UraumeTracker.Instance;
            if (t == null || t.Dead) return null;
            var u = t.Get();
            if (u == null || u.Dead || u.Spawned || u.Faction == Faction.OfPlayer || u.IsPrisoner || u.GetCaravan() != null) return null;
            return u;
        }

        // ------------------------------------------------------------------ 3. the Angel

        private void TimeUp()
        {
            stage = Stage.AngelSearch;
            // the captors get away
            foreach (var s in captorSites) if (s != null && !s.Destroyed && !(s is MapParent mp && mp.HasMap)) s.Destroy();
            OpenAngelSite();
        }

        public void OpenAngelSite()
        {
            var home = Find.AnyPlayerHomeMap;
            if (home == null || AngelChar.Def == null) return;
            if (!TileFinder.TryFindNewSiteTile(out PlanetTile tile, home.Tile, 4, 12)) return;
            var s = SiteMaker.MakeSite(AngelChar.Def, tile, null, true, null);
            if (s == null) return;
            Find.WorldObjects.Add(s);
            site = s;
            Find.LetterStack.ReceiveLetter("Find the Angel",
                "Time ran out: Gojo's captors got away, and the Prison Realm with him in it can't be opened by force. "
                + "There is one person who can open it: Hana Kurusu, a girl who carries the Angel, a sorcerer from a thousand "
                + "years ago whose technique, Jacob's Ladder, undoes any cursed technique. She is somewhere nearby, marked on the "
                + "world map, and the captors have left people there.\n\nGoing is up to you. If she (or anyone with Jacob's Ladder) "
                + "is ever with you, the Prison Realm will turn up, and Jacob's Ladder cast on it lets Gojo out.",
                LetterDefOf.NeutralEvent, s);
        }

        /// <summary>Her site stays until she is with you (her call 2026-11-12): the game removes a site once your people
        /// leave it, so it is put back on the same tile while she is alive and not yet yours.</summary>
        private void KeepAngelSite()
        {
            if (site == null || !site.Destroyed || AngelChar.Def == null) return;
            if (angel != null && (angel.Dead || angel.Faction == Faction.OfPlayer)) return;
            if (Find.WorldObjects.AnyWorldObjectAt(site.Tile))
            {
                var home = Find.AnyPlayerHomeMap;
                if (home == null || !TileFinder.TryFindNewSiteTile(out PlanetTile t, home.Tile, 4, 12)) return;
                var moved = SiteMaker.MakeSite(AngelChar.Def, t, null, true, null);
                if (moved == null) return;
                Find.WorldObjects.Add(moved);
                site = moved;
                return;
            }
            var s = SiteMaker.MakeSite(AngelChar.Def, site.Tile, null, true, null);
            if (s == null) return;
            Find.WorldObjects.Add(s);
            site = s;
        }

        private void TickAngel()
        {
            KeepAngelSite();
            // she asks once, the first time your people are on her map with her (her call: no spam)
            if (!angelAsked && angel != null && !angel.Dead && angel.Spawned && angel.Faction != Faction.OfPlayer
                && angel.Map.mapPawns.FreeColonistsSpawned.Any())
            {
                angelAsked = true;
                JoinOffer.Send(JoinOffer.Angel, angel.Map, "The Angel asks to join",
                    "Hana Kurusu has heard why you came. She will open the Prison Realm for you, and she would like to stay.",
                    DefDatabase<LetterDef>.GetNamed("JJK_JoinOffer"), angel);
            }
            // someone who can open it is in the colony: the abandoned Prison Realm turns up beside them
            if (!delivered && HeldRealm != null)
            {
                var opener = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists
                    .FirstOrDefault(p => p.Spawned && JacobsLadder.Knows(p));
                if (opener != null)
                {
                    var realm = HeldRealm;
                    held.Remove(realm);
                    if (!CellFinder.TryFindRandomCellNear(opener.Position, opener.Map, 3, x => x.Standable(opener.Map), out var spot)) spot = opener.Position;
                    GenSpawn.Spawn(realm, spot, opener.Map);
                    delivered = true;
                    Find.LetterStack.ReceiveLetter("The Prison Realm",
                        $"The captors left the Prison Realm behind, and it has found its way to {opener.LabelShort}. Gojo is sealed inside. "
                        + "It won't open by hand: cast Jacob's Ladder on it.", LetterDefOf.PositiveEvent, realm);
                }
            }
            if (gojo != null && !gojo.Dead && gojo.Spawned) { stage = Stage.Done; }
        }

        /// <summary>Jacob's Ladder opened the Prison Realm holding Gojo.</summary>
        public void Notify_Opened(Apparel_PrisonRealm r)
        {
            if (stage == Stage.AngelSearch && gojo != null && !gojo.Dead)
            {
                stage = Stage.Done;
                if (gojo.Faction != Faction.OfPlayer) gojo.SetFaction(Faction.OfPlayer);
                Find.LetterStack.ReceiveLetter("Gojo is free",
                    "Jacob's Ladder falls on the Prison Realm and it comes apart. Satoru Gojo is free.", LetterDefOf.PositiveEvent, gojo);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Values.Look(ref stage, "jjkRealmStage");
            Scribe_References.Look(ref gojo, "jjkRealmGojo", true);
            Scribe_References.Look(ref angel, "jjkAngel", true);
            Scribe_References.Look(ref carrier, "jjkRealmCarrier");
            Scribe_Values.Look(ref sealedTick, "jjkRealmSealed", -1);
            Scribe_Values.Look(ref failedYear, "jjkRealmFailedYear", -1);
            Scribe_Values.Look(ref windStart, "jjkRealmWind", -1);
            Scribe_Values.Look(ref waitSince, "jjkRealmWait", -1);
            Scribe_Values.Look(ref skipReady, "jjkRealmSkip");
            Scribe_Values.Look(ref warned, "jjkRealmWarned");
            Scribe_Values.Look(ref delivered, "jjkRealmDelivered");
            Scribe_Values.Look(ref angelAsked, "jjkAngelAsked");
            Scribe_Values.Look(ref angelGuarded, "jjkAngelGuarded");
            Scribe_References.Look(ref site, "jjkAngelSite");
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                raidPawns.RemoveAll(p => p == null || p.Destroyed);
                if (stage == Stage.Sealed) KeepSites();   // an unbeaten site must not drop out of the list
                captorSites.RemoveAll(s => s == null || s.Destroyed);
            }
            Scribe_Collections.Look(ref raidPawns, "jjkRealmRaid", LookMode.Reference);
            Scribe_Collections.Look(ref captorSites, "jjkRealmCaptorSites", LookMode.Reference);
            Scribe_Collections.Look(ref captorCleared, "jjkRealmCaptorCleared", LookMode.Value);
            if (raidPawns == null) raidPawns = new List<Pawn>();
            if (captorSites == null) captorSites = new List<WorldObject>();
            captorSites.RemoveAll(s => s == null);
            if (captorCleared == null) captorCleared = new List<int>();
            Scribe_Deep.Look(ref held, "jjkRealmHeld", this);
            if (held == null) held = new ThingOwner<Thing>(this, false, LookMode.Deep);
        }
    }

    /// <summary>
    /// The Prison Realm carrier's techniques (her call 2026-11-11: "maybe they can have psycasts suited to doing just
    /// that, like from the Nightstalker tree or the teleportation one"). They learn VPE's Obscure and Shadowskip
    /// (Nightstalker) and Skip (Skipmaster), so the player sees them on the carrier, and this code does what those do
    /// on the carrier's behalf (VEF's AI never casts them): unseen while closing in (vanilla psychic invisibility, which
    /// holds in daylight too, where Obscure's own effect only works in darkness), then a skip to Gojo's side with the
    /// game's skip flash and sound. No psyfocus is spent: it's AI. Numbers are proposals.
    /// </summary>
    public static class Carrier
    {
        public const float SkipRange = 24f;          // VPE's Skip reaches 28
        public const int SkipCooldown = 600;
        public const int PsycastLevel = 6;

        public static void Prepare(Pawn c)
        {
            if (c == null) return;
            Characters.Psycaster(c, PsycastLevel);
            Learn(c, "VPE_Nightstalker", "VPE_Obscure", "VPE_Shadowskip");
            Learn(c, "VPE_Skipmaster", "VPE_Skip");
            c.GetLord()?.RemovePawn(c);
            Hide(c);
            if (c.Spawned) FleckMaker.Static(c.Position, c.Map, FleckDefOf.PsycastAreaEffect, 3f);   // Obscure, cast
        }

        private static void Learn(Pawn c, string path, params string[] abilities)
        {
            var p = DefDatabase<VanillaPsycastsExpanded.PsycasterPathDef>.GetNamedSilentFail(path);
            var defs = abilities.Select(a => DefDatabase<VEF.Abilities.AbilityDef>.GetNamedSilentFail(a)).Where(d => d != null).ToArray();
            if (p != null && defs.Length > 0) Characters.Learn(c, p, defs);
        }

        private static HediffDef invis;
        private static HediffDef Invis => invis ?? (invis = DefDatabase<HediffDef>.GetNamedSilentFail("PsychicInvisibility"));

        public static void Hide(Pawn c)
        {
            if (c?.health == null || Invis == null || c.health.hediffSet.HasHediff(Invis)) return;
            c.health.AddHediff(Invis);
        }

        public static void Reveal(Pawn c)
        {
            var h = Invis == null ? null : c?.health?.hediffSet?.GetFirstHediffOfDef(Invis);
            if (h != null) c.health.RemoveHediff(h);
        }

        /// <summary>Within 2 cells of Gojo and in sight of him.</summary>
        public static bool Beside(Pawn c, Pawn g) =>
            c.Map == g.Map && c.Position.InHorDistOf(g.Position, 2.9f) && GenSight.LineOfSight(c.Position, g.Position, c.Map);

        /// <summary>Skip (VPE's, done here): out of where they stand and into a cell beside Gojo.</summary>
        public static bool SkipNextTo(Pawn c, Pawn g)
        {
            var map = c.Map;
            if (!CellFinder.TryFindRandomCellNear(g.Position, map, 2, x => x.Standable(map) && x != g.Position && x.GetFirstPawn(map) == null
                                                   && GenSight.LineOfSight(x, g.Position, map), out var cell)) return false;
            FleckMaker.Static(c.Position, map, FleckDefOf.PsycastSkipFlashEntry, 1f);
            SoundDefOf.Psycast_Skip_Entry?.PlayOneShot(new TargetInfo(c.Position, map));
            c.Position = cell;
            c.Notify_Teleported(true, false);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipInnerExit, 1f);
            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
            SoundDefOf.Psycast_Skip_Exit?.PlayOneShot(new TargetInfo(cell, map));
            c.rotationTracker?.FaceTarget(g);
            return true;
        }
    }

    /// <summary>One group of Gojo's captors (her call 2026-11-11: hunt the captors). A strong hostile group holding the
    /// middle; Uraume with the second if he's alive and free; PrisonRealmCaptors for later characters. The same group
    /// comes back if the player leaves and returns. Beaten when none of them is left standing and hostile.</summary>
    public class SitePartWorker_CaptorSite : SitePartWorker
    {
        private static readonly Dictionary<int, List<Pawn>> groups = new Dictionary<int, List<Pawn>>();

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            var q = PrisonRealmQuest.Instance;
            var site = map.Parent as WorldObject;
            if (q == null || site == null) return;
            int idx = q.CaptorIndex(site);
            var faction = site.Faction ?? Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction && !f.Hidden);
            if (faction == null) return;
            var group = Group(site.Tile);
            group.RemoveAll(p => p == null || p.Dead || p.Destroyed);
            if (group.Count == 0)
            {
                var parms = new PawnGroupMakerParms
                {
                    groupKind = PawnGroupKindDefOf.Combat, tile = map.Tile, faction = faction,
                    points = Mathf.Max(300f, StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap ?? map) * PrisonRealmQuest.SiteGroupPoints),
                };
                group.AddRange(PawnGroupMakerUtility.GeneratePawns(parms));
                if (idx == PrisonRealmQuest.UraumeSite) { var u = PrisonRealmQuest.UraumeFor(); if (u != null) group.Add(u); }
                foreach (var f in PrisonRealmQuest.PrisonRealmCaptors)
                {
                    try { group.AddRange(f(map, faction, idx) ?? Enumerable.Empty<Pawn>()); }
                    catch (Exception e) { Log.Warning("[JJK] PrisonRealmCaptors: " + e.Message); }
                }
            }
            var spawned = new List<Pawn>();
            var kings = new List<Pawn>();
            foreach (var g in group)
            {
                if (g.Spawned) continue;
                // Uraume stays the King's, allied with this group (her call 2026-11-12)
                bool king = PrisonRealmQuest.UraumeIs(g);
                if (king)
                {
                    var kf = SukunaPsycasts.SukunaFaction.Get();
                    if (g.Faction != kf) g.SetFaction(kf);
                    SukunaPsycasts.SukunaFaction.AllyWith(kf, faction);
                }
                else if (g.Faction != faction) g.SetFaction(faction);
                if (Find.WorldPawns.Contains(g)) Find.WorldPawns.RemovePawn(g);
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 16, c => c.Standable(map), out var cell)) cell = map.Center;
                GenSpawn.Spawn(g, cell, map);
                (king ? kings : spawned).Add(g);
            }
            if (spawned.Count > 0) LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(map.Center, 12f, 16f), map, spawned);
            if (kings.Count > 0) LordMaker.MakeNewLord(kings[0].Faction, new LordJob_DefendPoint(map.Center, 12f, 16f), map, kings);
        }

        public override void SitePartWorkerTick(SitePart sitePart)
        {
            base.SitePartWorkerTick(sitePart);
            if (Find.TickManager.TicksGame % 250 != 31) return;
            CheckBeaten(sitePart.site);
        }

        // leaving right after the last of them falls still counts (the tick check may not have run yet)
        public override void Notify_SiteMapAboutToBeRemoved(SitePart sitePart)
        {
            base.Notify_SiteMapAboutToBeRemoved(sitePart);
            CheckBeaten(sitePart.site);
        }

        private static void CheckBeaten(Site site)
        {
            if (site == null || !site.HasMap) return;
            var group = Group(site.Tile);
            // after a load the list is empty: those of theirs still on the map are the group
            if (group.Count == 0)
                group.AddRange(site.Map.mapPawns.AllPawnsSpawned.Where(p => ((p.Faction != null && p.Faction == site.Faction) || PrisonRealmQuest.UraumeIs(p)) && p.HostileTo(Faction.OfPlayer)));
            if (group.All(p => p == null || p.Dead || p.Downed || p.IsPrisoner || !p.Spawned || p.Faction == Faction.OfPlayer || !p.HostileTo(Faction.OfPlayer)))
                PrisonRealmQuest.Instance?.Notify_SiteBeaten(site);
        }

        public static void Reset() => groups.Clear();

        public static List<Pawn> Group(int tile)
        {
            if (!groups.TryGetValue(tile, out var g)) groups[tile] = g = new List<Pawn>();
            return g;
        }
    }

    /// <summary>The Angel's site: Hana Kurusu near the middle, a rear guard of the captors around.</summary>
    public class SitePartWorker_AngelSite : SitePartWorker
    {
        public const float GuardPoints = 0.5f;   // x the storyteller's raid points (proposal)

        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            var q = PrisonRealmQuest.Instance;
            if (q == null) return;
            var a = AngelChar.Get();
            if (a == null || a.Dead || a.Spawned) return;
            if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemovePawn(a);
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 10, c => c.Standable(map), out var cell)) cell = map.Center;
            GenSpawn.Spawn(a, cell, map);
            // RemovePawn dropped her keep-forever mark; without it the world would throw her away once the player leaves
            Find.WorldPawns.ForcefullyKeptPawns.Add(a);
            // the rear guard, holding the middle; only the first time (proposal): going back finds just her
            if (q.angelGuarded) return;
            q.angelGuarded = true;
            var faction = Find.FactionManager.AllFactions
                .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer) && f.def.pawnGroupMakers != null)
                .RandomElementWithFallback();
            if (faction == null) return;
            var parms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat, tile = map.Tile, faction = faction,
                points = Mathf.Max(200f, StorytellerUtility.DefaultThreatPointsNow(Find.AnyPlayerHomeMap ?? map) * GuardPoints),
            };
            var guards = PawnGroupMakerUtility.GeneratePawns(parms).ToList();
            foreach (var g in guards)
            {
                if (!CellFinder.TryFindRandomCellNear(map.Center, map, 18, c => c.Standable(map) && !c.InHorDistOf(cell, 6), out var gc)) gc = map.Center;
                GenSpawn.Spawn(g, gc, map);
            }
            if (guards.Count > 0) LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(map.Center, 12f, 18f), map, guards);
        }
    }
}
