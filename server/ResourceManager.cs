#nullable enable
using Maps.Utilities;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Web.Hosting;
using System.Xml.Serialization;

namespace Maps
{
    internal interface IDeserializable
    {
        void Deserialize(Stream stream, string mediaType, ErrorLogger? errors = null);
    }

    internal class ResourceManager
    {
        // Thread affinity
        private static ThreadLocal<ResourceManager> s_instance = new ThreadLocal<ResourceManager>(() => new ResourceManager());
        
        /// <summary>
        /// Use for caching where thread-affinity is desired.
        /// </summary>
        /// <returns></returns>
        public static ResourceManager GetInstance()
        {
            return s_instance.Value;
        }
        /// <summary>
        /// Use for tasks where caching should expire at the end of the lifetime.
        /// </summary>
        /// <returns></returns>
        public static ResourceManager GetDedicatedInstance()
        {
            return new ResourceManager();
        }

        private LRUCache cache = new LRUCache(50);

        private ResourceManager()
        {
        }

        public static T GetXmlFileObject<T>(string name)
        {
            using var stream = new FileStream(HostingEnvironment.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read);
            try
            {
                object o = new XmlSerializer(typeof(T)).Deserialize(stream);
                if (o.GetType() != typeof(T))
                    throw new ApplicationException($"Invalid file: {name}");
                return (T)o;
            }
            catch (InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                throw ex.InnerException;
            }
        }

        public T GetCachedXmlFileObject<T>(string name)
        {
            object? o = cache[name];

            if (o == null)
            {
                o = GetXmlFileObject<T>(name);
                cache[name] = o;
            }
            if (o == null)
                throw new ApplicationException("Unexpected null");

            return (T)o;
        }

        private static T GetDeserializableFileObject<T>(string name, string mediaType)
        {
            using (var stream = new FileStream(HostingEnvironment.MapPath(name), FileMode.Open, FileAccess.Read, FileShare.Read))
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
                    throw new ApplicationException($"Invalid file: {name}");

                return (T)obj;
            }
        }
        public T GetCachedDeserializableFileObject<T>(string name, string mediaType)
        {
            object? obj = cache[name];

            if (obj == null)
            {
                obj = GetDeserializableFileObject<T>(name, mediaType);
                cache[name] = obj;
            }
            if (obj == null)
                throw new ApplicationException("Unexpected null");

            return (T)obj;
        }
        public void Flush()
        {
            cache.Clear();
        }
    }
}
