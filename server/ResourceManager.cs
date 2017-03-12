using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using System.Xml.Serialization;

namespace Maps
{
    internal class LRUCache
    {
        public LRUCache(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), size, "must be > 0");
            this.size = size;
        }

        public object this[string key]
        {
            get
            {
                int index = keys.FindIndex(s => s == key);
                if (index == -1)
                    return null;
                
                if (index == 0)
                    return values[0];
                
                string k = keys[index];
                object v = values[index];
                keys.RemoveAt(index);
                values.RemoveAt(index);
                keys.Insert(0, k);
                values.Insert(0, v);
                return v;
            }

            set
            {
                keys.Insert(0, key);
                values.Insert(0, value);
                while (keys.Count > size)
                {
                    keys.RemoveAt(size);
                }
                while (values.Count > size)
                {
                    values.RemoveAt(size);
                }
            }
        }

        public void Clear()
        {
            keys = new List<string>();
            values = new List<object>();
        }

        public int Count => keys.Count;
        public List<string>.Enumerator GetEnumerator()
        {
            return keys.GetEnumerator();
        }

        private int size;
        private List<string> keys = new List<string>();
        private List<object> values = new List<object>();
    }

    internal interface IDeserializable
    {
        void Deserialize(Stream stream, string mediaType, ErrorLogger errors = null);
    }

    internal class ResourceManager
    {
        public HttpServerUtility Server { get; private set; }
        public LRUCache Cache { get; } = new LRUCache(50);

        public ResourceManager(HttpServerUtility serverUtility)
        {
            Server = serverUtility;
        }

        public object GetXmlFileObject(string name, Type type, bool cache = true)
        {
            if (!cache)
            {
                using (var stream = new FileStream(Server.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    try
                    {
                        object o = new XmlSerializer(type).Deserialize(stream);
                        if (o.GetType() != type)
                            throw new InvalidOperationException();
                        return o;
                    }
                    catch (InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
                    {
                        throw ex.InnerException;
                    }
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
                            CallingConventions.HasThis, new Type[0], null) ??
                            throw new TargetException();

                        obj = constructorInfoObj.Invoke(null);

                        IDeserializable ides = obj as IDeserializable ??
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
