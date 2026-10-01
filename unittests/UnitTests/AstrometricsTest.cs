using Maps;
using Maps.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace UnitTests
{
    [TestClass]
    public class AstrometricsTest
    {
        // test/fixtures/astrometrics.json is shared with the JS tests (test/unit) so the
        // client (map.js) and server agree on coordinates and distances.
        private static Dictionary<string, object> LoadFixture()
        {
            string json = File.ReadAllText(Path.Combine(TestSetup.RepoRoot, "test", "fixtures", "astrometrics.json"));
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }

        private static IEnumerable<Dictionary<string, object>> Items(Dictionary<string, object> fixture, string key)
            => ((ArrayList)fixture[key]).Cast<Dictionary<string, object>>();

        private static int I(Dictionary<string, object> d, string key) => Convert.ToInt32(d[key]);

        [TestMethod]
        public void LocationCoordinatesMatchFixture()
        {
            foreach (var l in Items(LoadFixture(), "locations"))
            {
                var sector = new Point(I(l, "sx"), I(l, "sy"));
                var hex = new Hex((byte)I(l, "hx"), (byte)I(l, "hy"));
                var coords = Astrometrics.LocationToCoordinates(sector, hex);
                Assert.AreEqual(new Point(I(l, "x"), I(l, "y")), coords, $"{sector} {hex}");

                Location back = Astrometrics.CoordinatesToLocation(coords);
                Assert.AreEqual(sector, back.Sector, $"round trip sector {coords}");
                Assert.AreEqual(hex, back.Hex, $"round trip hex {coords}");
            }
        }

        [TestMethod]
        public void HexDistanceMatchesFixture()
        {
            foreach (var d in Items(LoadFixture(), "distances"))
            {
                var a = new Point(I(d, "ax"), I(d, "ay"));
                var b = new Point(I(d, "bx"), I(d, "by"));
                Assert.AreEqual(I(d, "d"), Astrometrics.HexDistance(a, b), $"{a} -> {b}");
            }
        }

        [TestMethod]
        public void HexDistanceProperties()
        {
            for (int x = -3; x <= 3; ++x)
            {
                for (int y = -3; y <= 3; ++y)
                {
                    var p = new Point(x, y);
                    Assert.AreEqual(0, Astrometrics.HexDistance(p, p));
                    for (int dir = 0; dir < 6; ++dir)
                    {
                        var n = Astrometrics.HexNeighbor(p, dir);
                        Assert.AreEqual(1, Astrometrics.HexDistance(p, n), $"{p} neighbor {dir} = {n}");
                        Assert.AreEqual(1, Astrometrics.HexDistance(n, p), $"symmetric {p} {n}");
                    }
                }
            }
        }

        private class HexGrid : PathFinder.IMap<Point>
        {
            public HashSet<Point> Blocked { get; } = new HashSet<Point>();
            public int Jump { get; set; } = 1;

            public IEnumerable<Point> Neighbors(Point p)
            {
                for (int x = p.X - Jump; x <= p.X + Jump; ++x)
                    for (int y = p.Y - Jump - 1; y <= p.Y + Jump + 1; ++y)
                    {
                        var q = new Point(x, y);
                        if (q != p && !Blocked.Contains(q) && Astrometrics.HexDistance(p, q) <= Jump)
                            yield return q;
                    }
            }
            public double CostEstimate(Point a, Point b) => Astrometrics.HexDistance(a, b) / (double)Jump;
            public double EdgeWeight(Point a, Point b) => 1;
        }

        [TestMethod]
        public void PathFinderFindsShortestPath()
        {
            var grid = new HexGrid();
            var start = new Point(0, 0);
            var end = new Point(4, 0);

            var path = PathFinder.FindPath(grid, start, end);
            Assert.IsNotNull(path);
            Assert.AreEqual(start, path.First());
            Assert.AreEqual(end, path.Last());
            Assert.AreEqual(5, path.Count, "4 jumps");
            for (int i = 1; i < path.Count; ++i)
                Assert.AreEqual(1, Astrometrics.HexDistance(path[i - 1], path[i]));

            // Longer jumps take fewer steps.
            grid.Jump = 2;
            Assert.AreEqual(3, PathFinder.FindPath(grid, start, end).Count, "2 jumps");
        }

        [TestMethod]
        public void PathFinderRoutesAroundAndReportsUnreachable()
        {
            var grid = new HexGrid();
            var start = new Point(0, 0);
            var end = new Point(2, 0);

            // Block the direct route; path should go around.
            grid.Blocked.Add(new Point(1, 0));
            grid.Blocked.Add(new Point(1, 1));
            var path = PathFinder.FindPath(grid, start, end);
            Assert.IsNotNull(path);
            Assert.IsFalse(path.Any(p => grid.Blocked.Contains(p)));
            Assert.AreEqual(end, path.Last());

            // Surround the destination completely.
            for (int dir = 0; dir < 6; ++dir)
                grid.Blocked.Add(Astrometrics.HexNeighbor(end, dir));
            // Bound the search space so "unreachable" terminates.
            var bounded = new BoundedGrid(grid, radius: 6);
            Assert.IsNull(PathFinder.FindPath(bounded, start, end));
        }

        private class BoundedGrid : PathFinder.IMap<Point>
        {
            private readonly HexGrid inner;
            private readonly int radius;
            public BoundedGrid(HexGrid inner, int radius) { this.inner = inner; this.radius = radius; }
            public IEnumerable<Point> Neighbors(Point p) =>
                inner.Neighbors(p).Where(q => Astrometrics.HexDistance(Point.Empty, q) <= radius);
            public double CostEstimate(Point a, Point b) => inner.CostEstimate(a, b);
            public double EdgeWeight(Point a, Point b) => inner.EdgeWeight(a, b);
        }
    }
}
