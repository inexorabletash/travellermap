using System;
using System.Collections.Generic;
using System.Linq;

namespace Maps
{
    internal static class PathFinder
    {
        // A* Algorithm
        //
        // Based on notes in _AI for Game Developers_, Bourg & Seemann,
        //     O'Reilly Media, Inc., July 2004.
        private class Node<T> : IComparable<Node<T>>
        {
            public Node(T entity, double cost = 0, Node<T> parent = null)
            {
                this.entity = entity;
                this.cost = cost;
                this.parent = parent;
            }
            public T entity;
            public double cost;
            public Node<T> parent;

            public int CompareTo(Node<T> other)
            {
                if (other == null) throw new ArgumentNullException(nameof(other));
                return cost.CompareTo(other.cost);
            }
        }

        public interface Map<T>
        {
            IEnumerable<T> Adjacent(T entity);
            int Distance(T a, T b);
        }

        public static List<T> FindPath<T>(Map<T> map, T start, T end)
        {
            var open = new PriorityQueue<Node<T>>();
            var openSet = new HashSet<T>();

            var closed = new PriorityQueue<Node<T>>();
            var closedSet = new HashSet<T>();

            // add the starting node to the open list
            open.Add(new Node<T>(start));
            openSet.Add(start);

            // while the open list is not empty
            while (open.Count > 0)
            {
                // current node = node from open list with the lowest cost
                var currentNode = open.Dequeue();
                openSet.Remove(currentNode.entity);

                // if current node = goal node then path complete
                if (currentNode.entity.Equals(end)) // TODO: Why not == ?
                {
                    var path = new List<T>();

                    var node = currentNode;
                    path.Insert(0, node.entity);

                    while (node.parent != null)
                    {
                        node = node.parent;
                        path.Insert(0, node.entity);
                    }

                    return path;
                }

                // move current node to the closed list
                closed.Add(currentNode);
                closedSet.Add(currentNode.entity);

                // examine each node adjacent to the current node
                IEnumerable<T> adjacent = map.Adjacent(currentNode.entity);

                // for each adjacent node
                foreach (T t in adjacent)
                {
                    // if it isn't on the open list
                    if (openSet.Contains(t))
                        continue;

                    // and it isn't on the closed list
                    if (closedSet.Contains(t))
                        continue;

                    // and it isn't an obstacle then

                    // move it to open list and calculate cost

                    double cost = currentNode.cost + map.Distance(t, end);
                    open.Add(new Node<T>(t, cost, currentNode));
                    openSet.Add(t);
                }
            }

            return null;
        }
    }
}