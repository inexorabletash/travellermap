using Json;
using Maps.Rendering;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Maps
{
    public class Sector : MetadataItem
    {
        public Sector()
        {
            Names = new List<Name>();
        }

        internal Sector(Stream stream, string mediaType, ErrorLogger errors)
            : this()
        {
            WorldCollection wc = new WorldCollection(isUserData: true);
            wc.Deserialize(stream, mediaType, errors);
            foreach (World world in wc)
                world.Sector = this;
            worlds = wc;
        }

        public int X { get { return Location.X; } set { Location = new Point(value, Location.Y); } }
        public int Y { get { return Location.Y; } set { Location = new Point(Location.X, value); } }
        public Point Location { get; set; }

        // TODO: Better name for this, possibly just by cleaning up the data.
        public string CanonicalMilieu
        {
            get
            {
                return DataFile?.Milieu ?? Milieu ?? SectorMap.DEFAULT_MILIEU;
            }
        }

        [XmlAttribute]
        public string Abbreviation {
            get
            {
                if (!string.IsNullOrEmpty(abbreviation))
                    return abbreviation;
                if (!Tags.Contains("OTU") || Names.Count == 0)
                    return null;
                // For OTU sectors, synthesize an abbreviation if not specified.
                string name = Names[0].Text;
                name = name.Replace(" ", "");
                name = Regex.Replace(name, @"[^A-Z]", "x", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
                if (name.Length == 0)
                    return null;
                name = name.SafeSubstring(0, 4);
                name = name.Substring(0, 1).ToString().ToUpperInvariant() + name.Substring(1).ToLowerInvariant();
                abbreviation = name;
                return abbreviation;
            }
            set { abbreviation = value; }
        }
        private string abbreviation;

        [XmlAttribute]
        public string Label { get; set; }

        [XmlElement("Name")]
        public List<Name> Names { get; }

        public string Domain { get; set; }

        public string AlphaQuadrant { get; set; }
        public string BetaQuadrant { get; set; }
        public string GammaQuadrant { get; set; }
        public string DeltaQuadrant { get; set; }

        private MetadataCollection<Subsector> subsectors = new MetadataCollection<Subsector>();
        private MetadataCollection<Route> routes = new MetadataCollection<Route>();
        private MetadataCollection<Label> labels = new MetadataCollection<Label>();
        private MetadataCollection<Border> borders = new MetadataCollection<Border>();
        private MetadataCollection<Allegiance> allegiances = new MetadataCollection<Allegiance>();
        private MetadataCollection<Product> products = new MetadataCollection<Product>();

        [XmlAttribute]
        [DefaultValue(false)]
        public bool Selected { get; set; }

        [XmlElement("Product")]
        public MetadataCollection<Product> Products { get { return products; } }

        public MetadataCollection<Subsector> Subsectors { get { return subsectors; } }
        public MetadataCollection<Border> Borders { get { return borders; } }
        public MetadataCollection<Label> Labels { get { return labels; } }
        public MetadataCollection<Route> Routes { get { return routes; } }
        public MetadataCollection<Allegiance> Allegiances { get { return allegiances; } }

        public string Credits { get; set; }

        public void Merge(Sector metadataSource)
        {
            if (metadataSource == null)
                throw new ArgumentNullException("metadataSource");

            // TODO: This is very fragile; if a new type is added to Sector we need to add more code here.

            if (metadataSource.Names.Any()) { Names.Clear(); Names.AddRange(metadataSource.Names); }

            if (metadataSource.DataFile != null && DataFile != null)
            {
                if (metadataSource.DataFile.FileName != DataFile.FileName)
                    throw new Exception(string.Format("Mismatching DataFile.Name entries for {0}: {1} vs. {2}",
                        this.Names[0].Text, metadataSource.DataFile.FileName, DataFile.FileName));

                if (metadataSource.DataFile.Type != DataFile.Type)
                    throw new Exception(string.Format("Mismatching DataFile.Type entries for {0}", 
                        this.Names[0].Text, metadataSource.DataFile.Type, DataFile.Type));
            }

            if (metadataSource.DataFile != null) DataFile = metadataSource.DataFile;

            Subsectors.AddRange(metadataSource.Subsectors);
            Allegiances.AddRange(metadataSource.Allegiances);
            Borders.AddRange(metadataSource.Borders);
            Routes.AddRange(metadataSource.Routes);
            Labels.AddRange(metadataSource.Labels);
            Credits = metadataSource.Credits;
            Products.AddRange(metadataSource.Products);
            StylesheetText = metadataSource.StylesheetText;
        }

        [XmlAttribute("Tags"), JsonName("Tags")]
        public string TagString
        {
            get { return string.Join(" ", tags); }
            set
            {
                tags.Clear();
                if (string.IsNullOrWhiteSpace(value))
                    return;
                tags.AddRange(value.Split());
            }
        }

        internal OrderedHashSet<string> Tags { get { return tags; } }
        private OrderedHashSet<string> tags = new OrderedHashSet<string>();

        public Allegiance GetAllegianceFromCode(string code)
        {
            // TODO: Consider hashtable
            Allegiance alleg = Allegiances.Where(a => a.T5Code == code).FirstOrDefault();
            return alleg ?? SecondSurvey.GetStockAllegianceFromCode(code);
        }

        /// <summary>
        /// Map allegiances like "Sy" for "Sylean Federation" worlds to "Im"
        /// </summary>
        /// <param name="code">The allegiance code to map, e.g. "Sy"</param>
        /// <returns>The base allegiance code, e.g. "Im", or the original code if none.</returns>
        public string AllegianceCodeToBaseAllegianceCode(string code)
        {
            var alleg = GetAllegianceFromCode(code)?.Base;
            return !string.IsNullOrEmpty(alleg) ? alleg : code;
        }

        public DataFile DataFile { get; set; }

        public string MetadataFile { get; set; }

        public void AdjustRelativePaths(string baseFileName)
        {
            string dir = Path.GetDirectoryName(baseFileName);
            if (DataFile != null)
                DataFile.FileName = Path.Combine(dir, DataFile.FileName).Replace(Path.DirectorySeparatorChar, '/');
            if (MetadataFile != null)
                MetadataFile = Path.Combine(dir, MetadataFile).Replace(Path.DirectorySeparatorChar, '/');
        }

        private WorldCollection worlds;

        public Subsector Subsector(char alpha)
        {
            return Subsectors.Where(ss => ss.Index != null && ss.Index[0] == alpha).FirstOrDefault();
        }

        public Subsector Subsector(int index)
        {
            if (index < 0 || index > 15)
                throw new ArgumentOutOfRangeException("index");

            char alpha = (char)('A' + index);

            return Subsector(alpha);
        }

        public Subsector Subsector(int x, int y)
        {
            if (x < 0 || x > 3)
                throw new ArgumentOutOfRangeException("x");
            if (y < 0 || y > 3)
                throw new ArgumentOutOfRangeException("y");

            return Subsector(x + (y * 4));
        }

        public int SubsectorIndexFor(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return -1;
            Subsector subsector;
            if (label.Length == 1)
            {
                char c = char.ToUpperInvariant(label[0]);
                if (Util.InRange(c, 'A', 'P'))
                    return (int)c - (int)'A';
            }

            subsector = Subsectors.Where(ss => !string.IsNullOrEmpty(ss.Name) && ss.Name.Equals(label, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (subsector == null)
                return -1;
            return subsector.IndexNumber;
        }

        public static int QuadrantIndexFor(string label)
        {
            if (label == null) throw new ArgumentNullException("label");

            switch (label.ToLowerInvariant())
            {
                case "alpha": return 0;
                case "beta": return 1;
                case "gamma": return 2;
                case "delta": return 3;
            }
            return -1;
        }

        internal virtual WorldCollection GetWorlds(ResourceManager resourceManager, bool cacheResults = true)
        {
            lock (this)
            {
                // Have it cached - just return it
                if (worlds != null)
                    return worlds;

                // Can't look it up; failure case
                if (DataFile == null)
                    return null;

                // Otherwise, look it up
                WorldCollection data = resourceManager.GetDeserializableFileObject(DataFile.FileName, typeof(WorldCollection), cacheResults: false, mediaType: DataFile.Type) as WorldCollection;
                foreach (World world in data)
                    world.Sector = this;

                if (cacheResults)
                    worlds = data;

                return data;
            }
        }

        internal void Serialize(ResourceManager resourceManager, TextWriter writer, string mediaType, bool includeMetadata = true, bool includeHeader = true, bool sscoords = false, WorldFilter filter = null)
        {
            WorldCollection worlds = GetWorlds(resourceManager);

            // TODO: less hacky T5 support
            bool isT5 = (mediaType == "TabDelimited" || mediaType == "SecondSurvey");

            if (mediaType == "TabDelimited")
            {
                if (worlds != null)
                    worlds.Serialize(writer, mediaType, includeHeader: includeHeader, filter: filter);
                return;
            }

            if (includeMetadata)
            {
                // Header
                //
                writer.WriteLine("# Generated by https://travellermap.com");
                writer.WriteLine("# " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", DateTimeFormatInfo.InvariantInfo));
                writer.WriteLine();

                writer.WriteLine("# {0}", Names[0]);
                writer.WriteLine("# {0},{1}", X, Y);

                writer.WriteLine();
                foreach (var name in Names)
                {
                    if (name.Lang != null)
                        writer.WriteLine("# Name: {0} ({1})", name.Text, name.Lang);
                    else
                        writer.WriteLine("# Name: {0}", name);
                }

                writer.WriteLine();
                writer.WriteLine("# Milieu: {0}", CanonicalMilieu);

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
                    Subsector ss = Subsector(c);
                    writer.WriteLine("# Subsector {0}: {1}", c, ss?.Name ?? "");
                }
                writer.WriteLine();
            }

            if (worlds == null)
            {
                if (includeMetadata)
                    writer.WriteLine("# No world data available");
                return;
            }

            // Allegiances
            if (includeMetadata)
            {
                // Use codes as present in the data, to match the worlds
                foreach (string code in worlds.AllegianceCodes().OrderBy(s => s))
                {
                    var alleg = GetAllegianceFromCode(code);
                    if (alleg != null)
                        writer.WriteLine("# Alleg: {0}: \"{1}\"", isT5 ? code : SecondSurvey.T5AllegianceCodeToLegacyCode(code), alleg.Name);
                }
                writer.WriteLine();
            }

            // Worlds
            worlds.Serialize(writer, mediaType, includeHeader: includeHeader, sscoords: sscoords, filter: filter);
        }

        // TODO: Move this elsewhere
        internal class ClipPath
        {
            public readonly PointF[] clipPathPoints;
            public readonly byte[] clipPathPointTypes;
            public readonly RectangleF bounds;

            public ClipPath(Sector sector, PathUtil.PathType borderPathType)
            {
                float[] edgex, edgey;
                RenderUtil.HexEdges(borderPathType, out edgex, out edgey);

                IEnumerable<Hex> hexes =
                    Util.Sequence(1, Astrometrics.SectorWidth).Select(x => new Hex((byte)x, 1))
                    .Concat(Util.Sequence(2, Astrometrics.SectorHeight).Select(y => new Hex(Astrometrics.SectorWidth, (byte)y)))
                    .Concat(Util.Sequence(Astrometrics.SectorWidth - 1, 1).Select(x => new Hex((byte)x, Astrometrics.SectorHeight)))
                    .Concat(Util.Sequence(Astrometrics.SectorHeight - 1, 1).Select(y => new Hex(1, (byte)y)));

                Rectangle bounds = sector.Bounds;
                IEnumerable<Point> points = (from hex in hexes select new Point(hex.X + bounds.X, hex.Y + bounds.Y)).ToList();
                PathUtil.ComputeBorderPath(points, edgex, edgey, out clipPathPoints, out clipPathPointTypes);

                PointF min = clipPathPoints[0];
                PointF max = clipPathPoints[0];
                for (int i = 1; i < clipPathPoints.Length; ++i)
                {
                    PointF pt = clipPathPoints[i];
                    if (pt.X < min.X)
                        min.X = pt.X;
                    if (pt.Y < min.Y)
                        min.Y = pt.Y;
                    if (pt.X > max.X)
                        max.X = pt.X;
                    if (pt.Y > max.Y)
                        max.Y = pt.Y;
                }
                this.bounds = new RectangleF(min, new SizeF(max.X - min.X, max.Y - min.Y));
            }
        }

        private ClipPath[] clipPathsCache = new ClipPath[(int)PathUtil.PathType.TypeCount];
        internal ClipPath ComputeClipPath(PathUtil.PathType type)
        {
            lock (this)
            {
                if (clipPathsCache[(int)type] == null)
                    clipPathsCache[(int)type] = new ClipPath(this, type);
            }

            return clipPathsCache[(int)type];
        }

        internal Rectangle Bounds
        {
            get
            {
                return new Rectangle(
                        (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X,
                        (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y,
                        Astrometrics.SectorWidth, Astrometrics.SectorHeight
                    );
            }
        }

        public Rectangle SubsectorBounds(int index)
        {
            return new Rectangle(
                (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X + (Astrometrics.SubsectorWidth * (index % 4)),
                (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y + (Astrometrics.SubsectorHeight * (index / 4)),
                Astrometrics.SubsectorWidth,
                Astrometrics.SubsectorHeight);
        }

        public Rectangle QuadrantBounds(int index)
        {
            return new Rectangle(
                (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X + (Astrometrics.SubsectorWidth * 2 * (index % 2)),
                (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y + (Astrometrics.SubsectorHeight * 2 * (index / 2)),
                Astrometrics.SubsectorWidth * 2,
                Astrometrics.SubsectorHeight * 2);
        }

        internal Point Center
        {
            get
            {
                return Astrometrics.LocationToCoordinates(Location, Astrometrics.SectorCenter);
            }
        }

        public Point SubsectorCenter(int index)
        {
            int ssx = index % 4;
            int ssy = index / 4;
            return Astrometrics.LocationToCoordinates(Location,
                new Hex((byte)(Astrometrics.SubsectorWidth * (2 * ssx + 1) / 2), (byte)(Astrometrics.SubsectorHeight * (2 * ssy + 1) / 2)));
        }

        private static readonly SectorStylesheet s_defaultStyleSheet =
            s_defaultStyleSheet = SectorStylesheet.Parse(new StreamReader(
                Assembly.GetExecutingAssembly().GetManifestResourceStream(@"Maps.res.styles.otu.css")));

        internal SectorStylesheet Stylesheet { get; set; }

        internal SectorStylesheet.StyleResult ApplyStylesheet(string element, string code)
        {
            return (Stylesheet ?? s_defaultStyleSheet).Apply(element, code);
        }

        [XmlElement("Stylesheet"), JsonName("Stylesheet")]
        public string StylesheetText
        {
            get { return stylesheetText; }
            set
            {
                stylesheetText = value;
                if (value != null)
                {
                    Stylesheet = SectorStylesheet.Parse(stylesheetText);
                    Stylesheet.Parent = s_defaultStyleSheet;
                }
            }
        }
        private string stylesheetText;

    }

    internal class Dotmap : Sector
    {
        private Sector basis;
        private WorldCollection worlds = null;

        public Dotmap(Sector basis) {
            this.X = basis.X;
            this.Y = basis.Y;
            this.basis = basis;
        }

        internal override WorldCollection GetWorlds(ResourceManager resourceManager, bool cacheResults = true)
        {
            if (this.worlds != null)
                return this.worlds;

            WorldCollection worlds = basis.GetWorlds(resourceManager, cacheResults);
            if (worlds == null)
                return null;

            WorldCollection dots = new WorldCollection();
            foreach (var world in worlds)
            {
                var dot = new World();
                dot.Hex = world.Hex;
                dot.UWP = "???????-?";
                dot.PBG = "???";
                dot.Allegiance = "??";
                dot.Sector = this;
                dots[dot.X, dot.Y] = dot;
            }

            if (cacheResults)
                this.worlds = dots;

            return dots;
        }
    }

    public class Product : MetadataItem
    {
    }

    public class Name
    {
        public Name() { }
        internal Name(string text = "", string lang = null)
        {
            Text = text;
            Lang = lang;
        }

        [XmlText]
        public string Text { get; set; }

        [XmlAttribute]
        [DefaultValueAttribute("")]
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
            FileName = string.Empty;
            Type = "SEC";
        }

        public override string ToString()
        {
            return FileName;
        }

        [XmlText]
        public string FileName { get; set; }

        [XmlAttribute]
        [DefaultValueAttribute("")]
        public string Type { get; set; }
    }

    public class Subsector : MetadataItem
    {
        public Subsector()
        {
            Name = string.Empty;
            Index = string.Empty;
        }

        [XmlText]
        public string Name { get; set; }

        [XmlAttribute]
        public string Index { get; set; }

        public int IndexNumber
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Index))
                    return -1;
                return (int)Index[0] - (int)'A';
            }
        }
    }

    public sealed class Allegiance : IAllegiance
    {
        public Allegiance() { }
        public Allegiance(string code, string name)
        {
            Name = name;
            T5Code = code;
            LegacyCode = code;
        }
        public Allegiance(string t5code, string legacyCode, string name)
        {
            Name = name;
            T5Code = t5code;
            LegacyCode = legacyCode;
        }
        public Allegiance(string t5code, string legacyCode, string baseCode, string name)
        {
            Name = name;
            T5Code = t5code;
            LegacyCode = legacyCode;
            Base = baseCode;
        }

        [XmlText]
        public string Name { get; set; }

        /// <summary>
        /// The four letter (or, in legacy data, two) code for the allegiance, e.g. "As" for Aslan, "Va" for Vargr,
        /// "Im" for Imperium, and so on.
        /// </summary>
        [XmlAttribute("Code"), JsonName("Code")]
        public string T5Code { get; set; }

        internal string LegacyCode { get { return string.IsNullOrEmpty(legacyCode) ? T5Code : legacyCode; } set { legacyCode = value; } }
        private string legacyCode;

        /// <summary>
        /// The code for the fundamental allegiance type. For example, the various MT-era Rebellion
        /// factions (e.g. Domain of Deneb, "Dd") and cultural regions (Sylean Federation Worlds, "Sy")
        /// have the base code "Im" for Imperium.
        ///
        /// Base codes should be unique across Charted Space, but other allegiance codes may not be.
        ///
        /// This is not the same as e.g. naval/scout bases, but it can be used to more easily distinguish
        //  e.g. Imperial naval bases from Vargr naval bases (e.g. "Im"+"N" vs. "Va"+"N")
        /// </summary>
        [XmlAttribute]
        public string Base { get; set; }

        string IAllegiance.Allegiance { get { return T5Code; } }
    }

    public interface IAllegiance
    {
        string Allegiance { get; }
    }


    public class Border : IAllegiance
    {
        public Border()
        {
            ShowLabel = true;
        }

        internal Border(string path, string color = null) : this()
        {
            PathString = path;
            if (color != null)
                ColorHtml = color;
        }

        [XmlAttribute]
        [DefaultValue(true)]
        public bool ShowLabel { get; set; }

        [XmlAttribute]
        public bool WrapLabel { get; set; }

        internal Color? Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string ColorHtml { get { return Color.HasValue ? ColorTranslator.ToHtml(Color.Value) : null; } set { Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute]
        public string Allegiance { get; set; }

        internal IEnumerable<Hex> Path { get { return path; } }
        private List<Hex> path = new List<Hex>();

        internal Hex LabelPosition { get; set; }

        [XmlAttribute("LabelPosition"), JsonName("LabelPosition")]
        public string LabelPositionHex
        {
            get { return LabelPosition.ToString(); }
            set { LabelPosition = new Hex(value); }
        }

        [XmlAttribute]
        public string Label { get; set; }

        internal LineStyle? Style { get; set; }
        [XmlAttribute("Style"), JsonIgnore]
        public LineStyle _Style { get { return Style.Value; } set { Style = value; } }
        public bool ShouldSerialize_Style() { return Style.HasValue; }


        [XmlText, JsonName("Path")]
        public string PathString
        {
            get
            {
                return string.Join(" ", from hex in path select hex.ToString());
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");

                string[] hexes = value.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                path = (from hex in hexes select new Hex(hex)).ToList();

                // Compute the "bounding box" (in hex space)
                Hex min = new Hex(
                    (from hex in path select hex.X).Min(),
                    (from hex in path select hex.Y).Min());
                Hex max = new Hex(
                    (from hex in path select hex.X).Max(),
                    (from hex in path select hex.Y).Max());

                // If no position was set, use the center of the "bounding box"
                if (LabelPosition.IsEmpty)
                    LabelPosition = new Hex((byte)((min.X + max.X + 1) / 2), (byte)((min.Y + max.Y + 1) / 2)); // "+ 1" to round up

                Extends = !min.IsValid || !max.IsValid;
            }
        }

        internal bool Extends { get; set; }

        private BorderPath[] borderPathsCache = new BorderPath[(int)PathUtil.PathType.TypeCount];
        internal BorderPath ComputeGraphicsPath(Sector sector, PathUtil.PathType type)
        {
            lock (this)
            {
                if (borderPathsCache[(int)type] == null)
                    borderPathsCache[(int)type] = new BorderPath(this, sector, type);
            }

            return borderPathsCache[(int)type];
        }

        public string GetLabel(Sector sector)
        {
            if (!ShowLabel)
                return null;
            if (!string.IsNullOrEmpty(Label))
                return Label;
            Allegiance alleg = sector.GetAllegianceFromCode(Allegiance);
            if (alleg == null)
                return null;
            return alleg.Name;
        }
    }
    
    public enum LineStyle
    {
        Solid = 0, // Default
        Dashed,
        Dotted,
        None
    }

    public class Route : IAllegiance
    {
        public Route()
        {
        }

        internal Route(Point? startOffset = null, int start = 0, Point? endOffset = null, int end = 0, string color = null)
            : this()
        {
            StartOffset = startOffset ?? Point.Empty;
            Start = new Hex(start);
            EndOffset = endOffset ?? Point.Empty;
            End = new Hex(end);
            if (color != null)
                ColorHtml = color;
        }

        internal Hex Start { get; set; }
        internal Hex End { get; set; }

        [XmlAttribute("Start"), JsonName("Start")]
        public string StartHex
        {
            get { return Start.ToString(); }
            set { Start = new Hex(value); }
        }

        [XmlAttribute("End"), JsonName("End")]
        public string EndHex
        {
            get { return End.ToString(); }
            set { End = new Hex(value); }
        }

        private sbyte startOffsetX;
        private sbyte startOffsetY;
        private sbyte endOffsetX;
        private sbyte endOffsetY;

        internal Point StartOffset {
            get { return new Point(startOffsetX, startOffsetY); }
            set { startOffsetX = (sbyte)value.X; startOffsetY = (sbyte)value.Y; }
        }

        internal Point EndOffset
        {
            get { return new Point(endOffsetX, endOffsetY); }
            set { endOffsetX = (sbyte)value.X; endOffsetY = (sbyte)value.Y; }
        }

        [XmlAttribute("StartOffsetX")]
        [DefaultValueAttribute(0)]
        public int StartOffsetX { get { return startOffsetX; } set { startOffsetX = (sbyte)value; } }

        [XmlAttribute("StartOffsetY")]
        [DefaultValueAttribute(0)]
        public int StartOffsetY { get { return startOffsetY; } set { startOffsetY = (sbyte)value; } }

        [XmlAttribute("EndOffsetX")]
        [DefaultValueAttribute(0)]
        public int EndOffsetX { get { return endOffsetX; } set { endOffsetX = (sbyte)value; } }

        [XmlAttribute("EndOffsetY")]
        [DefaultValueAttribute(0)]
        public int EndOffsetY { get { return endOffsetY; } set { endOffsetY = (sbyte)value; } }


        internal LineStyle? Style { get; set; }
        [XmlAttribute("Style"), JsonIgnore]
        public LineStyle _Style { get { return Style.Value; } set { Style = value; } }
        public bool ShouldSerialize_Style() { return Style.HasValue; }

        internal float? Width { get; set; }
        [XmlAttribute("Width"), JsonIgnore]
        public float _Width { get { return Width.Value; } set { Width = value; } }
        public bool ShouldSerialize_Width() { return Width.HasValue; }

        internal Color? Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string ColorHtml { get { return Color.HasValue ? ColorTranslator.ToHtml(Color.Value) : null; } set { Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute]
        public string Allegiance { get; set; }

        [XmlAttribute]
        public string Type { get; set; }

        public override string ToString()
        {
            var s = "";

            if (StartOffsetX != 0 || StartOffsetY != 0)
            {
                s += StartOffsetX.ToString(CultureInfo.InvariantCulture);
                s += " ";
                s += StartOffsetY.ToString(CultureInfo.InvariantCulture);
                s += " ";
            }
            s += StartHex;
            s += " ";
            if (EndOffsetX != 0 || EndOffsetY != 0)
            {
                s += EndOffsetX.ToString(CultureInfo.InvariantCulture);
                s += " ";
                s += EndOffsetY.ToString(CultureInfo.InvariantCulture);
                s += " ";
            }
            s += EndHex;
            return s;
        }
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

        internal Color Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string ColorHtml { get { return ColorTranslator.ToHtml(Color); } set { Color = ColorTranslator.FromHtml(value); } }

        [XmlAttribute]
        public string Size { get; set; }

        [XmlAttribute]
        public float OffsetY { get; set; }

        [XmlAttribute]
        // TODO: Unused
        public string RenderType { get; set; }

        [XmlText]
        public string Text { get; set; }
    }
}
