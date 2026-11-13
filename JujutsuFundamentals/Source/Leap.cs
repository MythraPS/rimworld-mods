using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using VEF.Abilities;
using Ability = VEF.Abilities.Ability;
using VEFAbilityDef = VEF.Abilities.AbilityDef;

namespace JujutsuFundamentals
{
    [DefOf]
    public static class LeapDefOf
    {
        public static VEFAbilityDef JJK_Leap;
        static LeapDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(LeapDefOf));
    }

    /// <summary>
    /// Leap (her call 2026-11-11, for Toji and Maki): Air Step's jump done with the legs alone. No cursed energy, so no
    /// psyfocus, no burst at the feet, and the range doesn't grow with psychic sensitivity (proposal: 19.9 cells, a 5 s
    /// cooldown). Same landing rules as Air Step: over walls and water, never into a building or over a mountain.
    /// Colonists only leap when ordered.
    /// </summary>
    public class Ability_Leap : Ability
    {
        private bool Mine => pawn != null && pawn.IsColonistPlayerControlled;
        public override bool CanAutoCast => !Mine && base.CanAutoCast;
        public override bool AutoCast => !Mine && base.AutoCast;

        public override void Cast(GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            if (targets == null || targets.Length == 0 || pawn.Map == null) return;
            var cell = targets[0].Cell;
            var map = pawn.Map;
            if (!cell.InBounds(map) || cell == pawn.Position || Ability_AirStep.Problem(pawn, cell) != null) return;
            FleckMaker.ThrowDustPuffThick(pawn.DrawPos, map, 1.2f, new Color(0.7f, 0.65f, 0.6f));
            var flyer = PawnFlyer.MakeFlyer(ThingDefOf.PawnFlyer, pawn, cell, null, null);
            if (flyer != null) GenSpawn.Spawn(flyer, pawn.Position, map);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            var map = pawn.Map;
            string why = map == null || !target.Cell.InBounds(map) ? "Nowhere to land there." : Ability_AirStep.Problem(pawn, target.Cell);
            if (why != null)
            {
                if (showMessages) Messages.Message(why, pawn, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.ValidateTarget(target, showMessages);
        }

        public static void Give(Pawn p)
        {
            var comp = p?.GetComp<CompAbilities>();
            if (comp != null && LeapDefOf.JJK_Leap != null && !comp.HasAbility(LeapDefOf.JJK_Leap)) comp.GiveAbility(LeapDefOf.JJK_Leap);
        }
    }

    /// <summary>Toji and Maki already in a save get Leap too.</summary>
    public class LeapKeeper : GameComponent
    {
        public LeapKeeper(Game game) { }

        public override void GameComponentTick()
        {
            if (Find.TickManager.TicksGame % 2500 != 211) return;
            var t = RestrictedTracker.Instance;
            if (t == null) return;
            if (t.toji != null && !t.toji.Dead) Ability_Leap.Give(t.toji);
            if (t.maki != null && !t.maki.Dead) Ability_Leap.Give(t.maki);
        }
    }
}
