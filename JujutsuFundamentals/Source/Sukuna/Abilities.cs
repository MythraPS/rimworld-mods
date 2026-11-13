using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
// RimWorld.Ability and VEF.Abilities.Ability are different classes with the same
// short name, exactly like AbilityDef. Alias, never rely on `using` order.
using VEFAbility = VEF.Abilities.Ability_Blank;

namespace SukunaPsycasts
{

    /// <summary>
    /// Where each of this path's buttons sits on the command bar.
    ///
    /// Gizmos are sorted with a STABLE sort on Gizmo.Order, so everything left at
    /// the default 0 keeps whatever order it happened to be yielded in — which is
    /// why the three sustained toggles came out ahead of Dismantle: VPE's gizmo
    /// patch passes non-Command_Ability gizmos straight through before the cast
    /// buttons. Giving every one of ours an explicit order settles it: the bar now
    /// reads in tree order, Dismantle first.
    /// </summary>
    internal static class SukunaGizmoOrder
    {
        private static readonly System.Collections.Generic.Dictionary<string, float> Orders =
            new System.Collections.Generic.Dictionary<string, float>
        {
            { "Sukuna_Dismantle",                  10f },
            { "Sukuna_Cleave",                     11f },
            { "Sukuna_SlashDefenseThorns",         12f },
            { "Sukuna_SlashDefensePseudoInfinity", 13f },
            { "Sukuna_ReverseCursedTechnique",     14f },
            { "Sukuna_MalevolentShrine",           15f },
            { "Sukuna_FireArrow",                  16f },
            { "Sukuna_WorldCuttingSlash",          17f },
        };

        internal static Gizmo Apply(Gizmo gizmo, Def def)
        {
            if (gizmo != null && def != null && Orders.TryGetValue(def.defName, out float order))
                gizmo.Order = order;
            return gizmo;
        }
    }

    /// <summary>A castable node of this path that needs nothing but its place on the
    /// bar. Ability and Ability_ShootProjectile are separate roots, so this is a
    /// method rather than a shared base class.</summary>
    internal static class OrderedGizmo
    {
        internal static Gizmo Of(VEF.Abilities.Ability ability, Gizmo baseGizmo)
            => SukunaGizmoOrder.Apply(baseGizmo, ability.def);
    }

