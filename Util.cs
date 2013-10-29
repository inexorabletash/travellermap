using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Maps
{
    public static class Util
    {
        public const string MediaTypeName_Image_Png = "image/png";
        public static readonly Encoding UTF8_NO_BOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static string FixCapitalization(string s)
        {
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

        public static Stream ToStream(this string str, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.UTF8;
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
            int c = start, d = (start < end) ? 1 : -1;
            yield return c;
            while (c != end)
            {
                c += d;
                yield return c;
            }
        }

        public static string SafeSubstring(string s, int start, int length)
        {
            if (start > s.Length)
                return string.Empty;
            if (start + length > s.Length)
                return s.Substring(start);
            return s.Substring(start, length);
        }
    }

    // Like Regex, but takes shell-style globs:
    //  - case insensitive by default
    //  - * matches 0-or-more of anything
    //  - ? matches exactly one of anything
    public class Glob : Regex
    {
        public Glob(string pattern, RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.Singleline)
            : base("^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$", options) { }
    }

    public class RegexDictionary<T> : Dictionary<Regex, T>
    {
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

    public class GlobDictionary<T> : RegexDictionary<T>
    {
        public override void Add(string r, T v) { Add(new Glob(r), v); }
        public override void Add(T v) { Add(new Glob(v.ToString()), v); }
    }

    // Don't close the underlying stream when disposed; must be disposed
    // before the underlying stream is.
    public class NoCloseStreamReader : StreamReader
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
    public class NoCloseStreamWriter : StreamWriter
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

    public class ListHashSet<T> : IEnumerable<T>
    {
        private List<T> m_list = new List<T>();
        private HashSet<T> m_set = new HashSet<T>();

        public void Add(T item)
        {
            if (m_set.Contains(item))
                return;
            m_list.Add(item);
            m_set.Add(item);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            foreach (var item in collection)
                Add(item);
        }

        public void Remove(T item)
        {
            if (m_set.Remove(item))
                m_list.Remove(item);
        }

        public bool Contains(T item)
        {
            return m_set.Contains(item);
        }

        public void Clear()
        {
            m_list.Clear();
            m_set.Clear();
        }

        public bool Any(Func<T, bool> predicate)
        {
            return m_set.Any(predicate);
        }

        public IEnumerable<T> Where(Func<T, bool> predicate)
        {
            return m_list.Where(predicate);
        }

        public int Count() { return m_set.Count(); }

        public T this[int index] { get { return m_list[index]; } }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return m_list.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_list.GetEnumerator();
        }
    }
}
