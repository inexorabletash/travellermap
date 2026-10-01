#nullable enable
using System;
using System.Threading;

namespace Maps.Utilities
{
    /// <summary>
    /// Global generation counter for per-thread caches. Incrementing it causes every
    /// ThreadLocalCache (on every thread) to rebuild its value on next access. Used by
    /// /admin/flush so that edited data files are picked up without an app restart.
    /// </summary>
    internal static class CacheGeneration
    {
        private static int current;

        public static int Current => Volatile.Read(ref current);

        public static void InvalidateAll() => Interlocked.Increment(ref current);
    }

    /// <summary>
    /// Like ThreadLocal&lt;T&gt; (one lazily created value per thread, so values need not be
    /// thread-safe), but the value is recreated after CacheGeneration.InvalidateAll(), or
    /// after Reset() on the current thread.
    /// </summary>
    internal sealed class ThreadLocalCache<T> where T : class
    {
        private readonly Func<T> factory;
        private readonly ThreadLocal<Entry?> slot = new ThreadLocal<Entry?>();

        private sealed class Entry
        {
            public Entry(int generation, T value) { Generation = generation; Value = value; }
            public int Generation { get; }
            public T Value { get; }
        }

        public ThreadLocalCache(Func<T> factory)
        {
            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public T Value
        {
            get
            {
                // Read the generation before building, so an invalidation that happens
                // during a (slow) build causes another rebuild on the next access.
                int generation = CacheGeneration.Current;
                Entry? entry = slot.Value;
                if (entry == null || entry.Generation != generation)
                {
                    entry = new Entry(generation, factory());
                    slot.Value = entry;
                }
                return entry.Value;
            }
        }

        /// <summary>Discards the current thread's value only.</summary>
        public void Reset() => slot.Value = null;
    }
}
