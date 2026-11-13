using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace GojoPsycasts
{
    /// <summary>What a Black Flash looks and sounds like (her call 2026-11-07; ideas from "Rim All-Stars"):
    /// its own impact sound, a black-and-red burst growing out of the hit, "Black flash!" floating over
    /// the target instead of a message, and a few seconds "in the zone" for the one who landed it.</summary>
    public static class BlackFlashFx
    {
        public const float ZoneChance = 1.5f;
        private static SoundDef sound; private static FleckDef burst; private static HediffDef zone;
        private static SoundDef Sound => sound ?? (sound = DefDatabase<SoundDef>.GetNamedSilentFail("Gojo_Snd_BlackFlash"));
        private static FleckDef Burst => burst ?? (burst = DefDatabase<FleckDef>.GetNamedSilentFail("Gojo_Fleck_BlackFlashBurst"));
        public static HediffDef Zone => zone ?? (zone = DefDatabase<HediffDef>.GetNamedSilentFail("Gojo_BlackFlashZone"));

        public static bool InZone(Pawn p) => Zone != null && p?.health?.hediffSet?.HasHediff(Zone) == true;

        public static void Landed(Pawn attacker, Thing victim)
        {
            var map = victim?.Map;
            if (map == null) return;
            Sound?.PlayOneShot(new TargetInfo(victim.Position, map));
            if (Burst != null)
            {
                var data = FleckMaker.GetDataStatic(victim.DrawPos, map, Burst, 1.6f);
                data.rotation = Rand.Range(0f, 360f);
                map.flecks.CreateFleck(data);
            }
            MoteMaker.ThrowText(victim.DrawPos + new Vector3(0f, 0f, 0.6f), map, "Black flash!", new Color(0.9f, 0.12f, 0.15f), 2.5f);
            if (attacker != null && Zone != null && !attacker.Dead)
            {
                var h = attacker.health.hediffSet.GetFirstHediffOfDef(Zone);
                if (h != null) attacker.health.RemoveHediff(h);   // a fresh 10 seconds
                attacker.health.AddHediff(Zone);
            }
        }
    }
}
