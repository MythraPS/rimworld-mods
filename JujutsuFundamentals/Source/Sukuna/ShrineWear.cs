using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using JujutsuFundamentals;

namespace SukunaPsycasts
{
    /// <summary>
    /// Malevolent Shrine grinds down anti-domain techniques (her call 2026-11-11, from the manga: Gojo's Simple Domain
    /// held against its sure hit in ch. 226; Yuji's cracked under the slashes in ch. 258). Hollow Wicker Basket and
    /// Simple Domain still stop its sure hit, but every wave of slashes costs whoever holds them psyfocus, on top of the
    /// toggle's own drain. When they can't pay, it breaks and drops: fully broken, no slashes leak through before that.
    /// Proposal: 2% psyfocus a wave (a wave is every half second) x Sukuna's power / the holder's power, between 0.5% and
    /// 10%; an equal holder lasts about 25 s of a 60 s domain, a weaker one much less.
    /// </summary>
    public static class ShrineWear
    {
        public const float BaseCost = 0.02f, MinCost = 0.005f, MaxCost = 0.10f;

        public static void Wave(Pawn sukuna, IntVec3 anchor, float radius, Map map)
        {
            if (sukuna == null || map == null) return;
            var holders = new List<(Pawn p, string key)>();
            foreach (var sd in Hediff_SimpleDomain.Active)
            {
                var u = sd.pawn;
                if (u == null || u == sukuna || u.Map != map || !u.Spawned) continue;
                if (u.Position.DistanceTo(anchor) <= radius + sd.Radius) holders.Add((u, "simpledomain"));
            }
            foreach (var p in map.mapPawns.AllPawnsSpawned)
            {
                if (p == sukuna || p.Position.DistanceTo(anchor) > radius) continue;
                if (Jujutsu.Has(p, JJKDefOf.JJK_HollowWickerBasket)) holders.Add((p, "basket"));
            }
            foreach (var h in holders) Wear(sukuna, h.p, h.key);
        }

        private static void Wear(Pawn sukuna, Pawn holder, string key)
        {
            var tr = holder.psychicEntropy;
            float cost = Mathf.Clamp(BaseCost * Jujutsu.Power(sukuna) / Mathf.Max(0.1f, Jujutsu.Power(holder)), MinCost, MaxCost);
            if (tr != null && tr.CurrentPsyfocus > cost)
            {
                tr.OffsetPsyfocusDirectly(-cost);
                if (key == "basket") WickerBasketFx.Crack(holder);   // the slashes crack the weave
                return;
            }
            if (key == "basket") WickerBasketFx.Shatter(holder);
            Toggles.Set(holder, key, false, quiet: true);
            // a toggle that is only a hediff (no toggle entry) is removed directly
            var def = key == "basket" ? JJKDefOf.JJK_HollowWickerBasket : JJKDefOf.JJK_SimpleDomain;
            var hd = holder.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (hd != null) holder.health.RemoveHediff(hd);
            if (holder.Spawned)
            {
                FleckMaker.ThrowMicroSparks(holder.DrawPos, holder.Map);
                MoteMaker.ThrowText(holder.DrawPos, holder.Map, "Broken", new Color(1f, 0.4f, 0.4f), 2f);
            }
            if (holder.Faction == Faction.OfPlayer)
                Messages.Message($"{holder.LabelShortCap}'s {(key == "basket" ? "Hollow Wicker Basket" : "Simple Domain")} broke under Malevolent Shrine.",
                                 holder, MessageTypeDefOf.NegativeEvent, false);
        }
    }
}
