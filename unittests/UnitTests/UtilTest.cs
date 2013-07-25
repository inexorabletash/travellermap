using Json;
using Maps;
using Maps.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace UnitTests
{
    [TestClass]
    public class UtilTest
    {
        [TestMethod]
        public void SequenceTest()
        {
            CollectionAssert.AreEqual(new int[] { 0 }, Util.Sequence(0, 0).ToArray());
            CollectionAssert.AreEqual(new int[] { 0, 1 }, Util.Sequence(0, 1).ToArray());
            CollectionAssert.AreEqual(new int[] { 0, 1, 2, 3, 4 }, Util.Sequence(0, 4).ToArray());
            CollectionAssert.AreEqual(new int[] { 4, 3, 2, 1, 0 }, Util.Sequence(4, 0).ToArray());
        }

        [TestMethod]
        public void SafeSubstringTest()
        {
            Assert.AreEqual("abc", Util.SafeSubstring("abc", 0, 100));
            Assert.AreEqual("", Util.SafeSubstring("abc", 100, 100));
            Assert.AreEqual("c", Util.SafeSubstring("abc", 2, 100));
        }

        [TestMethod]
        public void GlobTest()
        {
            Assert.IsTrue(new Glob("*.foo").IsMatch("abc.foo"));
            Assert.IsFalse(new Glob("*.foo").IsMatch("abc.foo.bar"));
            Assert.IsTrue(new Glob("a*b*c").IsMatch("abc"));
            Assert.IsTrue(new Glob("a*b*c").IsMatch("aXXbc"));
            Assert.IsTrue(new Glob("a*b*c").IsMatch("abXXc"));
            Assert.IsTrue(new Glob("a*b*c").IsMatch("aXXbXXXc"));
            Assert.IsFalse(new Glob("a*b*c").IsMatch("ac"));
            Assert.IsFalse(new Glob("a*b*c").IsMatch("Xabc"));
            Assert.IsFalse(new Glob("a*b*c").IsMatch("abcX"));

            Assert.IsTrue(new Glob("?.foo").IsMatch("a.foo"));
            Assert.IsFalse(new Glob("?.foo").IsMatch("ab.foo"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("abc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("aXbc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("abXc"));
            Assert.IsTrue(new Glob("a?b?c").IsMatch("aXbXc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("aXXbc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("abXXc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("aXXbXXc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("ac"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("Xabc"));
            Assert.IsFalse(new Glob("a?b?c").IsMatch("abcX"));
        }
    }
}
