using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using VEF.Abilities;

namespace JujutsuFundamentals
{
    /// <summary>Dev mode ▸ Debug actions ▸ "Jujutsu Kaisen": Yuta's Copy part.</summary>
    public static class YutaCopyDebug
    {
        private const string Cat = "Jujutsu Kaisen";

        private static void Say(string s) => Messages.Message("[Jujutsu dev] " + s, MessageTypeDefOf.NeutralEvent, false);

        /// <summary>A fresh Yuta at a stage joins at once. Any earlier Yuta stays as an ordinary pawn.</summary>
        private static void Join(int stage)
        {
            var map = Find.CurrentMap; var t = YutaTracker.Instance;
            if (map == null || t == null) return;
            var p = Yuta.Make(stage);
            if (p == null) { Say("Couldn't make Yuta."); return; }
            t.yutaArrived = true;
            if (YutaCopyState.Instance != null) YutaCopyState.Instance.cameForHelp = stage == 1;
            Characters.WalkInAndJoin(p, map);
            Say($"Yuta (stage {stage}) joins.");
        }

        [DebugAction(Cat, "Yuta: join at stage 1 (start of JJK 0)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Join1() => Join(1);
        [DebugAction(Cat, "Yuta: join at stage 2 (end of JJK 0)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Join2() => Join(2);
        [DebugAction(Cat, "Yuta: join at stage 3 (Shinjuku)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Join3() => Join(3);
        [DebugAction(Cat, "Yuta: join at stage 4 (adult)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Join4() => Join(4);

        [DebugAction(Cat, "Yuta: asks to join (ignores the friends rule)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Ask()
        {
            if (!YutaArrival.CanAsk(true)) { Say("Yuta has already joined, is dead, or is on a map."); return; }
            YutaArrival.Ask(Find.CurrentMap);
        }

        [DebugAction(Cat, "Yuta: learn giftable techniques (Blast, Air Step, RCT)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Giftable()
        {
            var y = YutaTracker.Instance?.yuta;
            if (y == null) { Say("No Yuta yet."); return; }
            Characters.Learn(y, JJKDefOf.JJK_CursedEnergyFundamentals, JJKAbilityDefOf.JJK_Blast, JJKAbilityDefOf.JJK_AirStep, JJKAbilityDefOf.JJK_ReverseCursedTechnique);
            Characters.Learn(y, YutaDefOf.JJK_YutaPath, new[] { YutaAbilities.Def(YutaAbilities.Paste) }.Where(d => d != null).ToArray());
            PassiveSync.Sync(y);
            Say("Yuta knows Cursed Energy Blast, Air Step, Reverse Cursed Technique and Paste.");
        }

        [DebugAction(Cat, "Pawn: make a Paste recipient (psycast level 1)", actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Recipient(Pawn p)
        {
            if (p == null) return;
            Characters.Psycaster(p, 1);
            Say($"{p.LabelShort} is psycast level {p.GetPsylinkLevel()}.");
        }

        [DebugAction(Cat, "Yuta: open Authentic Mutual Love (free)", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Domain()
        {
            var y = YutaTracker.Instance?.yuta;
            if (y == null || !y.Spawned) { Say("Yuta isn't on a map."); return; }
            if (MutualLove.Compatible(y).Count == 0) { Say("Yuta knows no technique a katana can hold."); return; }
            if (!Jujutsu.Has(y, YutaCopyDefOf.JJK_MutualLoveDomain)) y.health.AddHediff(YutaCopyDefOf.JJK_MutualLoveDomain);
        }
    }
}
