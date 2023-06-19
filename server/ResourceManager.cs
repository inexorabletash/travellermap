#nullable enable
using Maps.Utilities;
using System;
using System.IO;
using System.Reflection;
using System.Web;
using System.Xml.Serialization;

namespace Maps
{
    internal interface IDeserializable
    {
        void Deserialize(Stream stream, string mediaType, ErrorLogger? errors = null);
    }

    internal class ResourceManager
    {
        public HttpServerUtility Server { get; }
        public LRUCache Cache { get; } = new LRUCache(50);

        public ResourceManager(HttpServerUtility serverUtility)
        {
            Server = serverUtility;
        }

        public object GetXmlFileObject(string name, Type type, bool cache = true)
        {
            if (!cache)
            {
                using var stream = new FileStream(Server.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read);
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

            lock (Cache)
            {
                object? o = Cache[name];

                if (o == null)
                {
                    o = GetXmlFileObject(name, type, cache: false);

                    Cache[name] = o;
                }

                return o;
            }
        }

        public T GetDeserializableFileObject<T>(string name, bool cacheResults, string mediaType)
        {
            if (!cacheResults)
            {
                using (var stream = new FileStream(Server.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    ConstructorInfo constructorInfoObj = (typeof(T)).GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public, null,
                        CallingConventions.HasThis, new Type[0], null) ??
                        throw new TargetException();

                    object obj = constructorInfoObj.Invoke(null);

                    IDeserializable ides = obj as IDeserializable ??
                        throw new TargetException();

                    ides.Deserialize(stream, mediaType);

                    if (obj.GetType() != typeof(T))
                        throw new InvalidOperationException("Object is of the wrong type.");

                    return (T)obj;
                }
            }

            // PERF: Whole cache is locked while loading a single item. Should use finer granularity
            lock (Cache)
            {
                object? obj = null;

                obj = Cache[name];

                if (obj == null)
                {
                    obj = GetDeserializableFileObject<T>(name, false, mediaType);
                    Cache[name] = obj;
                }
                if (obj == null)
                    throw new ApplicationException("Unexpected null");

                return (T)obj;
            }
        }
    }
}
