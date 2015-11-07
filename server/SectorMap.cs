using System;
using System.Collections.Generic;
using System.Drawing;
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

        private static SectorMap s_OTU;

        private SectorCollection sectors;
        public IList<Sector> Sectors { get { return sectors.Sectors; } }

        private Dictionary<string, Sector> nameMap = new Dictionary<string, Sector>(StringComparer.InvariantCultureIgnoreCase);
        private Dictionary<Point, Sector> locationMap = new Dictionary<Point, Sector>();

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

            nameMap.Clear();
            locationMap.Clear();

            foreach (var sector in sectors.Sectors)
            {
                if (sector.MetadataFile != null)
                {
                    Sector metadata = resourceManager.GetXmlFileObject(@"~/res/Sectors/" + sector.MetadataFile, typeof(Sector), cache: false) as Sector;
                    sector.Merge(metadata);
                }

                locationMap.Add(sector.Location, sector);

                foreach (var name in sector.Names)
                {
                    if (!nameMap.ContainsKey(name.Text))
                        nameMap.Add(name.Text, sector);

                    // Automatically alias "SpinwardMarches"
                    string spaceless = name.Text.Replace(" ", "");
                    if (spaceless != name.Text && !nameMap.ContainsKey(spaceless))
                        nameMap.Add(spaceless, sector);
                }

                if (!string.IsNullOrEmpty(sector.Abbreviation) && !nameMap.ContainsKey(sector.Abbreviation))
                    nameMap.Add(sector.Abbreviation, sector);
            }
        }

        public static SectorMap GetInstance(ResourceManager resourceManager)
        {
            lock (SectorMap.s_lock)
            {
                if (s_OTU == null)
                {
                    List<SectorMetafileEntry> files = new List<SectorMetafileEntry>
                    {
                        new SectorMetafileEntry(@"~/res/legend.xml", new List<string> { "meta" } ),
                        new SectorMetafileEntry(@"~/res/sectors.xml", new List<string> { "OTU" } ),
                        new SectorMetafileEntry(@"~/res/faraway.xml", new List<string> { "Faraway" } ),
                        new SectorMetafileEntry(@"~/res/ZhodaniCoreRoute.xml", new List<string> { "ZCR" } )
                    };

                    s_OTU = new SectorMap(files, resourceManager);
                }
            }

            return s_OTU;
        }

        public static void Flush()
        {
            lock (SectorMap.s_lock)
            {
                s_OTU = null;
            }
        }

        public static SectorMap GetInstance()
        {
            // This method supports deserializing of Location instances that reference sectors by name.
            if (s_OTU == null)
                throw new MapNotInitializedException();
            return s_OTU;
        }

        public Sector FromName(string sectorName)
        {
            if (sectors == null || nameMap == null)
                throw new MapNotInitializedException();

            Sector sector;
            nameMap.TryGetValue(sectorName, out sector); // Using indexer throws exception, this is more performant
            return sector;
        }

        public Sector FromLocation(int x, int y) { return FromLocation(new Point(x, y)); }
        public Sector FromLocation(Point pt)
        {
            if (sectors == null || locationMap == null)
                throw new MapNotInitializedException();

            Sector sector;
            locationMap.TryGetValue(pt, out sector);
            return sector;
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
