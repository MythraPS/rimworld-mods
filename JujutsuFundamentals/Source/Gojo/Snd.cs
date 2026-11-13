using Verse;
using Verse.Sound;

namespace GojoPsycasts
{
    /// <summary>Plays one of the mod's sounds (1.6/Defs/SoundDefs, made by tools/sounds.py) at a cell.</summary>
    public static class Snd
    {
        public static void At(string defName, IntVec3 cell, Map map)
        {
            if (map == null) return;
            DefDatabase<SoundDef>.GetNamedSilentFail(defName)?.PlayOneShot(new TargetInfo(cell, map));
        }
    }
}
