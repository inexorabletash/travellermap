using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Maps
{
    [Serializable]
    public class MapNotInitializedException : Exception
    {
        public MapNotInitializedException() : base("SectorMap data not initialized") { }
        public MapNotInitializedException(string message) : base(message) { }
        public MapNotInitializedException(string message, Exception innerException) : base(message, innerException) { }
        protected MapNotInitializedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }

    internal struct SectorMetafileEntry
    {
        public string filename;
        public List<string> tags;
        public SectorMetafileEntry(string filename, List<string> tags)
        {
            this.tags = tags;
            this.filename = filename;
        }
    }

    internal class SectorMap
    {
        private static object s_lock = new object();

        private static SectorMap s_instance;

        private SectorCollection sectors;
        public IList<Sector> Sectors { get { return sectors.Sectors; } }

        private class MilieuMap
        {
            public Dictionary<string, Sector> nameMap = new Dictionary<string, Sector>(StringComparer.InvariantCultureIgnoreCase);
            public Dictionary<Point, Sector> locationMap = new Dictionary<Point, Sector>();
        }

        private const string DEFAULT_MILIEU = "1105";
        private static readonly IEnumerable<string> FALLBACK_MILIEUX = new List<string> { "1100", "1110", "1000", "1117", "1120", "1200" };

        private Dictionary<string, MilieuMap> milieux = new Dictionary<string, MilieuMap>();

        private SectorMap(List<SectorMetafileEntry> metafiles, ResourceManager resourceManager)
        {
            foreach (var metafile in metafiles)
            {
                SectorCollection collection = resourceManager.GetXmlFileObject(metafile.filename, typeof(SectorCollection), cache: false) as SectorCollection;
                foreach (var sector in collection.Sectors)
                    sector.Tags.AddRange(metafile.tags);

                if (sectors == null)
                    sectors = collection;
                else
                    sectors.Merge(collection);
            }

            milieux.Clear();

            foreach (var sector in sectors.Sectors)
            {
                if (sector.MetadataFile != null)
                {
                    Sector metadata = resourceManager.GetXmlFileObject(@"~/res/Sectors/" + sector.MetadataFile, typeof(Sector), cache: false) as Sector;
                    sector.Merge(metadata);
                }

                string milieu = sector.Milieu ?? sector.DataFile?.Milieu ?? DEFAULT_MILIEU;
                if (!milieux.ContainsKey(milieu))
                    milieux.Add(milieu, new MilieuMap());

                MilieuMap m = milieux[milieu];
                m.locationMap.Add(sector.Location, sector);

                foreach (var name in sector.Names)
                {
                    if (!m.nameMap.ContainsKey(name.Text))
                        m.nameMap.Add(name.Text, sector);

                    // Automatically alias "SpinwardMarches"
                    string spaceless = name.Text.Replace(" ", "");
                    if (spaceless != name.Text && !m.nameMap.ContainsKey(spaceless))
                        m.nameMap.Add(spaceless, sector);
                }

                if (!string.IsNullOrEmpty(sector.Abbreviation) && !m.nameMap.ContainsKey(sector.Abbreviation))
                    m.nameMap.Add(sector.Abbreviation, sector);
            }
        }

        public static SectorMap GetInstance(ResourceManager resourceManager)
        {
            lock (SectorMap.s_lock)
            {
                if (s_instance == null)
                {
                    List<SectorMetafileEntry> files = new List<SectorMetafileEntry>
                    {
                        // Meta
                        new SectorMetafileEntry(@"~/res/legend.xml", new List<string> { "meta" } ),

                        // OTU - Default Milieu
                        new SectorMetafileEntry(@"~/res/sectors.xml", new List<string> { "OTU" } ),
                        new SectorMetafileEntry(@"~/res/Sectors/Zhodani Core Route/ZhodaniCoreRoute.xml", new List<string> { "ZCR" } ),
                        new SectorMetafileEntry(@"~/res/Sectors/Orion OB1/orion.xml", new List<string> { "OrionOB1" } ),

                        // OTU - Other Milieu
                        new SectorMetafileEntry(@"~/res/Sectors/M0/M0.xml", new List<string> {} ),
                        new SectorMetafileEntry(@"~/res/Sectors/M990/M990.xml", new List<string> {} ),

                        // Non-OTU
                        new SectorMetafileEntry(@"~/res/Sectors/Faraway/faraway.xml", new List<string> { "Faraway" } ),
                    };

                    s_instance = new SectorMap(files, resourceManager);
                }
            }

            return s_instance;
        }

        public static void Flush()
        {
            lock (SectorMap.s_lock)
            {
                s_instance = null;
            }
        }

        // This method supports deserializing of Location instances that reference sectors by name.
        // Throws if the map is not initialized.
        public static Point GetSectorCoordinatesByName(string name)
        {
            SectorMap instance;
            lock (SectorMap.s_lock)
            {
                instance = s_instance;
            }
            if (instance == null)
                throw new MapNotInitializedException();
            return instance.FromName(name, null).Location;
        }

        public class Milieu
        {
            private SectorMap map;
            private string milieu;
            public Milieu(SectorMap map, string milieu)
            {
                this.map = map;
                this.milieu = milieu;
            }
            public Sector FromLocation(int x, int y) { return map.FromLocation(x, y, milieu); }
            public Sector FromLocation(Point pt) { return map.FromLocation(pt, milieu); }
            public Sector FromName(string name) { return map.FromName(name, milieu); }
        }

        public static Milieu ForMilieu(ResourceManager resourceManager, string milieu)
        {
            return new Milieu(SectorMap.GetInstance(resourceManager), milieu);
        }

        private IEnumerable<MilieuMap> SelectMilieux(string m)
        {
            if (milieux == null)
                throw new MapNotInitializedException();

            if (m != null)
            {
                if (milieux.ContainsKey(m))
                    yield return milieux[m];
                yield break;
            }

            yield return milieux[DEFAULT_MILIEU];
            foreach (string milieu in FALLBACK_MILIEUX)
            {
                if (milieux.ContainsKey(milieu))
                    yield return milieux[milieu];
            }
        }

        private Sector FromName(string name, string milieu)
        {
            if (sectors == null)
                throw new MapNotInitializedException();
            return SelectMilieux(milieu)
                .Select(m => m.nameMap.ContainsKey(name) ? m.nameMap[name] : null)
                .Where(s => s != null)
                .FirstOrDefault();
        }

        private Sector FromLocation(int x, int y, string milieu) { return FromLocation(new Point(x, y), milieu); }
        private Sector FromLocation(Point pt, string milieu)
        {
            if (sectors == null)
                throw new MapNotInitializedException();
            return SelectMilieux(milieu)
                .Select(m => m.locationMap.ContainsKey(pt) ? m.locationMap[pt] : null)
                .Where(s => s != null)
                .FirstOrDefault();
        }
    }

    [XmlRoot(ElementName = "Sectors")]
    public class SectorCollection
    {
        public SectorCollection()
        {
            Sectors = new List<Sector>();
        }

        [XmlElement("Sector")]
        public List<Sector> Sectors { get; }

        public void Merge(SectorCollection otherCollection)
        {
            if (otherCollection == null)
                throw new ArgumentNullException("otherCollection");

            if (otherCollection.Sectors != null)
                Sectors.AddRange(otherCollection.Sectors);
        }
    }
}
