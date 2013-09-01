using Json;
using Maps.Rendering;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Maps
{

#if NOT_YET_IMPLEMENTED
	[XmlRoot( ElementName = "Setting" )]
	public class Setting
	{
        public Setting()
        {
            Rifts = new List<string>();
            Borders = new List<string>();
            Worlds = new List<string>();
            SectorCollections = new List<string>();
        }

		private SectorMap m_map;
		public SectorMap Map { get { return m_map; } }

		public List<string> Rifts { get; set; }
		public List<string> Borders { get; set; }
		public List<string> Worlds { get; set; }
        public List<string> SectorCollections { get; set; }

		public static Setting GetSetting( string name, ResourceManager resourceManager )
		{
			// TODO: Cache these statically
			Setting setting = resourceManager.GetXmlFileObject( String.Format( @"~/res/Setting_{0}.xml", name ), typeof( Setting ), false ) as Setting;

            setting.m_map = null; // new SectorMap( /*setting.m_sectorCollections*/ );

			return setting;
		}
	}
#endif

    public class MapNotInitializedException : Exception
    {
        public MapNotInitializedException()
            :
            base("SectorMap data not initialized")
        {
        }
    }

    public struct SectorMetafileEntry
    {
        public string filename;
        public List<string> tags;
        public SectorMetafileEntry(string filename, List<string> tags)
        {
            this.tags = tags;
            this.filename = filename;
        }
    }

    public class SectorMap
    {
        public const string DefaultSetting = "OTU";

        private static SectorMap s_OTU;

        private SectorCollection m_sectors;
        public IList<Sector> Sectors { get { return m_sectors.Sectors; } }

        private Dictionary<string, Sector> m_nameMap = new Dictionary<string, Sector>(StringComparer.InvariantCultureIgnoreCase);
        // TODO: Add Dictionary<Pair<x,y>, Sector> for FromLocation lookups

        private SectorMap(List<SectorMetafileEntry> metafiles, ResourceManager resourceManager)
        {
            foreach (var metafile in metafiles)
            {
                SectorCollection collection = resourceManager.GetXmlFileObject(metafile.filename, typeof(SectorCollection), cache: false) as SectorCollection;
                foreach (var sector in collection.Sectors) {
                    sector.Tags.AddRange(metafile.tags);
                }
                if (m_sectors == null)
                {
                    m_sectors = collection;
                }
                else
                {
                    m_sectors.Merge(collection);
                }
            }

            m_nameMap.Clear();

            foreach (var sector in m_sectors.Sectors)
            {
                if (sector.MetadataFile != null)
                {
                    Sector metadata = resourceManager.GetXmlFileObject(@"~/res/Sectors/" + sector.MetadataFile, typeof(Sector), cache: false) as Sector;
                    sector.Merge(metadata);
                }

                foreach (var name in sector.Names)
                {
                    try
                    {
                        m_nameMap.Add(name.Text, sector);
                    }
                    catch (ArgumentException)
                    {
                        // If it's already in there, ignore it
                        // FUTURE: Return a list of candidates
                    }
                }

                if (!String.IsNullOrEmpty(sector.Abbreviation) && !m_nameMap.ContainsKey(sector.Abbreviation))
                {
                    m_nameMap.Add(sector.Abbreviation, sector);
                }
            }
        }

        public static SectorMap FromName(string settingName, ResourceManager resourceManager)
          {
            if (settingName != SectorMap.DefaultSetting)
            {
                throw new ArgumentException("Only OTU setting is currently supported.");
            }

            lock (typeof(SectorMap))
            {
                if (s_OTU == null)
                {
                    List<SectorMetafileEntry> files = new List<SectorMetafileEntry>
                    {
                        new SectorMetafileEntry(@"~/res/legend.xml", new List<string> { "meta" } ),
                        new SectorMetafileEntry(@"~/res/sectors.xml", new List<string> { "OTU" } ),
                        new SectorMetafileEntry(@"~/res/ZhodaniCoreRoute.xml", new List<string> { "ZCR" } )
                    };                        

                    s_OTU = new SectorMap(files, resourceManager);
                }
            }

            return s_OTU;
        }

        public static void Flush()
        {
            lock (typeof(SectorMap))
            {
                s_OTU = null;
            }
        }

        public static Sector FromName(string settingName, string sectorName)
        {
            // TODO: Having this method supports deserializing data that refers generically to
            // sectors by name.
            //  * Consider decoupling sector *names* from sector data
            //  * Consider Location (the offender) having a default setting

            if (settingName == null)
            {
                settingName = SectorMap.DefaultSetting;
            }

            if (settingName != SectorMap.DefaultSetting)
            {
                throw new ArgumentException("Only OTU setting is currently supported");
            }

            if (s_OTU == null)
            {
                throw new MapNotInitializedException();
            }

            return s_OTU.FromName(sectorName);
        }

        public Sector FromName(string sectorName)
        {
            if (m_sectors == null || m_nameMap == null)
                throw new MapNotInitializedException();

            Sector sector;
            m_nameMap.TryGetValue(sectorName, out sector); // Using indexer throws exception, this is more performant
            return sector;
        }

        public Sector FromLocation(int x, int y)
        {
            if (m_sectors == null)
                throw new MapNotInitializedException();

            // TODO: If perf is a concern, replace this with an array (or some such).
            return m_sectors.Sectors.Where(sector => sector.X == x && sector.Y == y).FirstOrDefault();
        }
    }

    public class SectorBase : MetadataItem
    {
        public SectorBase()
        {
            this.Names = new List<Name>();
        }

        public SectorBase(SectorBase other)
        {
            // TODO: Deep copy?
            this.Location = other.Location;
            this.Names = other.Names;
            this.Abbreviation = other.Abbreviation;
        }

        public int X { get { return Location.X; } set { Location = new Point(value, Location.Y); } }
        public int Y { get { return Location.Y; } set { Location = new Point(Location.X, value); } }

        [XmlAttribute]
        public string Abbreviation { get; set; }

        [XmlIgnore, JsonIgnore]
        public Point Location { get; set; }

        [XmlElement("Name")]
        public List<Name> Names { get; set; }

        [XmlIgnore, JsonIgnore]
        public string Domain { get; set; }

        [XmlIgnore, JsonIgnore]
        public string AlphaQuadrant { get; set; }
        [XmlIgnore, JsonIgnore]
        public string BetaQuadrant { get; set; }
        [XmlIgnore, JsonIgnore]
        public string GammaQuadrant { get; set; }
        [XmlIgnore, JsonIgnore]
        public string DeltaQuadrant { get; set; }
    }

    public class Sector : SectorBase
    {
        public Sector()
        {
        }

        public Sector(Stream stream, string mediaType)
        {
            WorldCollection wc = new WorldCollection();
            wc.Deserialize(stream, mediaType);
            foreach (World world in wc)
            {
                world.Sector = this;
            }
            m_data = wc;
        }

        private MetadataCollection<Subsector> m_subsectors = new MetadataCollection<Subsector>();
        private MetadataCollection<Route> m_routes = new MetadataCollection<Route>();
        private MetadataCollection<Label> m_labels = new MetadataCollection<Label>();
        private MetadataCollection<Border> m_borders = new MetadataCollection<Border>();
        private MetadataCollection<Allegiance> m_allegiances = new MetadataCollection<Allegiance>();
        private List<Product> m_products = new List<Product>();

        [XmlAttribute]
        [System.ComponentModel.DefaultValue(false)]
        public bool Selected { get; set; }

        [XmlElement("Product")]
        public List<Product> Products { get { return m_products; } }

        public MetadataCollection<Subsector> Subsectors { get { return m_subsectors; } }

        public MetadataCollection<Border> Borders { get { return m_borders; } }

        public MetadataCollection<Label> Labels { get { return m_labels; } }

        public MetadataCollection<Route> Routes { get { return m_routes; } }

        public MetadataCollection<Allegiance> Allegiances { get { return m_allegiances; } }

        public string Credits { get; set; }

        public void Merge(Sector metadataSource)
        {
            if (metadataSource == null)
            {
                throw new ArgumentNullException("metadataSource");
            }

            // TODO: This is very fragile; if a new type is added to Sector we need to add more code here.
            if (metadataSource.Names.Any()) this.Names = metadataSource.Names;

            this.Subsectors.AddRange(metadataSource.Subsectors);
            this.Allegiances.AddRange(metadataSource.Allegiances);
            this.Borders.AddRange(metadataSource.Borders);
            this.Routes.AddRange(metadataSource.Routes);
            this.Labels.AddRange(metadataSource.Labels);
            this.Credits = metadataSource.Credits;
        }


        [XmlAttribute("Tags"), JsonName("Tags")]
        public string TagString
        {
            get { return String.Join(" ", m_tags); }
            set
            {
                m_tags.Clear();
                if (String.IsNullOrWhiteSpace(value))
                    return;
                m_tags.AddRange(value.Split());
            }
        }

        [XmlIgnore, JsonIgnore]
        public List<string> Tags { get { return m_tags; } }
        private List<string> m_tags = new List<string>();

        public Allegiance GetAllegiance(string code)
        {
            // TODO: Consider hashtable
            Allegiance alleg = Allegiances.Where(a => a.Code == code).FirstOrDefault();
            return alleg != null ? alleg : Allegiance.GetStockAllegiance(code);
        }

        /// <summary>
        /// Map allegiances like "Sy" for "Sylean Federation" worlds to "Im"
        /// </summary>
        /// <param name="code">The allegiance code to map, e.g. "Sy"</param>
        /// <returns>The base allegiance code, e.g. "Im", or the original code if none.</returns>
        public string GetBaseAllegianceCode(string code)
        {
            if (m_allegiances == null)
                return code;

            Allegiance alleg = m_allegiances.Where(a => a.Code != null && a.Code == code).FirstOrDefault();
            if (alleg != null && !String.IsNullOrEmpty(alleg.Base))
            {
                return alleg.Base;
            }

            return code;
        }

        public DataFile DataFile { get; set; }

        public string MetadataFile { get; set; }

        private WorldCollection m_data;

        public Subsector this[char alpha]
        {
            get
            {
                return Subsectors.Where(ss => ss.Index != null && ss.Index[0] == alpha).FirstOrDefault();
            }
        }

        public Subsector this[int index]
        {
            get
            {
                if (index < 0 || index > 15)
                    throw new ArgumentOutOfRangeException("index");

                char alpha = (char)('A' + index);

                return this[alpha];
            }
        }

        public Subsector this[int x, int y]
        {
            get
            {
                if (x < 0 || x > 3)
                    throw new ArgumentOutOfRangeException("x");
                if (y < 0 || y > 3)
                    throw new ArgumentOutOfRangeException("y");

                return this[x + (y * 4)];
            }
        }

        public WorldCollection GetWorlds(ResourceManager resourceManager, bool cacheResults = true)
        {
            lock (this)
            {
                // Have it cached - just return it
                if (m_data != null)
                {
                    return m_data;
                }

                // Can't look it up; failure case
                if (DataFile == null)
                {
                    return null;
                }

                // Otherwise, look it up
                WorldCollection data = resourceManager.GetDeserializableFileObject(@"~/res/Sectors/" + DataFile, typeof(WorldCollection), cacheResults: false, mediaType: DataFile.Type) as WorldCollection;
                foreach (World world in data)
                {
                    world.Sector = this;
                }

                if (cacheResults)
                {
                    m_data = data;
                }

                return data;
            }
        }

        public void Serialize(ResourceManager resourceManager, TextWriter writer, string mediaType, bool includeMetadata=true, bool includeHeader=true, WorldFilter filter=null)
        {
            WorldCollection worlds = GetWorlds(resourceManager);

            // TODO: less hacky T5 support
            if (mediaType == "TabDelimited")
            {
                if (worlds != null)
                {
                    worlds.Serialize(writer, mediaType, includeHeader, filter);
                }
                return;
            }

            if (includeMetadata)
            {
                // Header
                //
                writer.WriteLine("# Generated by http://www.travellermap.com");
                writer.WriteLine("# " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", DateTimeFormatInfo.InvariantInfo));
                writer.WriteLine();

                writer.WriteLine("# {0}", this.Names[0]);
                writer.WriteLine("# {0},{1}", this.X, this.Y);

                writer.WriteLine();
                foreach(var name in Names) {
                    if (name.Lang != null)
                    {
                        writer.WriteLine("# Name: {0} ({1})", name.Text, name.Lang);
                    }
                    else
                    {
                        writer.WriteLine("# Name: {0}", name);
                    }
                }

                if (Credits != null)
                {
                    string stripped = Regex.Replace(Credits, "<.*?>", "");
                    stripped = Regex.Replace(stripped, @"\s+", " ");
                    stripped = stripped.Trim();
                    writer.WriteLine();
                    writer.WriteLine("# Credits: {0}", stripped);
                }

                if (DataFile != null)
                {
                    writer.WriteLine();
                    if (DataFile.Era != null) { writer.WriteLine("# Era: {0}", DataFile.Era); }
                    writer.WriteLine();

                    if (DataFile.Author != null) { writer.WriteLine("# Author:    {0}", DataFile.Author); }
                    if (DataFile.Publisher != null) { writer.WriteLine("# Publisher: {0}", DataFile.Publisher); }
                    if (DataFile.Copyright != null) { writer.WriteLine("# Copyright: {0}", DataFile.Copyright); }
                    if (DataFile.Source != null) { writer.WriteLine("# Source:    {0}", DataFile.Source); }
                    if (DataFile.Ref != null) { writer.WriteLine("# Ref:       {0}", DataFile.Ref); }
                }

                writer.WriteLine();
                for (int i = 0; i < 16; ++i)
                {
                    char c = (char)('A' + i);
                    Subsector ss = this[c];
                    writer.WriteLine("# Subsector {0}: {1}", c, (ss != null ? ss.Name : ""));
                }
                writer.WriteLine();
            }

            if (worlds == null)
            {
                if (includeMetadata) {
                    writer.WriteLine("# No world data available");
                }
                return;
            }

            // Allegiances
            if (includeMetadata)
            {
                Dictionary<string, Allegiance> allegiances = new Dictionary<string, Allegiance>();
  
                // TODO: Factor this logic out for MSEC/SectorMetaData serializers to use
                foreach (Allegiance alleg in worlds
                    .Select(world => world.Allegiance)
                    .Where(code => !allegiances.ContainsKey(code))
                    .Select(code => GetAllegiance(code))
                    .Where(alleg => alleg != null)
                    .Distinct()
                    .OrderBy(alleg => alleg.Code))
                {
                    writer.WriteLine("# Alleg: {0}: \"{1}\"", alleg.Code, alleg.Name);
                }
                writer.WriteLine();
            }

            // Worlds
            worlds.Serialize(writer, mediaType, includeHeader, filter);
        }

        // TODO: Move this elsewhere
        public class ClipPath
        {
            public readonly PointF[] clipPathPoints;
            public readonly byte[] clipPathPointTypes;
            public readonly RectangleF bounds;

            public ClipPath(Sector sector, PathUtil.PathType borderPathType)
            {
                float[] edgex, edgey;
                RenderUtil.HexEdges(borderPathType, out edgex, out edgey);

                Rectangle bounds = sector.Bounds;
                List<Point> clip = new List<Point>();

                clip.AddRange(Util.Sequence(1, Astrometrics.SectorWidth).Select(x => new Point(x, 1)));
                clip.AddRange(Util.Sequence(2, Astrometrics.SectorHeight).Select(y => new Point(Astrometrics.SectorWidth, y)));
                clip.AddRange(Util.Sequence(Astrometrics.SectorWidth - 1, 1).Select(x => new Point(x, Astrometrics.SectorHeight)));
                clip.AddRange(Util.Sequence(Astrometrics.SectorHeight - 1, 1).Select(y => new Point(1, y)));

                for (int i = 0; i < clip.Count; ++i)
                {
                    Point pt = clip[i];
                    pt.Offset(bounds.Location);
                    clip[i] = pt;
                }

                PathUtil.ComputeBorderPath(clip, edgex, edgey, out clipPathPoints, out clipPathPointTypes);

                PointF min = clipPathPoints[0];
                PointF max = clipPathPoints[0];
                for (int i = 1; i < clipPathPoints.Length; ++i )
                {
                    PointF pt = clipPathPoints[i];
                    if (pt.X < min.X) { min.X = pt.X; }
                    if (pt.Y < min.Y) { min.Y = pt.Y; }
                    if (pt.X > max.X) { max.X = pt.X; }
                    if (pt.Y > max.Y) { max.Y = pt.Y; }
                }
                this.bounds = new RectangleF(min, new SizeF(max.X - min.X, max.Y - min.Y));
            }
        }

        private ClipPath[] clipPathsCache = new ClipPath[(int)PathUtil.PathType.TypeCount];
        public ClipPath ComputeClipPath(PathUtil.PathType type)
        {
            lock (this)
            {
                if (clipPathsCache[(int)type] == null)
                {
                    clipPathsCache[(int)type] = new ClipPath(this, type);
                }
            }

            return clipPathsCache[(int)type];
        }

        [XmlIgnore, JsonIgnore]
        public Rectangle Bounds
        {
            get
            {
                return new Rectangle(
                        (this.Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X,
                        (this.Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y,
                        Astrometrics.SectorWidth, Astrometrics.SectorHeight
                    );
            }
        }

        public Rectangle SubsectorBounds(int index)
        {
            return new Rectangle(
                (this.Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X + (Astrometrics.SubsectorWidth * (index % 4)),
                (this.Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y + (Astrometrics.SubsectorHeight * (index / 4)),
                Astrometrics.SubsectorWidth,
                Astrometrics.SubsectorHeight);
        }

        [XmlIgnore, JsonIgnore]
        public Point Center
        {
            get
            {
                return Astrometrics.LocationToCoordinates(Location, new Point(Astrometrics.SectorWidth / 2, Astrometrics.SectorHeight / 2));
            }
        }

        public Point SubsectorCenter(int index)
        {
            int ssx = index % 4;
            int ssy = index / 4;
            return Astrometrics.LocationToCoordinates(this.Location,
                new Point(Astrometrics.SubsectorWidth * (2 * ssx + 1) / 2, Astrometrics.SubsectorHeight * (2 * ssy + 1) / 2));
        }
    }

    public class Product : MetadataItem
    {
    }

    public class Name
    {
        public Name() { }
        public Name(string text = "", string lang = null)
        {
            Text = text;
            Lang = lang;
        }

        [XmlText]
        public string Text { get; set; }

        [XmlAttribute]
        [System.ComponentModel.DefaultValueAttribute("")]
        public string Lang { get; set; }

        [XmlAttribute]
        public string Source { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    public class DataFile : MetadataItem
    {
        public DataFile()
        {
            FileName = String.Empty;
            Type = "SEC";
        }

        public override string ToString()
        {
            return FileName;
        }

        [XmlText]
        public string FileName { get; set; }

        [XmlAttribute]
        [System.ComponentModel.DefaultValueAttribute("")]
        public string Type { get; set; }
    }

    public class Subsector : MetadataItem
    {
        public Subsector()
        {
            Name = String.Empty;
            Index = String.Empty;
        }

        [XmlText]
        public string Name { get; set; }

        [XmlAttribute]
        public string Index { get; set; }
    }

    public class Allegiance : IAllegiance
    {
        public Allegiance() { }
        public Allegiance(string code, string name)
        {
            this.Name = name;
            this.Code = code;
        }

        [XmlText]
        public string Name { get; set; }

        /// <summary>
        /// The two letter code for the allegiance, e.g. "As" for Aslan, "Va" for Vargr, 
        /// "Im" for Imperium, and so on.
        /// </summary>
        [XmlAttribute]
        public string Code { get; set; }

        /// <summary>
        /// The code for the fundamental allegiance type. For example, the various MT-era Rebellion 
        /// factions (e.g. Domain of Deneb, "Dd") and cultural regions (Sylean Federation Worlds, "Sy") 
        /// have the base code "Im" for Imperium. 
        /// 
        /// Base codes should be unique across Charted Space, but other allegiance codes may not be.
        /// 
        /// This is not for encoding naval/scout bases.
        /// </summary>
        [XmlAttribute]
        public string Base { get; set; }



        public static Allegiance GetStockAllegiance(string code)
        {
            return s_stockAllegiances.Where(alleg => alleg.Code == code).FirstOrDefault();
        }

        private class AllegianceCollection : List<Allegiance>
        {
            public void Add(string code, string name) { Add(new Allegiance(code, name)); }
        }

        private static List<Allegiance> s_stockAllegiances = InitAllegiances();
        static List<Allegiance> InitAllegiances()
        {
            List<Allegiance> stockAllegiances = new AllegianceCollection {
                { "As", "Aslan Hierate" },
                { "Cs", "Imperial Client State" },
                { "Dr", "Droyne" },
                { "Hv", "Hive Federation" },
                { "Im", "Third Imperium" },
                { "J-", "Julian Protectorate" },
                { "JP", "Julian Protectorate" },
                { "Kk", "The Two Thousand Worlds" },
                { "Na", "Non-Aligned" },
                { "So", "Solomani Confederation" },
                { "Va", "Vargr (Non-Aligned }" },
                { "Zh", "Zhodani Consulate" },
            
                { "A0", "Yerlyaruiwo Tlaukhu Bloc" },
                { "A1", "Khaukeairl Tlaukhu Bloc" },
                { "A2", "Syoisuis Tlaukhu Bloc" },
                { "A3", "Tralyeaeawi Tlaukhu Bloc" },
                { "A4", "Eakhtiyho Tlaukhu Bloc" },
                { "A5", "Hlyueawi/Isoitiyro Tlaukhu Bloc" },
                { "A6", "Uiktawa Tlaukhu Bloc" },
                { "A7", "Ikhtealyo Tlaukhu Bloc" },
                { "A8", "Seieakh Tlaukhu Bloc" },
                { "A9", "Aokhalte Tlaukhu Bloc" }
            };

            return stockAllegiances;
        }

        string IAllegiance.Allegiance { get { return this.Code; } }
    }

    public interface IAllegiance
    {
        string Allegiance { get; }
    }

    public class Border : IAllegiance
    {
        public Border()
        {
            this.Color = Color.Red;
            this.ShowLabel = true;
            this.Path = new int[0];
        }

        public Border(string path, string color = null)
            : this()
        {
            this.PathString = path;
            if (color != null)
                this.ColorHtml = color;
        }

        [XmlAttribute]
        [System.ComponentModel.DefaultValue(true)]
        public bool ShowLabel { get; set; }

        [XmlAttribute]
        public bool WrapLabel { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Color Color { get; set; }

        [XmlAttribute("Color"),JsonName("Color")]
        public string ColorHtml { get { return ColorTranslator.ToHtml(this.Color); } set { this.Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute]
        public string Allegiance { get; set; }

        [XmlIgnore,JsonIgnore]
        public int[] Path { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Point LabelPosition { get; set; }

        [XmlAttribute("LabelPosition"),JsonName("LabelPosition")]
        public int LabelPositionHex
        {
            get { return LabelPosition.X * 100 + LabelPosition.Y; }
            set { LabelPosition = new Point(value / 100, value % 100); }
        }

        [XmlText,JsonName("Path")]
        public string PathString
        {
            get
            {
                string[] hexes = new string[Path.Length];
                for (int i = 0; i < Path.Length; i++)
                {
                    hexes[i] = Path[i].ToString("0000", CultureInfo.InvariantCulture);
                }
                return String.Join(" ", hexes);
            }
            set
            {
                // Track the "bounding box" (in hex space)
                Point min = new Point(99, 99);
                Point max = new Point(0, 0);

                string[] hexes = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                List<int> list = new List<int>(hexes.Length);

                for (int i = 0; i < hexes.Length; i++)
                {
                    int hex;
                    if (Int32.TryParse(hexes[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out hex))
                    {
                        list.Add(hex);

                        int x = hex / 100;
                        int y = hex % 100;

                        if (x < min.X) min.X = x;
                        if (y < min.Y) min.Y = y;

                        if (x > max.X) max.X = x;
                        if (y > max.Y) max.Y = y;
                    }
                }

                Path = list.ToArray();

                // If no position was set, use the center of the "bounding box"
                if (LabelPosition.IsEmpty)
                {
                    LabelPosition = new Point((min.X + max.X + 1) / 2, (min.Y + max.Y + 1) / 2); // "+ 1" to round up
                }

                Extends = (min.X < 1 || min.Y < 1 || max.X > Astrometrics.SectorWidth || max.Y > Astrometrics.SectorHeight);
            }
        }

        [XmlIgnoreAttribute,JsonIgnore]
        public bool Extends { get; set; }

        private BorderPath[] borderPathsCache = new BorderPath[(int)PathUtil.PathType.TypeCount];
        public BorderPath ComputeGraphicsPath(Sector sector, PathUtil.PathType type)
        {
            lock (this)
            {
                if (borderPathsCache[(int)type] == null)
                {
                    borderPathsCache[(int)type] = new BorderPath(this, sector, type);
                }
            }

            return borderPathsCache[(int)type];
        }

    }

    public class Route : IAllegiance
    {
        public enum RouteStyle
        {
            Solid,
            Dashed,
            Dotted
        }

        public static Color DefaultColor { get { return Color.Green; } }
        public static RouteStyle DefaultStyle { get { return RouteStyle.Solid; } }

        public Route()
        {
            Color = DefaultColor;
            Style = DefaultStyle;
            Allegiance = "Im";
        }

        public Route(Point? startOffset = null, int start = 0, Point? endOffset = null, int end = 0, string color = null)
            : this()
        {
            StartOffset = (startOffset == null) ? Point.Empty : (Point)startOffset;
            Start = start;
            EndOffset = (endOffset == null) ? Point.Empty : (Point)endOffset;
            End = end;
            if (color != null)
                ColorHtml = color;
        }

        [XmlAttribute]
        public int Start { get; set; }

        [XmlAttribute]
        public int End { get; set; }

        [XmlAttribute]
        [System.ComponentModel.DefaultValueAttribute(RouteStyle.Solid)]
        public RouteStyle Style { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Color Color { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Point StartOffset { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Point EndOffset { get; set; }

        [XmlAttribute]
        public string Allegiance { get; set; }

        [XmlAttribute("Color"),JsonName("Color")]
        public string ColorHtml { get { return ColorTranslator.ToHtml(Color); } set { Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute("StartOffsetX")]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int StartOffsetX { get { return StartOffset.X; } set { StartOffset = new Point(value, StartOffset.Y); } }

        [XmlAttribute("StartOffsetY")]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int StartOffsetY { get { return StartOffset.Y; } set { StartOffset = new Point(StartOffset.X, value); } }

        [XmlAttribute("EndOffsetX")]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int EndOffsetX { get { return EndOffset.X; } set { EndOffset = new Point(value, EndOffset.Y); } }

        [XmlAttribute("EndOffsetY")]
        [System.ComponentModel.DefaultValueAttribute(0)]
        public int EndOffsetY { get { return EndOffset.Y; } set { EndOffset = new Point(EndOffset.X, value); } }
    }

    public class Label : IAllegiance
    {
        public Label()
        {
            Color = DefaultColor;
        }
        public Label(int hex, string text)
            : this()
        {
            Hex = hex;
            Text = text;
        }

        public static Color DefaultColor { get { return Color.Yellow; } }

        [XmlAttribute]
        public int Hex { get; set; }

        [XmlAttribute]
        public string Allegiance { get; set; }

        [XmlIgnoreAttribute,JsonIgnore]
        public Color Color { get; set; }

        [XmlAttribute("Color"),JsonName("Color")]
        public string ColorHtml { get { return ColorTranslator.ToHtml(Color); } set { Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute]
        public string Size { get; set; }

        [XmlAttribute]
        // TODO: Unused
        public string RenderType { get; set; }

        [XmlText]
        public string Text { get; set; }
    }

    [XmlRoot(ElementName = "Sectors")]
    public class SectorCollection
    {
        [XmlElement("Sector")]
        public List<Sector> Sectors { get; set; }

        public void Merge(SectorCollection otherCollection)
        {
            if (otherCollection == null)
                throw new ArgumentNullException("otherCollection");

            if (Sectors == null)
            {
                Sectors = otherCollection.Sectors;
            }
            else if (otherCollection.Sectors != null)
            {
                Sectors.AddRange(otherCollection.Sectors);
            }
        }
    }

}
