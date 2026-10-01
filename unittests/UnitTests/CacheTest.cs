using Maps.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace UnitTests
{
    [TestClass]
    public class CacheTest
    {
        private class Box { public int Id; }

        private static T OnNewThread<T>(System.Func<T> f)
        {
            T result = default(T);
            var t = new Thread(() => result = f());
            t.Start();
            t.Join();
            return result;
        }

        [TestMethod]
        public void ThreadLocalCacheReusesValuePerThread()
        {
            int built = 0;
            var cache = new ThreadLocalCache<Box>(() => new Box { Id = Interlocked.Increment(ref built) });

            Box first = cache.Value, second = cache.Value;
            Assert.AreSame(first, second, "repeated access returns the cached value");
            Assert.AreEqual(1, built);

            // Another thread gets its own value.
            Box other = OnNewThread(() => cache.Value);
            Assert.AreNotSame(cache.Value, other);
            Assert.AreEqual(2, built);
        }

        [TestMethod]
        public void InvalidateAllRebuildsOnEveryThread()
        {
            var cache = new ThreadLocalCache<Box>(() => new Box());

            // A long-lived second thread holds its own value across the invalidation,
            // like an IIS worker thread.
            var invalidated = new ManualResetEventSlim();
            Box before = null, after = null;
            var worker = new Thread(() =>
            {
                before = cache.Value;
                invalidated.Wait();
                after = cache.Value;
            });
            worker.Start();

            Box mine = cache.Value;
            while (before == null) Thread.Sleep(1);

            CacheGeneration.InvalidateAll();
            invalidated.Set();
            worker.Join();

            Assert.AreNotSame(mine, cache.Value, "this thread rebuilds");
            Assert.AreNotSame(before, after, "other threads rebuild too");
        }

        [TestMethod]
        public void ResetOnlyAffectsCurrentThread()
        {
            var cache = new ThreadLocalCache<Box>(() => new Box());
            var reset = new ManualResetEventSlim();
            var ready = new ManualResetEventSlim();
            Box before = null, after = null;
            var worker = new Thread(() =>
            {
                before = cache.Value;
                ready.Set();
                reset.Wait();
                after = cache.Value;
            });
            worker.Start();
            ready.Wait();

            Box mine = cache.Value;
            cache.Reset();
            reset.Set();
            worker.Join();

            Assert.AreNotSame(mine, cache.Value, "this thread rebuilds");
            Assert.AreSame(before, after, "other threads keep their value");
        }

        [TestMethod]
        public void InvalidationDuringBuildCausesAnotherBuild()
        {
            int built = 0;
            ThreadLocalCache<Box> cache = null;
            cache = new ThreadLocalCache<Box>(() =>
            {
                // Simulate /admin/flush arriving while data is being loaded.
                if (Interlocked.Increment(ref built) == 1)
                    CacheGeneration.InvalidateAll();
                return new Box { Id = built };
            });

            Assert.AreEqual(1, cache.Value.Id);  // Built with possibly-stale data...
            Assert.AreEqual(2, cache.Value.Id);  // ...so rebuilt on next access.
            Assert.AreEqual(2, cache.Value.Id);
        }
    }
}