    /// <summary>
    /// VEF draws an ability's warmup mote at rotation 0, always. The warmup toil
    /// calls MoteMaker.MakeStaticMote with the rotation argument as a literal 0f, and
    /// from then on only ever rewrites exactPosition — so the fire bow pointed north
    /// no matter where the shot was aimed.
    ///
    /// warmupMote and verb are both public fields on Ability, and Graphic_Mote draws
    /// with Quaternion.AngleAxis(exactRotation, Vector3.up), so re-aiming is a matter
    /// of writing the compass angle in each tick. Mote textures point north at
    /// rotation 0 and AngleFlat is measured clockwise from north, so the angle goes
    /// in unmodified.
    ///
    /// Driven from the vessel hediff rather than Ability.TickInterval: that one only
    /// fires for abilities sitting in CompAbilities.abilitiesToTickInterval, a list
    /// built in GiveAbility and rebuilt from needsTickingInterval only in
    /// PostExposeData — so an ability already learned in a running save is not in it
    /// until the next save/load. The hediff ticks unconditionally.
    ///
    /// The target comes from CompAbilities.currentlyCasting/currentlyCastingTargets, where VEF keeps
    /// the cast while the warmup runs. (verb.CurrentTarget, read here until 2026-11-08, is never set
    /// for these casts: it defaults to cell 0,0, so the bow pointed at the map's bottom-left corner.)
    /// </summary>
    internal static class WarmupMoteAim
    {
        internal static void Aim(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned) return;
            var ability = pawn.GetComp<CompAbilities>()?.currentlyCasting;
            var mote = ability?.warmupMote;
            if (mote == null || mote.Destroyed) return;
            FireBowAim.Aim(pawn, ability, mote);
        }
    }

    /// <summary>A tree node that is not castable. RCT and Black Flash occupy slots
    /// in the path but do their work through a permanent hediff, so the gizmo must
    /// never appear and the ability must never fire.</summary>
    public class Ability_Passive : VEFAbility
    {
        public override bool ShowGizmoOnPawn() => false;

        public override bool IsEnabledForPawn(out string reason)
        {
            reason = "Passive — always active once learned.";
            return false;
        }

        public override void Cast(GlobalTargetInfo[] targets) { }
    }

    /// <summary>Dismantle. Invisible slash; x1.5 against anything with no psychic
    /// sensitivity of its own, which is the canon Dismantle/Cleave split and is
    /// also what makes the Shrine behave correctly without a second aura.</summary>
    public class Ability_Dismantle : Ability_ShootProjectile
    {
        // Damage is applied when the slash ARRIVES (see Patch_ProjectileImpact),
        // not at cast time. Applying it here made distant targets die before the
        // slash reached them.
        public static bool NoCursedEnergy(Thing t) =>
            !(t is Pawn p) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= 0.01f;

        public override Gizmo GetGizmo() => OrderedGizmo.Of(this, base.GetGizmo());

        /// <summary>Yuji's reaches three times as far; Modulo Yuji's has no limit (her call 2026-11-02).</summary>
        public const float YujiRangeFactor = 3f, NoLimit = 999f;

        /// <summary>Range grows with fingers (her call 2026-11-07): 18.9 cells at one, 44.9 (a sniper
        /// rifle) at all twenty. Applied as a factor on the def's range so anything else scaling it still works.</summary>
        public const float RangeAtOne = 18.9f, RangeAtTwenty = 44.9f;

        public override float GetRangeForPawn()
        {
            float r = base.GetRangeForPawn();
            int n = Scaling.FingersEaten(pawn);
            if (n > 1) r *= Mathf.Lerp(1f, RangeAtTwenty / RangeAtOne, Mathf.Clamp01((n - 1) / 19f));
            if (ModuloYuji.Is(pawn)) return NoLimit;
            if (Yuji.Is(pawn)) return r * YujiRangeFactor;
            return r;
        }
    }

    /// <summary>Cleave. Damage is a share of the target's own maximum health, so it
    /// stays the tool for one hard thing while Dismantle clears everything.</summary>
    public class Ability_Cleave : VEFAbility
    {
        // Cleave is the executioner, not the workhorse: a much bigger share of the
        // target's own maximum health, with a floor so it always outhits Dismantle.
        private const float MaxHpFraction = 0.55f;
        private const float MinDamage     = 45f;

        public override Gizmo GetGizmo()
        {
            var toggle = new Command_Toggle
            {
                defaultLabel  = "Cleave",
                defaultDesc   = def.description,
                icon          = def.icon,
                isActive      = () => CleaveToggle.IsOn(pawn),
                activateSound = SoundDefOf.Tick_Tiny,
            };
            toggle.toggleAction = () =>
            {
                bool now = !CleaveToggle.IsOn(pawn);
                CleaveToggle.Set(pawn, now);
                Messages.Message(now ? "Cleave on — melee attacks now cut." : "Cleave off.",
                                 pawn, MessageTypeDefOf.NeutralEvent, false);
            };
            return SukunaGizmoOrder.Apply(toggle, def);
        }

        public override void Cast(GlobalTargetInfo[] targets) { }

        public static void Strike(Pawn caster, Thing thing)
        {
            if (caster == null || thing == null || thing.Destroyed) return;

            // A flurry, not a single stroke.
            for (int i = 0; i < 5; i++) SlashUtility.SlashFleck(thing);
            Snd.At("Sukuna_Snd_Cleave", thing.Position, thing.Map);
            if (thing is Corpse corpse) { SlashUtility.ButcherCorpse(corpse, caster); return; }

            float maxHp = (thing as Pawn)?.health?.LethalDamageThreshold ?? thing.MaxHitPoints;
            float dmg = Mathf.Max(MinDamage, maxHp * MaxHpFraction) * Scaling.Power(caster) * Scaling.AbilityBoost(caster);
            float ap  = Scaling.ArmorPen(caster, Scaling.CleaveBaseAP, Scaling.CleaveCapAP, dmg);
            thing.TakeDamage(new DamageInfo(SukunaDefOf.Sukuna_Slash, dmg, ap, -1f, caster));
            if (thing is Pawn cv) SlashUtility.NotifyAttacked(cv, caster);
        }
    }

    /// <summary>Cursed Energy Reinforcement (old cast version, unused). Once Furnace is known the same buff also tempers
    /// the caster against heat — implemented as severity 2 on the hediff.</summary>
    public class Ability_PseudoInfinity : VEFAbility
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            var h = pawn.health.hediffSet.GetFirstHediffOfDef(SukunaDefOf.Sukuna_PseudoInfinity);
            if (h == null) return;
            var comp = pawn.GetComp<CompAbilities>();
            bool furnace = comp != null && comp.HasAbility(SukunaAbilityDefOf.Sukuna_FireArrow);
            h.Severity = furnace ? 2f : 1f;
        }
    }

    /// <summary>Domain Expansion. The shrine building is cosmetic and despawns with
    /// the effect; all the damage comes from the caster-borne aura.</summary>
    public class Ability_MalevolentShrine : VEFAbility
    {
        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            // a domain is a huge strain (her call 2026-11-03): half the caster's heat limit on top of the
            // cast's own heat, never past the limit itself
            // calamity grades keep the old strain (half their limit); anyone else ends near the limit (her call 2026-11-10)
            JujutsuFundamentals.Domains.Strain(pawn);
            Messages.Message("Domain Expansion: Malevolent Shrine.",
                             pawn, MessageTypeDefOf.ThreatBig, false);
        }

        public override Gizmo GetGizmo() => OrderedGizmo.Of(this, base.GetGizmo());
    }

    /// <summary>Furnace: Fire Arrow. No combo requirement (her call 2026-11-02).</summary>
    public class Ability_FireArrow : Ability_ShootProjectile
    {

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            // The furnace is their own fire. Immunity covers the whole flight plus
            // the burn, so the caster does not die to their own arrow.
            SlashUtility.GrantFurnaceImmunity(pawn, 3600);
        }

        /// <summary>Her call 2026-11-09: big again (no 14.9 cap), but cheaper. Modulo Yuji's blast comes to
        /// radius 40-60. Only the game's radial limit stays, past which every cell lookup logs an error. The lag
        /// she reported on 8 Nov is handled by <see cref="MaxBlastFires"/>, by the burn following the blast
        /// wave (<see cref="FurnaceGround.Blast"/>), and by the lingering burn working through its cells over
        /// each second instead of all on one tick.</summary>
        public static float BlastRadius(Pawn caster) =>
            Mathf.Min(SukunaAbilityDefOf.Sukuna_FireArrow.radius * Scaling.Power(caster) * Scaling.AbilityBoost(caster),
                      HediffComp_MalevolentShrine.MaxRadius);

        /// <summary>The most fires the blast itself starts, however big it is: what the old 14.9 cap allowed
        /// (about 700 cells at <see cref="FireChance"/>).</summary>
        public const int MaxBlastFires = 240;

        /// <summary>The ring shown while aiming is the blast that will land (VEF's own figure used
        /// sensitivity only, so it never matched).</summary>
        public override float GetRadiusForPawn() => BlastRadius(pawn);

        /// <summary>Same share of the area alight as FurnaceGround keeps (it was every flammable cell).</summary>
        public const float FireChance = 0.34f;

        /// <summary>Called from the projectile impact, so there is exactly ONE
        /// detonation and it happens where the arrow lands.</summary>
        public static void Detonate(Pawn caster, IntVec3 centre, Map map, float radius)
        {
            if (map == null) return;
            if (caster != null) SlashUtility.GrantFurnaceImmunity(caster, 3600);

            int dmg = Mathf.RoundToInt(Scaling.Damage(caster, 160f) * Scaling.AbilityBoost(caster));

            float fireChance = Mathf.Min(FireChance, MaxBlastFires / (float)Mathf.Max(1, GenRadial.NumCellsInRadius(radius)));
            GenExplosion.DoExplosion(
                centre, map, radius, SukunaDefOf.Sukuna_Flame,
                caster, dmg, 0.75f,
                null, null, null, null, null, 1f, 1, null, null, 0,
                chanceToStartFire: fireChance, damageFalloff: false,
                ignoredThings: caster == null ? null : new List<Thing> { caster });

            // Flame damage alone barely scratched armoured objects — a ship chunk took
            // 7 of 300. The furnace is heat, so it also dumps real temperature into
            // every cell and burns everything present directly, which is what lets it
            // hurt mechanoids and wreckage. Since 2026-11-09 each cell burns when the blast
            // wave reaches it, instead of every cell in the same frame.
            FurnaceGround.Blast(map, centre, radius, caster, dmg);

            // Fire that keeps burning with nothing to burn, and a molten core, both
            // for ~40 seconds, and the residue bites structures and mechs too.
            FurnaceGround.Ignite(map, centre, radius, 2400, caster, dmg * 0.25f);
        }

        public override Gizmo GetGizmo() => OrderedGizmo.Of(this, base.GetGizmo());
    }

    /// <summary>World Cutting Slash. Aimed at a DIRECTION, not a target: it carves a
    /// line from the caster outward, and a pawn who steps out of the line during the
    /// long charge is simply not hit.</summary>
    public class Ability_WorldCuttingSlash : VEFAbility
    {
        /// <summary>
        /// The binding vow he placed on himself: the hand sign, Dismantle's
        /// incantation, and the palm directing the trajectory. Spoken over the charge.
        /// </summary>
        private static readonly string[] Chant =
        {
            "Sukuna forms the sign.",
            "\"Dismantle.\"",
            "The palm turns, and the world lines up behind it.",
        };

        /// <summary>Yuji's own words (her call 2026-11-07: his chant differs and names him).</summary>
        private static readonly string[] YujiChant =
        {
            "Yuji forms the sign.",
            "\"Dismantle. Nobody else dies today.\"",
            "His palm turns, and the world lines up behind it.",
        };

        public override void PreWarmupAction()
        {
            base.PreWarmupAction();
            SpeakChant(pawn);
        }

        private static void SpeakChant(Pawn p)
        {
            if (p?.Map == null) return;
            var lines = Yuji.Is(p) ? YujiChant : Chant;
            for (int i = 0; i < lines.Length; i++)
                MoteMaker.ThrowText(p.DrawPos + new Vector3(0f, 0f, 0.6f + i * 0.45f),
                                    p.Map, lines[i], new Color(0.85f, 0.25f, 0.22f), 3.5f + i);
        }

        private const float BaseDamage = 140f;

        private static TerrainDef scarCache;
        private static TerrainDef ScarTerrain
        {
            get
            {
                if (scarCache == null)
                    scarCache = DefDatabase<TerrainDef>.GetNamedSilentFail("Gravel")
                             ?? DefDatabase<TerrainDef>.GetNamedSilentFail("Sand");
                return scarCache;
            }
        }

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length == 0 || pawn.Map == null) return;

            IntVec3 origin = pawn.Position;
            IntVec3 aim    = targets[0].Cell;
            if (aim == origin) return;

            // Hand the cut to WorldCut — the destruction now chases the slash
            // outward instead of resolving the whole line instantly.
            WorldCut.Begin(pawn, pawn.Map, origin,
                           (aim - origin).ToVector3().normalized,
                           Scaling.Damage(pawn, BaseDamage) * Scaling.AbilityBoost(pawn));
        }

        public override Gizmo GetGizmo() => OrderedGizmo.Of(this, base.GetGizmo());
    }
}