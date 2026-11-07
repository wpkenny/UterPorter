using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UterPorter
{
    /// <summary>Plans a walking route over land, like a sat-nav: a grid of 8 m squares between start and goal,
    /// using the world's terrain height (known even where the world isn't loaded yet). Deep water, lava and
    /// cliffs are off limits; slopes and shallows cost extra. A* search finds the cheapest way, so he follows
    /// coasts round lakes instead of wading out onto rocks.</summary>
    public static class RoutePlanner
    {
        public const float Cell = 8f;
        private const float Margin = 200f;          // room to go round things, beyond the box between start and goal
        private const int MaxExpansions = 120000;    // give up rather than search the whole world
        private const float WadeDepth = 0.6f;        // deeper than this below sea level is "deep water"
        private const float MaxClimbPerCell = 6.5f;  // more height change than this over 8 m is a cliff

        public class Result { public bool Done; public bool Found; public List<Vector3> Path = new List<Vector3>(); }

        /// <summary>Runs over several frames (it yields now and then); check result.Done.</summary>
        public static IEnumerator Plan(Vector3 from, Vector3 to, Result result)
        {
            var wg = WorldGenerator.instance;
            float water = ZoneSystem.instance ? ZoneSystem.instance.m_waterLevel : 30f;
            float minX = Mathf.Min(from.x, to.x) - Margin, minZ = Mathf.Min(from.z, to.z) - Margin;
            int w = Mathf.CeilToInt((Mathf.Abs(from.x - to.x) + 2 * Margin) / Cell) + 1;
            int h = Mathf.CeilToInt((Mathf.Abs(from.z - to.z) + 2 * Margin) / Cell) + 1;

            Vector2Int ToCell(Vector3 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - minX) / Cell), 0, w - 1),
                                                           Mathf.Clamp(Mathf.RoundToInt((p.z - minZ) / Cell), 0, h - 1));
            Vector3 ToWorld(Vector2Int c) => new Vector3(minX + c.x * Cell, 0f, minZ + c.y * Cell);

            var heights = new Dictionary<int, float>();
            float HeightAt(Vector2Int c)
            {
                int key = c.y * w + c.x;
                if (heights.TryGetValue(key, out var v)) return v;
                var p = ToWorld(c);
                // Loaded ground includes the player's digging and raising; elsewhere use the world's own shape.
                if (!Heightmap.GetHeight(p, out v)) v = wg != null ? wg.GetHeight(p.x, p.z) : water + 1f;
                heights[key] = v;
                return v;
            }
            bool Blocked(Vector2Int c, float ht)
            {
                if (ht < water - WadeDepth) return true;
                var p = ToWorld(c);
                var hm = Heightmap.FindHeightmap(p);
                if (hm && hm.IsLava(p)) return true;
                return false;
            }

            var start = ToCell(from);
            var goal = ToCell(to);
            var cameFrom = new Dictionary<int, int>();
            var cost = new Dictionary<int, float> { [start.y * w + start.x] = 0f };
            var open = new MinHeap();
            open.Push(start.y * w + start.x, Vector2Int.Distance(start, goal) * Cell);
            var closed = new HashSet<int>();
            int expansions = 0;
            int reached = -1;

            while (open.Count > 0)
            {
                int cur = open.Pop();
                if (!closed.Add(cur)) continue;
                var c = new Vector2Int(cur % w, cur / w);
                if (Mathf.Abs(c.x - goal.x) <= 1 && Mathf.Abs(c.y - goal.y) <= 1) { reached = cur; break; }
                if (++expansions > MaxExpansions) break;
                if (expansions % 2000 == 0) yield return null;   // keep the game smooth while planning

                float hc = HeightAt(c);
                for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (dx == 0 && dz == 0) continue;
                    var n = new Vector2Int(c.x + dx, c.y + dz);
                    if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h) continue;
                    int nk = n.y * w + n.x;
                    if (closed.Contains(nk)) continue;
                    float hn = HeightAt(n);
                    bool isGoal = Mathf.Abs(n.x - goal.x) <= 1 && Mathf.Abs(n.y - goal.y) <= 1;
                    if (!isGoal && Blocked(n, hn)) continue;
                    float step = (dx != 0 && dz != 0 ? 1.414f : 1f) * Cell;
                    float climb = Mathf.Abs(hn - hc);
                    if (climb > MaxClimbPerCell * (step / Cell) && !isGoal) continue;   // cliff
                    float extra = climb * 2.5f;                                         // slopes are slow
                    if (hn < water + 0.3f) extra += step * 2f;                          // wading is slow and risky
                    float total = cost[cur] + step + extra;
                    if (cost.TryGetValue(nk, out var old) && old <= total) continue;
                    cost[nk] = total;
                    cameFrom[nk] = cur;
                    open.Push(nk, total + Vector2Int.Distance(n, goal) * Cell);
                }
            }

            if (reached >= 0)
            {
                var cells = new List<int>();
                for (int k = reached; k != start.y * w + start.x && cameFrom.ContainsKey(k); k = cameFrom[k]) cells.Add(k);
                cells.Reverse();
                // Every third square is plenty to steer by; the game's own walking handles the bits between.
                for (int i = 2; i < cells.Count; i += 3)
                {
                    var c = new Vector2Int(cells[i] % w, cells[i] / w);
                    var p = ToWorld(c); p.y = HeightAt(c);
                    result.Path.Add(p);
                }
                result.Path.Add(to);
                result.Found = true;
            }
            result.Done = true;
        }

        /// <summary>A small binary heap keyed by priority, for the search.</summary>
        private class MinHeap
        {
            private readonly List<(int item, float pri)> _h = new List<(int, float)>();
            public int Count => _h.Count;

            public void Push(int item, float pri)
            {
                _h.Add((item, pri));
                int i = _h.Count - 1;
                while (i > 0)
                {
                    int parent = (i - 1) / 2;
                    if (_h[parent].pri <= _h[i].pri) break;
                    (_h[parent], _h[i]) = (_h[i], _h[parent]);
                    i = parent;
                }
            }

            public int Pop()
            {
                var top = _h[0].item;
                _h[0] = _h[_h.Count - 1];
                _h.RemoveAt(_h.Count - 1);
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1, r = l + 1, m = i;
                    if (l < _h.Count && _h[l].pri < _h[m].pri) m = l;
                    if (r < _h.Count && _h[r].pri < _h[m].pri) m = r;
                    if (m == i) break;
                    (_h[m], _h[i]) = (_h[i], _h[m]);
                    i = m;
                }
                return top;
            }
        }
    }
}
