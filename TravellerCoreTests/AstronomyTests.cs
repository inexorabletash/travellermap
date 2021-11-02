using NUnit.Framework;
using TravellerCore.Features;
using TravellerCore.Util;

namespace TravellerCoreTests;
public class AstronomyTests
{
    [Test]
    [TestCase(0,0, 1,1, 1,1)]
    [TestCase(0,0, 0,0, 0,0)]
    public void TestOffsetCalculation(int x1, int y1, int x2, int y2, int xd, int yd)
    {
        var a = new Position(x1, y1);
        var b = new Position(x2, y2);
        Assert.AreEqual(
            (xd, yd), 
            Astronomy.CalculateOffset(a, b));
    }

    [Test]
    [TestCase(1, 1, 3, 1, 2.0)]
    [TestCase(1, 1, 5, 4, 5.0)]
    [TestCase(1, 1, 1, 2, 1.0)]
    [TestCase(0, 0, 0, 0, 0.0)]
    public void TestDistanceCalculation(int x1, int y1, int x2, int y2, double distance)
    {
        var a = new Position(x1, y1);
        var b = new Position(x2, y2);
        Assert.AreEqual(
            distance,
            Astronomy.CalculateDistance(a, b));
    }

    [Test]
    [TestCase(1,1, 1,3, 2)]
    [TestCase(1,1, 1,6, 5)]
    [TestCase(1,1, 1,9, 8)]
    [TestCase(1,1, 3,1, 2)]
    [TestCase(1,1, 5,4, 5)]
    [TestCase(1,1, 1,2, 1)]
    [TestCase(0,0, 0,0, 0)]
    [TestCase(1,1, 2,1, 1)]
    [TestCase(1,1, 4,1, 3)]
    [TestCase(1,1, 6,7, 9)]
    [TestCase(10,1, 1,1, 9)]
    [TestCase(1,10, 1,1, 9)]
    [TestCase(10,10, 1,1, 14)]
    [TestCase(10,10, 8,1, 10)]
    [TestCase(10,10, 1,7, 9)]
    public void TestHexDistanceCalculation(int x1, int y1, int x2, int y2, int distance)
    {
        var a = new Position(x1, y1);
        var b = new Position(x2, y2);
        Assert.AreEqual(
            distance,
            Astronomy.CalculateHexDistance(a, b));
    }
}