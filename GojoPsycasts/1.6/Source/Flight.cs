using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace GojoPsycasts
{
    /// <summary>
    /// Flight (her call 2026-11-04): a toggle. While it is on, Gojo goes wherever he is sent
    /// through the air, over walls, water and everything else on the ground, as long as no
    /// roof is in the way. If the only way there is under a roof (or he starts under one)
    /// he walks as normal.
    ///
    /// The vanilla path follower is skipped while he flies: a route over unroofed cells is
    /// planned once per destination and he is moved along it here, with the follower's
    /// nextCell / cost fields set so the game draws him gliding between cells.
    /// </summary>
    public static class GojoFlight
    {
        public const float SpeedFactor = 1.5f;   // x his move speed

        private class Route
        {
            public LocalTargetInfo dest;
            public PathEndMode pe;
            public List<IntVec3> cells;   // null: no open-sky way, walk
            public int i;
            public float progress;
        }

        private static readonly Dictionary<int, Route> routes = new Dictionary<int, Route>();

        public static bool Flying(Pawn p)
        {
            if (!GojoToggles.IsOn(p, GojoToggles.Fly)) return false;
            var tr = p.psychicEntropy;
            return tr != null && tr.CurrentPsyfocus > 0.001f;
        }

        // plain reflection: this compiler (mcs) does not understand ref-returning delegates
        private static readonly System.Reflection.FieldInfo PawnField = AccessTools.Field(typeof(Pawn_PathFollower), "pawn");
        private static readonly System.Reflection.FieldInfo PeField = AccessTools.Field(typeof(Pawn_PathFollower), "peMode");
        private static Pawn PawnRef(Pawn_PathFollower p) => (Pawn)PawnField.GetValue(p);
        private static PathEndMode PeRef(Pawn_PathFollower p) => (PathEndMode)PeField.GetValue(p);
        private static readonly System.Reflection.MethodInfo Arrived =
            AccessTools.Method(typeof(Pawn_PathFollower), "PatherArrived");

        private static bool Same(LocalTargetInfo a, LocalTargetInfo b) =>
            a.Thing == b.Thing && (a.Thing != null || a.Cell == b.Cell);

        /// <summary>True when this tick was handled as flight.</summary>
        public static bool Tick(Pawn_PathFollower pather)
        {
            var pawn = PawnRef(pather);
            if (pawn == null) return false;
            int id = pawn.thingIDNumber;
            if (!pather.Moving || !pawn.Spawned || !Flying(pawn)) { routes.Remove(id); return false; }

            var dest = pather.Destination;
            var pe = PeRef(pather);
            if (!routes.TryGetValue(id, out var r) || !Same(r.dest, dest) || r.pe != pe)
            {
                r = new Route { dest = dest, pe = pe, cells = Plan(pawn, dest, pe) };
                routes[id] = r;
            }
            if (r.cells == null) return false;

            var map = pawn.Map;
            if (r.i >= r.cells.Count)
            {
                routes.Remove(id);
                pather.nextCell = pawn.Position;
                if (ReachabilityImmediate.CanReachImmediate(pawn, dest, pe)) { Arrived.Invoke(pather, null); return true; }
                return false;   // the target moved off the route: walk the rest
            }

            r.progress += pawn.GetStatValue(StatDefOf.MoveSpeed) * SpeedFactor / 60f;
            while (r.progress >= 1f && r.i < r.cells.Count)
            {
                r.progress -= 1f;
                pawn.Position = r.cells[r.i];
                r.i++;
            }
            if (r.i < r.cells.Count)
            {
                var next = r.cells[r.i];
                if (next.Roofed(map)) { routes.Remove(id); return false; }   // a roof went up in the way
                pather.nextCell = next;
                pather.nextCellCostTotal = 1f;
                pather.nextCellCostLeft = 1f - Mathf.Clamp01(r.progress);
                pawn.rotationTracker.FaceCell(next);
            }
            else pather.nextCell = pawn.Position;
            return true;
        }

        private static readonly IntVec3[] Dirs =
        {
            IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West,
            IntVec3.North + IntVec3.East, IntVec3.North + IntVec3.West,
            IntVec3.South + IntVec3.East, IntVec3.South + IntVec3.West,
        };

        /// <summary>Shortest route over unroofed cells to a standable cell from which the
        /// destination counts as reached. Null if he is under a roof or there is none.</summary>
        private static List<IntVec3> Plan(Pawn pawn, LocalTargetInfo dest, PathEndMode pe)
        {
            var map = pawn.Map;
            var start = pawn.Position;
            if (start.Roofed(map) || !dest.IsValid) return null;
            var goalCell = dest.Cell;
            CellRect rect = dest.HasThing ? dest.Thing.OccupiedRect() : CellRect.SingleCell(goalCell);

            System.Func<IntVec3, bool> Goal = c =>
            {
                if (!c.Standable(map)) return false;
                if (pe == PathEndMode.OnCell) return c == goalCell;
                if (pe == PathEndMode.InteractionCell && dest.HasThing && dest.Thing.def.hasInteractionCell)
                    return c == dest.Thing.InteractionCell;
                return rect.ExpandedBy(1).Contains(c);
            };

            if (Goal(start)) return new List<IntVec3>();

            int w = map.Size.x;
            var cost = new Dictionary<int, float>();
            var from = new Dictionary<int, int>();
            var open = new SimplePriorityQueue();
            int s = map.cellIndices.CellToIndex(start);
            cost[s] = 0f;
            open.Push(s, H(start, goalCell));
            int found = -1;
            while (open.Count > 0)
            {
                int ci = open.Pop();
                var c = map.cellIndices.IndexToCell(ci);
                if (Goal(c)) { found = ci; break; }
                float cc = cost[ci];
                foreach (var d in Dirs)
                {
                    var n = c + d;
                    if (!n.InBounds(map) || n.Roofed(map)) continue;
                    int ni = map.cellIndices.CellToIndex(n);
                    float nc = cc + (d.x != 0 && d.z != 0 ? 1.4142f : 1f);
                    if (cost.TryGetValue(ni, out var old) && old <= nc) continue;
                    cost[ni] = nc; from[ni] = ci;
                    open.Push(ni, nc + H(n, goalCell));
                }
            }
            if (found < 0) return null;
            var route = new List<IntVec3>();
            for (int k = found; k != s; k = from[k]) route.Add(map.cellIndices.IndexToCell(k));
            route.Reverse();
            return route;
        }

        private static float H(IntVec3 a, IntVec3 b)
        {
            int dx = Mathf.Abs(a.x - b.x), dz = Mathf.Abs(a.z - b.z);
            return Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz);
        }

        /// <summary>A plain binary min-heap of (cell index, priority).</summary>
        private class SimplePriorityQueue
        {
            private readonly List<int> items = new List<int>();
            private readonly List<float> pri = new List<float>();
            public int Count => items.Count;

            public void Push(int item, float p)
            {
                items.Add(item); pri.Add(p);
                int i = items.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (pri[parent] <= pri[i]) break;
                    Swap(i, parent); i = parent;
                }
            }

            public int Pop()
            {
                int top = items[0];
                int last = items.Count - 1;
                items[0] = items[last]; pri[0] = pri[last];
                items.RemoveAt(last); pri.RemoveAt(last);
                int i = 0;
                while (true)
                {
                    int l = 2 * i + 1, r = l + 1, m = i;
                    if (l < items.Count && pri[l] < pri[m]) m = l;
                    if (r < items.Count && pri[r] < pri[m]) m = r;
                    if (m == i) break;
                    Swap(i, m); i = m;
                }
                return top;
            }

            private void Swap(int a, int b)
            {
                int t = items[a]; items[a] = items[b]; items[b] = t;
                float q = pri[a]; pri[a] = pri[b]; pri[b] = q;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_PathFollower), nameof(Pawn_PathFollower.PatherTick))]
    public static class Patch_FlightPatherTick
    {
        public static bool Prefix(Pawn_PathFollower __instance) =>
            !GojoToggles.AnyOn(GojoToggles.Fly) || !GojoFlight.Tick(__instance);
    }
}
