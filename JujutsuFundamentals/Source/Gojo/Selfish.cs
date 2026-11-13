using System.Linq;
using RimWorld;
using Verse;

namespace GojoPsycasts
{
    /// <summary>Selfish: unhappy whenever someone else sleeps in the same room as his bed.</summary>
    public class ThoughtWorker_SelfishSharedRoom : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            var bed = p.ownership?.OwnedBed;
            if (bed == null || !bed.Spawned) return ThoughtState.Inactive;
            if (bed.OwnersForReading.Count > 1) return ThoughtState.ActiveAtStage(0);
            var room = bed.GetRoom();
            if (room == null || room.PsychologicallyOutdoors) return ThoughtState.Inactive;
            foreach (var other in room.ContainedBeds)
                if (other != bed && other.OwnersForReading.Any(o => o != p)) return ThoughtState.ActiveAtStage(0);
            return ThoughtState.Inactive;
        }
    }

    /// <summary>Selfish: everyone else thinks a bit less of him.</summary>
    public class ThoughtWorker_SelfishCold : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {
            if (p == other || !p.RaceProps.Humanlike || other?.story?.traits == null) return false;
            return other.story.traits.HasTrait(GojoDefOf.Gojo_Selfish);
        }
    }
}
