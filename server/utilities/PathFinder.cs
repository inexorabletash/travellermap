#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Maps.Utilities
{
    internal static class PathFinder
    {
        // A* Algorithm

        private class Node<T> : IComparable<Node<T>> where T : notnull
        {
            public Node(T entity, double fScore)
            {
                this.entity = entity;
                this.fScore = fScore;
            }
            public T entity;
            public double fScore;
            public int CompareTo(Node<T> other)
            {
                if (other == null) throw new ArgumentNullException(nameof(other));
                return fScore.CompareTo(other.fScore);
            }
        }

        public interface IMap<T>
        { 
            IEnumerable<T> Neighbors(T entity);
            double CostEstimate(T a, T b);
            double EdgeWeight(T a, T b);
        }


        // https://en.wikipedia.org/wiki/A*_search_algorithm
        // A* finds a path from start to goal.
        public static List<T>? FindPath<T>(IMap<T> map, T start, T end) where T : notnull
        {
            List<T> reconstruct_path(Dictionary<T, T> cameFrom, T current)
            {
                var path = new List<T>();
                path.Insert(0, current);
                while (cameFrom.ContainsKey(current))
                {
                    current = cameFrom[current];
                    path.Insert(0, current);
                }
                return path;
            }

            // h is the heuristic function. h(n) estimates the cost to reach goal from node n.
            double h(T t) => map.CostEstimate(t, end);

            // d(current,neighbor) is the weight of the edge from current to neighbor
            double d(T current, T neighbor) => map.EdgeWeight(current, neighbor);


            // The set of discovered nodes that may need to be (re-)expanded.
            // Initially, only the start node is known.
            var open = new PriorityQueue<Node<T>>();
            var openSet = new Dictionary<T, Node<T>>();
            var start_node = new Node<T>(start, h(start));
            open.Add(start_node);
            openSet[start] = start_node;

            // For node n, cameFrom[n] is the node immediately preceding it on the cheapest path from the start
            // to n currently known.
            var cameFrom = new Dictionary<T, T>();

            // For node n, gScore[n] is the cost of the cheapest path from start to n currently known.
            var gScore = new Dictionary<T, double>
            {
                [start] = 0
            };

            // For node n, fScore[n] := gScore[n] + h(n). fScore[n] represents our current best guess as to
            // how cheap a path could be from start to finish if it goes through n.
            // fScore[n] is stored in open (the PriorityQueue)

            while (openSet.Count > 0)
            {
                // current := the node in openSet having the lowest fScore[] value
                var node = open.Dequeue();
                var current = node.entity;
                openSet.Remove(current);

                if (current.Equals(end))
                    return reconstruct_path(cameFrom, current);

                foreach (T neighbor in map.Neighbors(current))
                {
                    // tentative_gScore is the distance from start to the neighbor through current

                    var tentative_gScore = gScore[current] + d(current, neighbor);
                    if (!gScore.ContainsKey(neighbor) || tentative_gScore < gScore[neighbor])
                    {
                        // This path to neighbor is better than any previous one. Record it!
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentative_gScore;
                        var fScore_neighbor = tentative_gScore + h(neighbor);
                        if (!openSet.ContainsKey(neighbor))
                        {
                            var neighbor_node = new Node<T>(neighbor, fScore_neighbor);
                            open.Add(neighbor_node);
                            openSet[neighbor] = neighbor_node;
                        } 
                        else
                        {
                            // Update with newer fScore
                            var neighbor_node = openSet[neighbor];
                            neighbor_node.fScore = fScore_neighbor;
                        }
                    }
                }
            }

            return null;
        }
    }
}