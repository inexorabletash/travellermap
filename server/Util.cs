using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps
{
    internal static class Util
    {
        public const string MediaTypeName_Image_Png = "image/png";
        public static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static string FixCapitalization(string s)
        {
            // TODO: Handle "I'Sred*N..."

            StringBuilder sb = new StringBuilder(s.Length);
            bool leading = true;
            foreach (char c in s)
            {
                if (Char.IsLetter(c) || c == '\'')
                {
                    if (leading)
                    {
                        sb.Append(Char.ToUpperInvariant(c));
                        leading = false;
                    }
                    else
                    {
                        sb.Append(Char.ToLowerInvariant(c));
                    }
                }
                else
                {
                    sb.Append(c);
                    leading = true;
                }
            }

            return sb.ToString();
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            T tmp = a;
            a = b;
            b = tmp;
        }

        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0)
                return min;
            else if (value.CompareTo(max) > 0)
                return max;
            else
                return value;
        }

        public static bool InRange<T>(T item, T a, T b) where T : IComparable<T> { return item.CompareTo(a) >= 0 && item.CompareTo(b) <= 0; }

        public static bool InList<T>(T item, T o1, T o2) { return item.Equals(o1) || item.Equals(o2); }
        public static bool InList<T>(T item, T o1, T o2, T o3) { return item.Equals(o1) || item.Equals(o2) || item.Equals(o3); }
        public static bool InList<T>(T item, T o1, T o2, T o3, T o4) { return item.Equals(o1) || item.Equals(o2) || item.Equals(o3) || item.Equals(o4); }
        public static bool InList<T>(T item, T o1, T o2, T o3, T o4, T o5) { return item.Equals(o1) || item.Equals(o2) || item.Equals(o3) || item.Equals(o4) || item.Equals(o5); }
        public static bool InList<T>(T item, T o1, T o2, T o3, T o4, T o5, T o6) { return item.Equals(o1) || item.Equals(o2) || item.Equals(o3) || item.Equals(o4) || item.Equals(o5) || item.Equals(o6); }

        public static string Truncate(this string value, int size)
        {
            return value.Length <= size ? value : value.Substring(0, size);
        }

        public static string SafeSubstring(this string s, int start, int length)
        {
            if (start > s.Length)
                return string.Empty;
            if (start + length > s.Length)
                return s.Substring(start);
            return s.Substring(start, length);
        }

        public static Stream ToStream(this string str, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            MemoryStream stream = new MemoryStream();
            using (StreamWriter writer = new NoCloseStreamWriter(stream, encoding))
            {
                writer.Write(str);
                writer.Flush();
            }
            stream.Position = 0;
            return stream;
        }

        // TODO: Could be a variant of Enumerable.Range(...).Select(...)
        public static IEnumerable<int> Sequence(int start, int end)
        {
            int current = start, delta = (start < end) ? 1 : -1;
            yield return current;
            while (current != end)
            {
                current += delta;
                yield return current;
            }
        }


        public static Func<A1, A2, R> Memoize2<A1, A2, R>(this Func<A1, A2, R> f)
        {
            var map = new Dictionary<Tuple<A1, A2>, R>();
            return (a1, a2) =>
            {
                var key = Tuple.Create(a1, a2);
                R value;
                if (map.TryGetValue(key, out value))
                    return value;
                value = f(a1, a2);
                map.Add(key, value);
                return value;
            };
        }

    }

    // Like Regex, but takes shell-style globs:
    //  - case insensitive by default
    //  - * matches 0-or-more of anything
    //  - ? matches exactly one of anything
    [Serializable]
    internal class Glob : Regex
    {
        public Glob(string pattern, RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline)
            : base("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", options) { }
        protected Glob(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    [Serializable]
    internal class RegexDictionary<T> : Dictionary<Regex, T>
    {
        public RegexDictionary() { }
        protected RegexDictionary(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public virtual void Add(string r, T v) { Add(new Regex(r), v); }
        public virtual void Add(T v) { Add(new Regex("^" + Regex.Escape(v.ToString()) + "$"), v); }

        public T Match(string s)
        {
            return this.FirstOrDefault(pair => pair.Key.IsMatch(s)).Value;
        }
        public bool IsMatch(string s)
        {
            return this.Any(pair => pair.Key.IsMatch(s));
        }
    }

    [Serializable]
    internal class GlobDictionary<T> : RegexDictionary<T>
    {
        public GlobDictionary() { }
        protected GlobDictionary(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public override void Add(string r, T v) { Add(new Glob(r), v); }
        public override void Add(T v) { Add(new Glob(v.ToString()), v); }
    }

    // Don't close the underlying stream when disposed; must be disposed
    // before the underlying stream is.
    internal class NoCloseStreamReader : StreamReader
    {
        public NoCloseStreamReader(Stream stream, Encoding encoding, bool detectEncodingFromByteOrderMarks, int bufferSize)
            : base(stream, encoding, detectEncodingFromByteOrderMarks, bufferSize)
        {
        }

        protected override void Dispose(bool disposeManaged)
        {
            base.Dispose(false);
        }
    }

    // Don't close the underlying stream when disposed; must be disposed
    // before the underlying stream is.
    internal class NoCloseStreamWriter : StreamWriter
    {
        public NoCloseStreamWriter(Stream stream, Encoding encoding)
            : base(stream, encoding)
        {
        }

        protected override void Dispose(bool disposeManaged)
        {
            base.Dispose(false);
        }
    }

    internal sealed class OrderedHashSet<T> : IEnumerable<T>
    {
        private List<T> list = new List<T>();
        private HashSet<T> set = new HashSet<T>();

        public void Add(T item)
        {
            if (set.Contains(item))
                return;
            list.Add(item);
            set.Add(item);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection)
                Add(item);
        }

        public void Remove(T item)
        {
            if (set.Remove(item))
                list.Remove(item);
        }

        public bool Contains(T item)
        {
            return set.Contains(item);
        }

        public void Clear()
        {
            list.Clear();
            set.Clear();
        }

        public bool Any(Func<T, bool> predicate)
        {
            return set.Any(predicate);
        }

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            return list.Where(predicate);
        }

        public int Count() { return set.Count(); }

        public T this[int index] { get { return list[index]; } }

        public IEnumerator<T> GetEnumerator() { return list.GetEnumerator(); }
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    internal class ErrorLogger
    {
        public enum Severity {
            Fatal,
            Error,
            Warning,
            Hint
        }

        private struct Record
        {
            public Record(Severity severity, string message)
            {
                this.severity = severity;
                this.message = message;
            }
            public Severity severity;
            public string message;
        }

        public ErrorLogger() { }

        public void Log(Severity sev, string message)
        {
            log.Add(new Record(sev, message));
        }
        public void Log(Severity sev, string message, int lineNumber, string line)
        {
            log.Add(new Record(sev, string.Format("{0}, line {1}: {2}", message, lineNumber, line)));
        }
        public void Fatal(string message) { Log(Severity.Fatal, message); }
        public void Fatal(string message, int lineNumber, string line) { Log(Severity.Fatal, message, lineNumber, line); }
        public void Error(string message) { Log(Severity.Error, message); }
        public void Error(string message, int lineNumber, string line) { Log(Severity.Error, message, lineNumber, line); }
        public void Warning(string message) { Log(Severity.Warning, message); }
        public void Warning(string message, int lineNumber, string line) { Log(Severity.Warning, message, lineNumber, line); }
        public void Hint(string message) { Log(Severity.Hint, message); }
        public void Hint(string message, int lineNumber, string line) { Log(Severity.Hint, message, lineNumber, line); }

        private List<Record> log = new List<Record>();

        public bool Empty { get { return log.Count == 0; } }
        public int Count { get { return log.Count; } }

        public void Report(TextWriter writer)
        {
            foreach (var record in log)
            {
                writer.WriteLine("{0}: {1}", record.severity.ToString(), record.message);
            }
        }

        public override string ToString()
        {
            using (StringWriter writer = new StringWriter())
            {
                Report(writer);
                return writer.ToString();
            }
        }

        public void Prepend(Severity severity, string message)
        {
            log.Insert(0, new Record(severity, message));
        }
    }


    // Based on:
    // https://visualstudiomagazine.com/Articles/2012/11/01/Priority-Queues-with-C.aspx
    internal class PriorityQueue<T> where T : IComparable<T>
    {
        private List<T> data = new List<T>();

        public PriorityQueue() { }

        public void Add(T item)
        {
            data.Add(item);

            int ci = data.Count - 1; // child index; start at end
            while (ci > 0)
            {
                int pi = (ci - 1) / 2; // parent index
                if (data[ci].CompareTo(data[pi]) >= 0) break; // child item is larger than (or equal) parent so we're done
                T tmp = data[ci]; data[ci] = data[pi]; data[pi] = tmp;
                ci = pi;
            }
        }

        public T Dequeue()
        {
            // assumes pq is not empty; up to calling code
            int li = data.Count - 1; // last index (before removal)
            T frontItem = data[0];   // fetch the front
            data[0] = data[li];
            data.RemoveAt(li);

            --li; // last index (after removal)
            int pi = 0; // parent index. start at front of pq
            while (true)
            {
                int ci = pi * 2 + 1; // left child index of parent
                if (ci > li) break;  // no children so done
                int rc = ci + 1;     // right child
                if (rc <= li && data[rc].CompareTo(data[ci]) < 0) // if there is a rc (ci + 1), and it is smaller than left child, use the rc instead
                    ci = rc;
                if (data[pi].CompareTo(data[ci]) <= 0) break; // parent is smaller than (or equal to) smallest child so done
                T tmp = data[pi]; data[pi] = data[ci]; data[ci] = tmp; // swap parent and child
                pi = ci;
            }

            return frontItem;
        }

        public int Count { get { return data.Count; } }
    }

    [Serializable]
    public class ParseException : Exception
    {
        public ParseException() : base("Parse error") { }
        public ParseException(string message) : base(message) { }
        public ParseException(string message, Exception innerException) : base(message, innerException) { }
        protected ParseException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
