using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using VEFAbilityDef = VEF.Abilities.AbilityDef;
using Ability = VEF.Abilities.Ability;
using LudeonTK;

namespace JujutsuFundamentals
{
    /// <summary>
    /// The Angel (her design 2026-11-11): Hana Kurusu, a girl who carries the Angel, a sorcerer from a thousand years
    /// ago. Canon look: chin-length blonde bob with blunt bangs, green eyes, an oversized tan sweater, black leggings.
    /// About 15. Made once per game, when her site's map is made (PrisonRealmQuest) or from the dev menu.
    /// She has Cursed Energy Fundamentals and Jacob's Ladder, and can never have anything else: in Yuta's tree only
    /// Jacob's Ladder is open to her.
    /// </summary>
    public static class AngelChar
    {
        public const int PsycastLevel = 2;      // her call 2026-11-12: weaker, level 2 (was 12 with all of the Fundamentals)

        public static SitePartDef Def => DefDatabase<SitePartDef>.GetNamedSilentFail("JJK_AngelSite");

        public static bool Is(Pawn p) => p != null && PrisonRealmQuest.Instance?.angel == p;

        public const string FlightName = "JJK_AngelFlight", FlightKey = "angelflight";
        public static VEFAbilityDef FlightDef => DefDatabase<VEFAbilityDef>.GetNamedSilentFail(FlightName);

        /// <summary>Jacob's Ladder and her flight, nothing else (her calls 2026-11-11 "Angel can fly too", 2026-11-12 not the
        /// Cursed Energy Fundamentals).</summary>
        public static bool Allowed(VEFAbilityDef d) =>
            d != null && (JacobsLadder.Is(d) || d.defName == FlightName);

        /// <summary>The Angel, made the first time she's needed.</summary>
        public static Pawn Get()
        {
            var q = PrisonRealmQuest.Instance;
            if (q == null) return null;
            if (q.angel == null) q.angel = Make();
            else { WithoutFundamentals(q.angel); Wings(q.angel); }
            return q.angel;
        }

        /// <summary>Her wings and halo (her ask 2026-11-12); older Angels get them too.</summary>
        public static void Wings(Pawn p)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail("JJK_AngelWings");
            if (def == null || p?.health == null || p.health.hediffSet.HasHediff(def)) return;
            p.health.AddHediff(def);
        }

        private static Pawn Make()
        {
            var q = PrisonRealmQuest.Instance;
            var p = Characters.Make("Hana", "Angel", "Kurusu", Gender.Female, 15f);
            if (q != null) q.angel = p;   // before her abilities, or the learner check turns them away
            Characters.Skill(p, SkillDefOf.Social, 6, Passion.Minor);
            Characters.Skill(p, SkillDefOf.Melee, 5, Passion.None);
            Characters.Psycaster(p, PsycastLevel);
            var comp = p.GetComp<CompAbilities>();
            var jl = JacobsLadder.Def;
            if (comp != null && jl != null && !comp.HasAbility(jl)) comp.GiveAbility(jl);
            if (comp != null && FlightDef != null && !comp.HasAbility(FlightDef)) comp.GiveAbility(FlightDef);
            Looks.Angel(p);
            CharacterIdeos.Give(p, CharacterIdeos.Get("jujutsu", FactionDefOf.PlayerColony, "Collectivist"));
            PassiveSync.Sync(p);
            Looks.Refresh(p);
            Find.WorldPawns.ForcefullyKeptPawns.Add(p);
            return p;
        }

        /// <summary>An Angel made before 2026-11-12 had all of the Fundamentals: they go.</summary>
        private static void WithoutFundamentals(Pawn p)
        {
            var path = JJKDefOf.JJK_CursedEnergyFundamentals;
            var comp = p?.GetComp<CompAbilities>();
            if (path?.abilities == null || comp == null) return;
            if (comp.LearnedAbilities.RemoveAll(a => a?.def != null && path.abilities.Contains(a.def)) > 0)
            {
                var psy = p.health?.hediffSet?.hediffs?.OfType<VanillaPsycastsExpanded.Hediff_PsycastAbilities>().FirstOrDefault();
                psy?.unlockedPaths?.Remove(path);
                PassiveSync.Sync(p);
            }
        }

