using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Caching;
using System.Xml.Serialization;

namespace Maps
{
    internal class LRUCache
    {
        public LRUCache(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException("size", "size must be > 0");
            m_size = size;
        }

        public object this[string key]
        {
            get
            {
                int index = m_keys.FindIndex(delegate(string s) { return s == key; });
                if (index == -1)
                    return null;
                
                if (index == 0)
                    return m_values[0];
                
                string k = m_keys[index];
                object v = m_values[index];
                m_keys.RemoveAt(index);
                m_values.RemoveAt(index);
                m_keys.Insert(0, k);
                m_values.Insert(0, v);
                return v;
            }

            set
            {
                m_keys.Insert(0, key);
                m_values.Insert(0, value);
                while (m_keys.Count > m_size)
                {
                    m_keys.RemoveAt(m_size);
                }
                while (m_values.Count > m_size)
                {
                    m_values.RemoveAt(m_size);
                }
            }
        }

        public void Clear()
        {
            m_keys = new List<string>();
            m_values = new List<object>();
        }

        public int Count { get { return m_keys.Count; } }

        public List<string>.Enumerator GetEnumerator()
        {
            return m_keys.GetEnumerator();
        }

        private int m_size;
        private List<string> m_keys = new List<string>();
        private List<object> m_values = new List<object>();
    }

    internal interface IDeserializable
    {
        void Deserialize(Stream stream, string mediaType, ErrorLogger errors = null);
    }

    internal class ResourceManager
    {
        private HttpServerUtility m_serverUtility;
        public HttpServerUtility Server { get { return m_serverUtility; } }

        private LRUCache m_cache = new LRUCache(50);

        // TODO: Quick Fix - clean this up later
        //private Cache m_cache;
        public LRUCache Cache { get { return m_cache; } }

        public ResourceManager(HttpServerUtility serverUtility)
        {
            m_serverUtility = serverUtility;
        }

        public object GetXmlFileObject(string name, Type type, bool cache = true)
        {
            if (!cache)
            {
                using (var stream = new FileStream(Server.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    XmlSerializer xs = new XmlSerializer(type);
                    object o = xs.Deserialize(stream);
                    if (o.GetType() != type)
                        throw new InvalidOperationException();
                    return o;
                }
            }

            lock (Cache)
            {
                object o = Cache[name];

                if (o == null)
                {
                    o = GetXmlFileObject(name, type, cache: false);

                    Cache[name] = o;
                }

                return o;
            }
        }

        public object GetDeserializableFileObject(string name, Type type, bool cacheResults, string mediaType)
        {
            object obj = null;

            // PERF: Whole cache is locked while loading a single item. Should use finer granularity
            lock (Cache)
            {
                obj = Cache[name];

                if (obj == null)
                {
                    using (var stream = new FileStream(Server.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ConstructorInfo constructorInfoObj = type.GetConstructor(
                            BindingFlags.Instance | BindingFlags.Public, null,
                            CallingConventions.HasThis, new Type[0], null);

                        if (constructorInfoObj == null)
                            throw new TargetException();

                        obj = constructorInfoObj.Invoke(null);

                        IDeserializable ides = obj as IDeserializable;
                        if (ides == null)
                            throw new TargetException();

                        ides.Deserialize(stream, mediaType);
                    }

                    if (cacheResults)
                        Cache[name] = obj;
                }
            }

            if (obj.GetType() != type)
                throw new InvalidOperationException("Object is of the wrong type.");
            
            return obj;
        }
    }
}
