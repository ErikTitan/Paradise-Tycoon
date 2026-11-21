using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ParadiseTycoon.Guests
{
    // Uses the same segment endpoints as the island artwork, including junctions in the middle of a segment.
    public sealed class IslandGuestPaths
    {
        const float Tolerance = .015f;
        readonly List<Vector2> nodes = new();
        readonly List<List<int>> neighbors = new();

        public IslandGuestPaths(Transform segments)
        {
            var edges = new List<(int a, int b)>();
            foreach (Transform segment in segments)
                edges.Add((Node(segment.Find("Start").position), Node(segment.Find("End").position)));
            foreach (var edge in edges)
            {
                Vector2 from = nodes[edge.a], direction = nodes[edge.b] - from;
                if (direction.sqrMagnitude < Tolerance * Tolerance) continue;
                var split = new List<(int index, float t)>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    float t = Vector2.Dot(nodes[i] - from, direction) / direction.sqrMagnitude;
                    if (t >= 0 && t <= 1 && Vector2.Distance(nodes[i], from + t * direction) < Tolerance)
                        split.Add((i, t));
                }
                split.Sort((a,b) => a.t.CompareTo(b.t));
                for (int i = 1; i < split.Count; i++)
                {
                    int a = split[i-1].index, b = split[i].index;
                    if (!neighbors[a].Contains(b)) neighbors[a].Add(b);
                    if (!neighbors[b].Contains(a)) neighbors[b].Add(a);
                }
            }
        }

        int Node(Vector2 point)
        {
            int index = nodes.FindIndex(p => Vector2.Distance(p, point) < Tolerance);
            if (index >= 0) return index;
            nodes.Add(point);
            neighbors.Add(new List<int>());
            return nodes.Count - 1;
        }

        public Vector2[] Route(Vector2 origin, Vector2 destination)
        {
            int start = nodes.FindIndex(p => Vector2.Distance(p, origin) < Tolerance);
            int end = nodes.FindIndex(p => Vector2.Distance(p, destination) < Tolerance);
            if (start < 0 || end < 0) throw new InvalidOperationException("Guest entrance is not on the island path.");
            float[] distances = Enumerable.Repeat(float.PositiveInfinity, nodes.Count).ToArray();
            int[] previous = Enumerable.Repeat(-1, nodes.Count).ToArray();
            bool[] visited = new bool[nodes.Count];
            distances[start] = 0;
            while (true)
            {
                int current = -1;
                float shortest = float.PositiveInfinity;
                for (int i = 0; i < nodes.Count; i++)
                    if (!visited[i] && distances[i] < shortest) { current = i; shortest = distances[i]; }
                if (current < 0) throw new InvalidOperationException("Guest entrances are disconnected.");
                if (current == end) break;
                visited[current] = true;
                foreach (int next in neighbors[current])
                {
                    float distance = shortest + Vector2.Distance(nodes[current], nodes[next]);
                    if (distance < distances[next]) { distances[next] = distance; previous[next] = current; }
                }
            }
            var route = new List<Vector2>();
            for (int i = end; i >= 0; i = previous[i]) route.Add(nodes[i]);
            route.Reverse();
            return route.ToArray();
        }
    }
}

