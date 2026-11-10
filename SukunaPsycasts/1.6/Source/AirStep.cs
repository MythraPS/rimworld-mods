using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SukunaPsycasts
{
    /// <summary>Air Step (her call 2026-11-04: Sukuna steps on the air rather than flying):
    /// a long jump to a chosen spot. Sukuna (any vessel) and Yuji both have it.</summary>
    public class Ability_AirStep : VEF.Abilities.Ability_Blank
    {
        public const float Psyfocus = 0.02f, Heat = 6f;

        /// <summary>Her call 2026-11-08: Raise cursed energy output reaches further (x1.5, like the other boosts).</summary>
        public override float GetRangeForPawn() => base.GetRangeForPawn() * Scaling.AbilityBoost(pawn);

        /// <summary>Her call 2026-11-08: no "use on its own" switch for her colonists, so Air Step only jumps
        /// when ordered (a switch already on can't stay stuck on either). Everyone else keeps VEF's rule:
        /// a non-player pawn uses it on its own whenever it can.</summary>
        private bool Mine => pawn != null && pawn.IsColonistPlayerControlled;
        public override bool CanAutoCast => !Mine && base.CanAutoCast;
        public override bool AutoCast => !Mine && base.AutoCast;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length == 0 || pawn.Map == null) return;
            var cell = targets[0].Cell;
            var map = pawn.Map;
            if (!cell.InBounds(map) || cell == pawn.Position || Problem(pawn, cell) != null) return;
            // cost (her call 2026-11-04): psyfocus x VPE's cost factor, plus neural heat
            var tr = pawn.psychicEntropy;
            var factor = DefDatabase<StatDef>.GetNamedSilentFail("VPE_PsyfocusCostFactor");
            float cost = Psyfocus * (factor != null ? pawn.GetStatValue(factor) : 1f);
            if (tr == null || tr.CurrentPsyfocus < cost)
            {
                Messages.Message("Not enough psyfocus.", pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }
            tr.OffsetPsyfocusDirectly(-cost);
            tr.TryAddEntropy(Heat, pawn, scale: true, overLimit: false);
            var flyer = PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer, pawn, cell, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, pawn.Position, map);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            var map = pawn.Map;
            string why = map == null || !target.Cell.InBounds(map) ? "Nowhere to land there." : Problem(pawn, target.Cell);
            if (why != null)
            {
                if (showMessages) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        /// <summary>Her call 2026-11-07: over buildings but never into one, and never over a mountain.
        /// Null when the jump is fine.</summary>
        public static string Problem(Pawn pawn, IntVec3 cell)
        {
            var map = pawn.Map;
            if (!cell.Standable(map) || cell.GetEdifice(map) != null) return "Nowhere to land there.";
            foreach (var c in GenSight.PointsOnLineOfSight(pawn.Position, cell))
            {
                if (!c.InBounds(map)) continue;
                var roof = c.GetRoof(map);
                var e = c.GetEdifice(map);
                if ((roof != null && roof.isThickRoof) || (e != null && e.def.building != null && e.def.building.isNaturalRock))
                    return "A mountain is in the way.";
            }
            return null;
        }
    }

    /// <summary>Hands Air Step to every vessel of Sukuna and to Yuji, old saves included.</summary>
    public class AirStepKeeper : GameComponent
    {
        public AirStepKeeper(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 250 != 0) return;
            var def = SukunaAbilityDefOf.Sukuna_AirStep;
            if (def == null) return;
            foreach (var map in Find.Maps)
                foreach (var p in map.mapPawns.AllPawnsSpawned)
                {
                    if (!p.RaceProps.Humanlike) continue;
                    var comp = p.GetComp<VEF.Abilities.CompAbilities>();
                    if (comp == null) continue;
                    // the passive nodes come from the psycast path, whoever learned them (her call 2026-11-08:
                    // Modulo Yuji had no Cursed Energy Reinforcement, since only the vessel condition added it)
                    HediffComp_VesselPassives.Match(p, comp, SukunaAbilityDefOf.Sukuna_BlackFlash, SukunaDefOf.Sukuna_BlackFlash);
                    HediffComp_VesselPassives.Match(p, comp, SukunaAbilityDefOf.Sukuna_SlashDefensePseudoInfinity, SukunaDefOf.Sukuna_PseudoInfinity);
                    // her call 2026-11-06: Air Step comes with Cursed Energy Reinforcement (it is cursed
                    // energy pushed out through the soles), and goes if that is ever lost
                    bool reinforced = comp.HasAbility(SukunaAbilityDefOf.Sukuna_SlashDefensePseudoInfinity);
                    if (reinforced && !comp.HasAbility(def)) comp.GiveAbility(def);
                    else if (!reinforced && comp.HasAbility(def)) comp.LearnedAbilities.RemoveAll(a => a.def == def);
                    // older saves: a Yuji who already learned Malevolent Shrine gets Benevolent in its place
                    if (YujiDomain.Owns(p) && comp.LearnedAbilities.Any(a => a.def == SukunaAbilityDefOf.Sukuna_MalevolentShrine))
                        YujiDomain.Awaken(p);
                }
        }
    }
}