        public static void Join(Map map)
        {
            var a = Get();
            if (a == null || a.Dead) return;
            if (!a.Spawned && map != null)
            {
                if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemovePawn(a);
                var c = CellFinder.TryFindRandomEdgeCellWith(x => x.Standable(map) && map.reachability.CanReachColony(x), map, CellFinder.EdgeRoadChance_Friendly, out var e) ? e : map.Center;
                GenSpawn.Spawn(a, c, map);
            }
            if (a.Faction != Faction.OfPlayer) a.SetFaction(Faction.OfPlayer);
            Find.LetterStack.ReceiveLetter("The Angel joins", "Hana Kurusu has joined you. The Angel goes with her.", LetterDefOf.PositiveEvent, a);
        }
    }

    /// <summary>Her flight: its own toggle on Uro's flight (the JJK_SkyFlight hediff, which GojoFlight moves),
    /// at a quarter of the Sky Flight's drain, like Uro's. Nobody else has the ability, so the two never meet on one pawn.</summary>
    [StaticConstructorOnStartup]
    public static class AngelStartup
    {
        static AngelStartup()
        {
            Toggles.Add(new ToggleSpec
            {
                key = AngelChar.FlightKey, title = "Flight", drain = 0.0008f,
                drainFactor = p => 0.25f,   // low, like Uro's (her call 2026-11-11)
                hediff = () => SkyDefOf.JJK_SkyFlight, ability = () => AngelChar.FlightDef,
            });
        }
    }

    // ==================================================================== Jacob's Ladder

    /// <summary>
    /// Maximum Output: Jacob's Ladder (her design 2026-11-11; canon: a four-winged trumpet of light, a magic circle in
    /// the sky, and a pillar of light falling on the target). In the area: Anomaly entities take heavy damage; everyone
    /// else who isn't an ally is neutralized like Domain Amplification does in melee (psycast shields and buffs
    /// stripped, raised output and other toggles switched off), at range. A Prison Realm in the area opens.
    /// Numbers are proposals: 24.9 range, 4.9 radius, 60 damage to Anomalies x power, no damage to anyone else.
    /// </summary>
    public class Ability_JacobsLadder : Ability
    {
        public const float AnomalyDamage = 60f;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var t = targets.FirstOrDefault();
            var map = pawn?.Map;
            if (map == null || !t.IsValid) return;
            JacobsLadder.Fall(pawn, t.Cell, GetRadiusForPawn(), AnomalyDamage * Mathf.Max(0.5f, Jujutsu.Power(pawn)));
        }
    }

    public static class JacobsLadder
    {
        /// <summary>Who else the Ladder burns like an Anomaly. Not Sukuna (her call 2026-11-12: it only cuts his soul, so
        /// his host isn't killed); later characters can add themselves here.</summary>
        public static readonly System.Collections.Generic.List<System.Func<Pawn, bool>> AlsoBurns = new System.Collections.Generic.List<System.Func<Pawn, bool>>();
        public static bool Burns(Pawn p) => p != null && AlsoBurns.Any(f => f(p));

        public const string Name = "JJK_JacobsLadder";
        public static VEFAbilityDef Def => DefDatabase<VEFAbilityDef>.GetNamedSilentFail(Name);
        public static bool Is(VEFAbilityDef d) => d != null && d.defName == Name;
        public static bool Knows(Pawn p) => Def != null && Jujutsu.Knows(p, Def);
        /// <summary>Of Sukuna's soul cut loose by one Ladder (proposal; Yuji's hit is 5%, a Black Flash 15%).</summary>
        public const float SoulCut = 0.20f;
        private static FleckDef beam;

        public static void Fall(Pawn caster, IntVec3 center, float radius, float damage)
        {
            var map = caster.Map;
            radius = Mathf.Max(1.5f, radius);
            // the pillar of light, and a circle on the ground
            beam = beam ?? DefDatabase<FleckDef>.GetNamedSilentFail("JJK_Fleck_JacobsLadder");
            var at = center.ToVector3Shifted();
            if (beam != null)
            {
                var d = FleckMaker.GetDataStatic(at + new Vector3(0f, 0f, 3f), map, beam, 1f);
                d.exactScale = new Vector3(radius * 1.2f, 1f, 7f);
                map.flecks.CreateFleck(d);
            }
            var ring = FleckMaker.GetDataStatic(at, map, FleckDefOf.PsycastAreaEffect, radius * 2f);
            ring.instanceColor = new Color(1f, 0.92f, 0.6f);
            map.flecks.CreateFleck(ring);
            FleckMaker.ThrowLightningGlow(at, map, radius);

            var dmg = DefDatabase<DamageDef>.GetNamedSilentFail("JJK_CursedEnergy") ?? DamageDefOf.Burn;
            foreach (var v in GenRadial.RadialDistinctThingsAround(center, map, radius, true).OfType<Pawn>().ToList())
            {
                if (v.Dead || !Domains.Hits(caster, v)) continue;
                if (v.RaceProps.IsAnomalyEntity || v.IsMutant || Burns(v))
                    v.TakeDamage(new DamageInfo(dmg, damage, 1f, -1f, caster));
                // it cuts the soul as Yuji's slashes do, and on its own (no Benevolent Shrine needed): enough of it
                // separates Sukuna from his host (her call 2026-11-11)
                if (!v.Dead && SukunaPsycasts.SukunaBehaviour.IsSukuna(v)) SukunaPsycasts.SukunaArc.Sever(v, SoulCut);
                if (!v.Dead) Neutralize(v, caster);
            }
            // any Prison Realm caught in it opens (on the ground, worn, or planted)
            foreach (var t in GenRadial.RadialDistinctThingsAround(center, map, radius, true).ToList())
            {
                if (t is Apparel_PrisonRealm r) r.OpenByLadder();
                else if (t is PrisonRealmTrap trap) trap.Realm?.OpenByLadder();
                else if (t is Pawn p && p.apparel != null)
                    foreach (var w in p.apparel.WornApparel.OfType<Apparel_PrisonRealm>().ToList()) w.OpenByLadder();
            }
        }

        /// <summary>Domain Amplification's strip, at range, and every toggle switched off.</summary>
        public static void Neutralize(Pawn v, Pawn caster)
        {
            AmplificationStrip.Strip(v, caster);
            foreach (var key in Toggles.Specs.Keys.ToList())
            {
                if (!Toggles.IsOn(v, key)) continue;
                if (key == "basket") WickerBasketFx.Shatter(v);   // Jacob's Ladder breaks the weave
                Toggles.Set(v, key, false, quiet: true);
            }
        }
    }
}

