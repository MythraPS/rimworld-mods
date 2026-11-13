using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace SukunaPsycasts
{
    /// <summary>
    /// The King of Curses answers to nobody. A pawn he takes over leaves their faction
    /// for this hidden one, which is hostile to everyone — including whoever they
    /// arrived with. Created on demand, so old saves pick it up the first time it is
    /// needed.
    /// </summary>
    public static class SukunaFaction
    {
        public static Faction Existing() =>
            Find.FactionManager.FirstFactionOfDef(SukunaQuestDefOf.Sukuna_KingOfCurses);

        /// <summary>The King's faction and <paramref name="other"/> become allies (her call 2026-11-12: Uraume keeps his
        /// own faction with the group holding Gojo, and that group's faction is allied with the King's; Kenjaku's too,
        /// once he is in).</summary>
        public static void AllyWith(Faction king, Faction other)
        {
            if (king == null || other == null || king == other || other.IsPlayer) return;
            if (king.RelationKindWith(other) == FactionRelationKind.Ally) return;
            king.SetRelation(new FactionRelation(other, FactionRelationKind.Ally) { baseGoodwill = 100 });
            other.SetRelation(new FactionRelation(king, FactionRelationKind.Ally) { baseGoodwill = 100 });
        }

        public static Faction Get()
        {
            var def = SukunaQuestDefOf.Sukuna_KingOfCurses;
            var existing = Find.FactionManager.FirstFactionOfDef(def);
            if (existing != null) { UseKingsIdeo(existing); return existing; }

            var relations = new List<FactionRelation>();
            foreach (var other in Find.FactionManager.AllFactionsListForReading)
                relations.Add(new FactionRelation(other, FactionRelationKind.Hostile));

            var faction = FactionGenerator.NewGeneratedFactionWithRelations(
                new FactionGeneratorParms(def, default(IdeoGenerationParms), true), relations);
            Find.FactionManager.Add(faction);
            UseKingsIdeo(faction);
            return faction;
        }

        /// <summary>The King of Curses' ideoligion (her pick 2026-11-04): Supremacist, Cannibal, Raider.
        /// Older saves' faction switches to it the next time it is needed.</summary>
        public static Ideo KingsIdeo(Faction f) =>
            CharacterIdeos.Get("sukuna", f.def, "Supremacist", "Cannibal", "Raider");

        private static void UseKingsIdeo(Faction f)
        {
            if (f?.ideos == null) return;
            var ideo = KingsIdeo(f);
            if (ideo != null && f.ideos.PrimaryIdeo != ideo) f.ideos.SetPrimary(ideo);
        }

        /// <summary>Hand the pawn to the King of Curses and point them at the colony.</summary>
        public static void Claim(Pawn pawn)
        {
            var faction = Get();
            if (pawn.Faction != faction) pawn.SetFaction(faction);
            SukunaBehaviour.RememberIdeo(pawn, faction.ideos?.PrimaryIdeo);
            CharacterIdeos.Give(pawn, faction.ideos?.PrimaryIdeo);
            if (!pawn.Spawned) return;

            pawn.GetLord()?.Notify_PawnLost(pawn, PawnLostCondition.ChangedFaction);
            LordMaker.MakeNewLord(faction,
                new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false,
                                          sappers: false, useAvoidGridSmart: false, canSteal: false),
                pawn.Map, new[] { pawn });
        }
    }
}