namespace JujutsuFundamentals
{
    public static class PrisonRealmDebug
    {
        private const string Cat = "Jujutsu Kaisen";
        private static void Say(string s) => Messages.Message("[Jujutsu dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        [DebugAction(Cat, "Gojo: Prison Realm attempt now (raid at home, sealed if away)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Seal()
        {
            var q = PrisonRealmQuest.Instance;
            if (q == null) return;
            if (q.stage != PrisonRealmQuest.Stage.None) { Say("The Prison Realm questline is already running or done this game."); return; }
            Say(q.TryCapture(true) ? "They've come for Gojo." : "Couldn't: no Gojo of yours, no hostile faction, or the Prison Realm is yours or already holds someone.");
        }

        [DebugAction(Cat, "Prison Realm: count every captor group beaten", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void BeatAll()
        {
            var q = PrisonRealmQuest.Instance;
            if (q == null || q.stage != PrisonRealmQuest.Stage.Sealed) { Say("Gojo isn't sealed right now."); return; }
            foreach (var s in q.captorSites.ToList()) q.Notify_SiteBeaten(s);
        }

        [DebugAction(Cat, "Prison Realm: days until Halloween (and today's chance)", allowedGameStates = AllowedGameStates.Playing)]
        private static void Halloween()
        {
            int d = PrisonRealmQuest.DaysToHalloween();
            Say($"{d} days to the 5th of Decembary; today's chance {PrisonRealmQuest.DayChance(d):P1}.");
        }

        [DebugAction(Cat, "Angel: find-quest now (time runs out on Gojo)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void FindAngel()
        {
            var q = PrisonRealmQuest.Instance;
            if (q == null) return;
            if (q.stage != PrisonRealmQuest.Stage.Sealed) { Say("Gojo has to be sealed first (the capture raid has to succeed)."); return; }
            q.sealedTick = Find.TickManager.TicksGame - (int)(PrisonRealmQuest.DeadlineDays * GenDate.TicksPerDay) - 1;
            Say("Time is up on the captors.");
        }

        [DebugAction(Cat, "Angel: spawn here as a colonist (testing)", actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnAngel()
        {
            var a = AngelChar.Get();
            if (a == null) return;
            if (a.Dead) { Say("The Angel is dead in this game."); return; }
            if (a.Spawned) { Say("The Angel is already on a map."); return; }
            var map = Find.CurrentMap; var cell = UI.MouseCell();
            if (!cell.InBounds(map) || !cell.Standable(map)) CellFinder.TryFindRandomCellNear(cell.ClampInsideMap(map), map, 6, c => c.Standable(map), out cell);
            if (Find.WorldPawns.Contains(a)) Find.WorldPawns.RemovePawn(a);
            GenSpawn.Spawn(a, cell, map);
            if (a.Faction != Faction.OfPlayer) a.SetFaction(Faction.OfPlayer);
        }
    }
}
