#nullable enable
using Json;
using Maps.Rendering;
using Maps.Serialization;
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Maps
{
    public class Sector : MetadataItem
    {
        public Sector()
        {
        }

        internal Sector(Stream stream, string mediaType, ErrorLogger? errors)
            : this()
        {
            WorldCollection wc = new WorldCollection(isUserData: true);
            wc.Deserialize(stream, mediaType, errors);
            foreach (World world in wc)
                world.Sector = this;
            worlds = wc;
        }

        public int X { get => Location.X; set => Location = new Point(value, Location.Y); }
        public int Y { get => Location.Y; set => Location = new Point(Location.X, value); }
        public Point Location { get; set; }

        // TODO: Better name for this, possibly just by cleaning up the data.
        public string CanonicalMilieu => DataFile?.Milieu ?? Milieu ?? SectorMap.DEFAULT_MILIEU;

        [XmlAttribute]
        public string? Abbreviation { get; set; }

        // For OTU sectors, synthesize an abbreviation if not specified.
        public string? SynthesizeAbbreviation()
        {
            if (!Tags.Contains("OTU") || Names.Count == 0)
                return null;

            string name = Names[0].Text ?? "";
            name = name.Replace(" ", "");
            name = Regex.Replace(name, @"[^A-Z]", "x", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            if (name.Length == 0)
                return null;
            name = name.SafeSubstring(0, 4);
            name = name.Substring(0, 1).ToString().ToUpperInvariant() + name.Substring(1).ToLowerInvariant();
            return name;
        }

        [XmlAttribute]
        public string? Label { get; set; }

        [XmlElement("Name")]
        public List<Name> Names { get; } = new List<Name>();

        public string? Domain { get; set; }

        public string? AlphaQuadrant { get; set; }
        public string? BetaQuadrant { get; set; }
        public string? GammaQuadrant { get; set; }
        public string? DeltaQuadrant { get; set; }


        [XmlAttribute]
        [DefaultValue(false)]
        public bool Selected { get; set; }

        [XmlElement("Product")]
        public MetadataCollection<Product> Products { get; private set; } = new MetadataCollection<Product>();

        public MetadataCollection<Subsector> Subsectors { get; private set; } = new MetadataCollection<Subsector>();
        public MetadataCollection<Border> Borders { get; private set; } = new MetadataCollection<Border>();
        public MetadataCollection<Region> Regions { get; private set; } = new MetadataCollection<Region>();
        public MetadataCollection<Label> Labels { get; private set; } = new MetadataCollection<Label>();
        public MetadataCollection<Route> Routes { get; private set; } = new MetadataCollection<Route>();
        public MetadataCollection<Allegiance> Allegiances { get; private set; } = new MetadataCollection<Allegiance>();

        public IEnumerable<Border> BordersAndRegions => Borders.Concat(Regions);

        public string? Credits { get; set; }

        public void Merge(Sector metadataSource)
        {
            if (metadataSource == null)
                throw new ArgumentNullException(nameof(metadataSource));

            string?[] milieux = { metadataSource.Milieu, metadataSource.DataFile?.Milieu, Milieu, DataFile?.Milieu };
            if (milieux.Where(s => s != null).Distinct().Count() > 1)
            {
                throw new Exception($"Mismatching Milieu entries for {Names[0].Text}: {milieux.Where(s => s != null)}");
            }

            // TODO: This is very fragile; if a new type is added to Sector we need to add more code here.

            if (metadataSource.Names.Any()) { Names.Clear(); Names.AddRange(metadataSource.Names); }

            if (metadataSource.DataFile != null && DataFile != null)
            {
                if (metadataSource.DataFile.FileName != DataFile.FileName)
                    throw new Exception($"Mismatching DataFile.Name entries for {Names[0].Text}: {metadataSource.DataFile.FileName} vs. {DataFile.FileName}");

                if (metadataSource.DataFile.Type != DataFile.Type)
                    throw new Exception($"Mismatching DataFile.Type entries for {Names[0].Text}: {metadataSource.DataFile.Type} vs. {DataFile.Type}");
            }

            if (metadataSource.DataFile != null) DataFile = metadataSource.DataFile;

            Subsectors.AddRange(metadataSource.Subsectors);
            Allegiances.AddRange(metadataSource.Allegiances);
            Borders.AddRange(metadataSource.Borders);
            Regions.AddRange(metadataSource.Regions);
            Routes.AddRange(metadataSource.Routes);
            Labels.AddRange(metadataSource.Labels);
            Credits = metadataSource.Credits;
            Products.AddRange(metadataSource.Products);
            StylesheetText = metadataSource.StylesheetText;

            Tags.AddRange(metadataSource.Tags);
        }

        [XmlAttribute("Tags"), JsonName("Tags")]
        public string TagString
        {
            get => string.Join(" ", Tags); set
            {
                Tags.Clear();
                if (string.IsNullOrWhiteSpace(value))
                    return;
                Tags.AddRange(value.Split());
            }
        }

        internal OrderedHashSet<string> Tags { get; } = new OrderedHashSet<string>();

        public Allegiance? GetAllegianceFromCode(string code)
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
        public string? AllegianceCodeToBaseAllegianceCode(string code)
        {
            var alleg = GetAllegianceFromCode(code)?.Base;
            return !string.IsNullOrEmpty(alleg) ? alleg : code;
        }

        public DataFile? DataFile { get; set; }

        public string? MetadataFile { get; set; }

        public void AdjustRelativePaths(string baseFileName)
        {
            string dir = Path.GetDirectoryName(baseFileName);
            if (DataFile != null)
                DataFile.FileName = Path.Combine(dir, DataFile.FileName).Replace(Path.DirectorySeparatorChar, '/');
            if (MetadataFile != null)
                MetadataFile = Path.Combine(dir, MetadataFile).Replace(Path.DirectorySeparatorChar, '/');
        }

        public Subsector Subsector(char alpha) => Subsectors.Where(ss => ss.Index != null && ss.Index[0] == alpha).FirstOrDefault();

        public Subsector Subsector(int index)
        {
            if (index < 0 || index > 15)
                throw new ArgumentOutOfRangeException(nameof(index));

            char alpha = (char)('A' + index);

            return Subsector(alpha);
        }

        public Subsector Subsector(int x, int y)
        {
            if (x < 0 || x > 3)
                throw new ArgumentOutOfRangeException(nameof(x));
            if (y < 0 || y > 3)
                throw new ArgumentOutOfRangeException(nameof(y));

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
                if (c.InRange('A', 'P'))
                    return (int)c - (int)'A';
            }

            subsector = Subsectors.Where(ss => !string.IsNullOrEmpty(ss.Name) && ss.Name.Equals(label, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            return subsector?.IndexNumber ?? -1;
        }

        public static int QuadrantIndexFor(string label)
        {
            if (label == null) throw new ArgumentNullException(nameof(label));

            return label.ToLowerInvariant() switch
            {
                "alpha" => 0,
                "beta" => 1,
                "gamma" => 2,
                "delta" => 3,
                _ => -1,
            };
        }

        private WorldCollection? worlds;
        internal virtual WorldCollection? GetWorlds(ResourceManager resourceManager, bool cacheResults = true)
        {
            lock (this)
            {
                // Have it cached - just return it
                if (worlds != null)
                    return worlds;

                WorldCollection? data = null;

                // Do we have data?
                if (DataFile != null)
                {
                    // Yes, load/parse it.
                    data = resourceManager.GetDeserializableFileObject(DataFile.FileName, typeof(WorldCollection), cacheResults: false, mediaType: DataFile.Type) as WorldCollection;
                }
                else if (Milieu != null && Milieu != SectorMap.DEFAULT_MILIEU)
                {
                    // Nope... maybe we can construct a dotmap from the default milieu?
                    SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, SectorMap.DEFAULT_MILIEU);
                    Sector? basis = map.FromLocation(this.Location);
                    if (basis == null)
                        return null;

                    WorldCollection? worlds = basis.GetWorlds(resourceManager, cacheResults);
                    if (worlds == null)
                        return null;

                    data = worlds.MakeDotmap();
                }

                if (data == null)
                    return null;

                foreach (World world in data)
                    world.Sector = this;

                if (cacheResults)
                    worlds = data;

                return data;
            }
        }

        internal void Serialize(ResourceManager resourceManager, TextWriter writer, string? mediaType, SectorSerializeOptions options)
        {
            WorldCollection? worlds = GetWorlds(resourceManager);

            // TODO: less hacky T5 support
            bool isT5 = (mediaType == "TabDelimited" || mediaType == "SecondSurvey");

            if (mediaType == "TabDelimited")
            {
                worlds?.Serialize(writer, mediaType, options);
                return;
            }

            if (options.includeMetadata)
            {
                // Header
                //
                writer.WriteLine("# Generated by https://travellermap.com");
                writer.WriteLine("# " + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", DateTimeFormatInfo.InvariantInfo));
                writer.WriteLine();

                writer.WriteLine($"# {Names[0]}");
                writer.WriteLine($"# {X},{-Y}");

                writer.WriteLine();
                foreach (var name in Names)
                {
                    if (name.Lang != null)
                        writer.WriteLine($"# Name: {name.Text} ({name.Lang})");
                    else
                        writer.WriteLine($"# Name: {name}");
                }

                if (Abbreviation != null)
                {
                    writer.WriteLine();
                    writer.WriteLine($"# Abbreviation: {Abbreviation}");
                }

                writer.WriteLine();
                writer.WriteLine($"# Milieu: {CanonicalMilieu}");

                if (Credits != null)
                {
                    string stripped = Regex.Replace(Credits, "<.*?>", "");
                    stripped = Regex.Replace(stripped, @"\s+", " ");
                    stripped = stripped.Trim();
                    writer.WriteLine();
                    writer.WriteLine($"# Credits: {stripped}");
                }

                if (DataFile != null)
                {
                    writer.WriteLine();
                    if (DataFile.Author != null) { writer.WriteLine($"# Author:    {DataFile.Author}"); }
                    if (DataFile.Publisher != null) { writer.WriteLine($"# Publisher: {DataFile.Publisher}"); }
                    if (DataFile.Copyright != null) { writer.WriteLine($"# Copyright: {DataFile.Copyright}"); }
                    if (DataFile.Source != null) { writer.WriteLine($"# Source:    {DataFile.Source}"); }
                    if (DataFile.Ref != null) { writer.WriteLine($"# Ref:       {DataFile.Ref}"); }
                }

                writer.WriteLine();
                for (int i = 0; i < 16; ++i)
                {
                    char c = (char)('A' + i);
                    Subsector ss = Subsector(c);
                    writer.WriteLine($"# Subsector {c}: {ss?.Name ?? ""}");
                }
                writer.WriteLine();
            }

            if (worlds == null)
            {
                if (options.includeMetadata)
                    writer.WriteLine("# No world data available");
                return;
            }

            // Allegiances
            if (options.includeMetadata)
            {
                // Use codes as present in the data, to match the worlds
                foreach (string code in worlds.AllegianceCodes().OrderBy(s => s))
                {
                    var alleg = GetAllegianceFromCode(code);
                    if (alleg != null)
                    {
                        var a = isT5 ? code : SecondSurvey.T5AllegianceCodeToLegacyCode(code);
                        writer.WriteLine($"# Alleg: {a}: \"{alleg.Name}\"");
                    }
                }
                writer.WriteLine();
            }

            // Worlds
            worlds.Serialize(writer, mediaType, options);
        }

        // TODO: Move this elsewhere
        internal class ClipPath
        {
            public readonly PointF[] clipPathPoints;
            public readonly byte[] clipPathPointTypes;
            public readonly RectangleF bounds;

            public ClipPath(Sector sector, PathUtil.PathType borderPathType)
            {
                RenderUtil.HexEdges(borderPathType, out float[] edgex, out float[] edgey);

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
                return clipPathsCache[(int)type];
            }
        }

        internal Rectangle Bounds => new Rectangle(
                        (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X,
                        (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y,
                        Astrometrics.SectorWidth, Astrometrics.SectorHeight
                    );

        public Rectangle SubsectorBounds(int index) => new Rectangle(
                (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X + (Astrometrics.SubsectorWidth * (index % 4)),
                (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y + (Astrometrics.SubsectorHeight * (index / 4)),
                Astrometrics.SubsectorWidth,
                Astrometrics.SubsectorHeight);

        public Rectangle QuadrantBounds(int index) => new Rectangle(
                (Location.X * Astrometrics.SectorWidth) - Astrometrics.ReferenceHex.X + (Astrometrics.SubsectorWidth * 2 * (index % 2)),
                (Location.Y * Astrometrics.SectorHeight) - Astrometrics.ReferenceHex.Y + (Astrometrics.SubsectorHeight * 2 * (index / 2)),
                Astrometrics.SubsectorWidth * 2,
                Astrometrics.SubsectorHeight * 2);

        internal Point Center => Astrometrics.LocationToCoordinates(Location, Astrometrics.SectorCenter);

        public Point SubsectorCenter(int index)
        {
            int ssx = index % 4;
            int ssy = index / 4;
            return Astrometrics.LocationToCoordinates(Location,
                new Hex((byte)(Astrometrics.SubsectorWidth * (2 * ssx + 1) / 2), (byte)(Astrometrics.SubsectorHeight * (2 * ssy + 1) / 2)));
        }

        private static readonly SectorStylesheet s_defaultStyleSheet =
            s_defaultStyleSheet = SectorStylesheet.Parse(
                File.OpenText(System.Web.Hosting.HostingEnvironment.MapPath("~/res/styles/otu.css")));

        internal SectorStylesheet? Stylesheet { get; set; }

        internal SectorStylesheet.StyleResult ApplyStylesheet(string element, string? code)
            => (Stylesheet ?? s_defaultStyleSheet).Apply(element, code);

        [XmlElement("Stylesheet"), JsonName("Stylesheet")]
        public string? StylesheetText
        {
            get => stylesheetText;
            set
            {
                stylesheetText = value;
                if (value != null)
                {
                    Stylesheet = SectorStylesheet.Parse(value);
                    Stylesheet.Parent = s_defaultStyleSheet;
                }
            }
        }
        private string? stylesheetText;

        internal SectorMap.MilieuMap? MilieuMap { get; set; }

        public IEnumerable<string> RoutesForWorld(World world)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));

            Location loc = new Location(Location, new Hex(world.X, world.Y));

            // Collect adjacent sectors
            List<Sector> sectors = new List<Sector>();
            if (MilieuMap == null)
            {
                sectors.Add(this);
            }
            else
            {
                for (int x = X - 1; x <= X + 1; ++x)
                {
                    for (int y = Y - 1; y <= Y + 1; ++y)
                    {
                        var pt = new Point(x, y);
                        Sector sector = MilieuMap.FromLocation(pt);
                        if (pt == Location && sector != this)
                            throw new ApplicationException("Sector lookup did not find itself");
                        if (sector != null)
                            sectors.Add(sector);
                    }
                }
            }

            // Collect routes linking to world
            HashSet<string> routes = new HashSet<string>();
            foreach (Sector sector in sectors)
            {
                foreach (Route route in sector.Routes)
                {
                    sector.RouteToStartEnd(route, out Location start, out Location end);
                    if (start == end)
                        continue;

                    if (end == loc)
                        (start, end) = (end, start);
                    else if (start != loc)
                        continue;

                    string prefix =
                        (string.IsNullOrWhiteSpace(route.Type) || (route.Type!.ToLowerInvariant()) == "xboat") ? "Xb" : "Tr";

                    string s;
                    if (end.Sector == Location)
                    {
                        s = $"{prefix}:{end.Hex}";
                    }
                    else
                    {
                        Sector? endSector = MilieuMap?.FromLocation(end.Sector);
                        // Dangling route into non-detailed sector.
                        if (endSector == null)
                            continue;
                        s = $"{prefix}:{endSector.Abbreviation}-{end.Hex}";
                    }
                    routes.Add(s);
                }
            }
            return routes;
        }

        public void RouteToStartEnd(Route route, out Location start, out Location end)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            Point startSector = Location, endSector = Location;
            startSector.Offset(route.StartOffset);
            endSector.Offset(route.EndOffset);

            start = new Location(startSector, route.Start);
            end = new Location(endSector, route.End);
        }
    }

    internal class Dotmap : Sector
    {
        private Sector basis;
        private WorldCollection? worlds = null;

        public Dotmap(Sector basis)
        {
            X = basis.X;
            Y = basis.Y;
            this.basis = basis;
        }

        internal override WorldCollection? GetWorlds(ResourceManager resourceManager, bool cacheResults = true)
        {
            lock (this)
            {
                if (this.worlds != null)
                    return this.worlds;

                WorldCollection? worlds = basis.GetWorlds(resourceManager, cacheResults);
                if (worlds == null)
                    return null;

                WorldCollection dots = worlds.MakeDotmap();
                foreach (World world in dots)
                    world.Sector = this;

                if (cacheResults)
                    this.worlds = dots;

                return dots;
            }
        }
    }

    public class Product : MetadataItem
    {
    }

    public class Name
    {
        public Name() { }
        internal Name(string text = "", string? lang = null)
        {
            Text = text;
            Lang = lang;
        }

        [XmlText]
        public string Text { get; set; } = "";

        [XmlAttribute]
        [DefaultValue("")]
        public string? Lang { get; set; }

        [XmlAttribute]
        public string? Source { get; set; }

        public override string ToString() => Text ?? "";
    }

    public class DataFile : MetadataItem
    {
        public override string ToString() => FileName;

        [XmlText]
        public string FileName { get; set; } = string.Empty;

        [XmlAttribute]
        [DefaultValue("")]
        public string Type { get; set; } = "SEC";
    }

    public class Subsector : MetadataItem
    {
        [XmlText]
        public string Name { get; set; } = string.Empty;

        [XmlAttribute]
        public string Index { get; set; } = string.Empty;

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
        public Allegiance(string t5code, string name)
        {
            T5Code = t5code;
            Name = name;
            LegacyCode = t5code;
        }
        public Allegiance(string t5code, string name, string legacyCode, string? baseCode = null, string? location = null)
        {
            T5Code = t5code;
            Name = name;
            LegacyCode = legacyCode;
            Base = baseCode;
            Location = location;
        }

        [XmlText]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The four letter (or, in legacy data, two) code for the allegiance, e.g. "As" for Aslan, "Va" for Vargr,
        /// "Im" for Imperium, and so on.
        /// </summary>
        [XmlAttribute("Code"), JsonName("Code")]
        public string T5Code { get; set; } = string.Empty;

        internal string LegacyCode { get => string.IsNullOrEmpty(legacyCode) ? T5Code : legacyCode!; set => legacyCode = value; }
        private string? legacyCode;

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
        public string? Base { get; set; }

        /// <summary>
        /// A textual summary of sectors or regions in which the allegiance occurs,
        /// from the T5SS master spreadsheets.
        /// </summary>
        [XmlAttribute]
        public string? Location { get; set; }

        string? IAllegiance.Allegiance => T5Code;
    }

    public interface IAllegiance
    {
        string? Allegiance { get; }
    }


    public class Border : IAllegiance
    {
        public Border()
        {
        }

        internal Border(string path, string? color = null) : this()
        {
            PathString = path;
            if (color != null)
                ColorHtml = color;
        }

        [XmlAttribute]
        [DefaultValue(true)]
        public bool ShowLabel { get; set; } = true;

        [XmlAttribute]
        [DefaultValue(false)]
        public bool WrapLabel { get; set; }

        internal Color? Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string? ColorHtml
        {
            get => Color.HasValue ? ColorTranslator.ToHtml(Color.Value) : null;
            set { if (value != null) Color = ColorUtil.ParseColor(value); }
        }

        [XmlAttribute]
        public string? Allegiance { get; set; }

        internal IEnumerable<Hex> Path => path;
        private List<Hex> path = new List<Hex>();

        internal Hex LabelPosition { get; set; }

        [XmlAttribute("LabelPosition"), JsonName("LabelPosition")]
        public string LabelPositionHex
        {
            get => LabelPosition.ToString(); set => LabelPosition = new Hex(value);
        }

        [XmlAttribute]
        [DefaultValue(0f)]
        public float LabelOffsetX { get; set; }

        [XmlAttribute]
        [DefaultValue(0f)]
        public float LabelOffsetY { get; set; }



        [XmlAttribute]
        public string? Label { get; set; }

        internal LineStyle? Style { get; set; }
        [XmlAttribute("Style"), JsonIgnore]
#pragma warning disable IDE1006 // Naming Styles
        public LineStyle _Style { get => Style ?? LineStyle.Solid; set => Style = value; }
#pragma warning restore IDE1006 // Naming Styles
        public bool ShouldSerialize_Style() => Style.HasValue;


        [XmlText, JsonName("Path")]
        public string PathString
        {
            get => string.Join(" ", from hex in path select hex.ToString());
            set
            {
                if (value == null) throw new ArgumentNullException(nameof(value));

                string[] hexes = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                path = (from hex in hexes select new Hex(hex)).ToList();
                if (path.First() != path.Last())
                    path.Append(new Hex(path.First()));

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
            }
        }

        private BorderPath[] borderPathsCache = new BorderPath[(int)PathUtil.PathType.TypeCount];
        internal BorderPath ComputeGraphicsPath(Sector sector, PathUtil.PathType type)
        {
            lock (this)
            {
                if (borderPathsCache[(int)type] == null)
                    borderPathsCache[(int)type] = new BorderPath(this, sector, type);
                return borderPathsCache[(int)type];
            }
        }

        internal string? GetLabel(Sector sector)
        {
            if (!ShowLabel)
                return null;
            if (!string.IsNullOrEmpty(Label))
                return Label;
            if (Allegiance == null)
                return null;
            Allegiance? alleg = sector.GetAllegianceFromCode(Allegiance);
            return alleg?.Name;
        }
    }

    public class Region : Border
    {
        public Region() { }
        internal Region(string path, string? color = null) : base(path, color) { }
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

        internal Route(Point? startOffset = null, int start = 0, Point? endOffset = null, int end = 0, string? color = null)
            : this()
        {
            StartOffset = startOffset ?? Point.Empty;
            Start = new Hex(start);
            EndOffset = endOffset ?? Point.Empty;
            End = new Hex(end);
            if (color != null)
                ColorHtml = color;
        }


        private static void FixHex(ref Hex hex, ref sbyte offsetX, ref sbyte offsetY)
        {
            // Normalize outsector links recorded as 0000...3341, etc.
            if (hex.X < 1)
            {
                hex.X = (byte)(hex.X + Astrometrics.SectorWidth);
                --offsetX;
            }
            else if (hex.X > Astrometrics.SectorWidth)
            {
                hex.X = (byte)(hex.X - Astrometrics.SectorWidth);
                ++offsetX;
            }
            else
            {
                hex.X = hex.X;
            }

            if (hex.Y < 1)
            {
                hex.Y = (byte)(hex.Y + Astrometrics.SectorHeight);
                --offsetY;
            }
            else if (hex.Y > Astrometrics.SectorHeight)
            {
                hex.Y = (byte)(hex.Y - Astrometrics.SectorHeight);
                ++offsetY;
            }
            else
            {
                hex.Y = hex.Y;
            }

        }
        internal Hex Start
        {
            get => start; set { start = value; FixHex(ref start, ref startOffsetX, ref startOffsetY); }
        }
        internal Hex End
        {
            get => end; set { end = value; FixHex(ref end, ref endOffsetX, ref endOffsetY); }
        }

        [XmlAttribute("Start"), JsonName("Start")]
        public string StartHex
        {
            get => Start.ToString(); set => Start = new Hex(value);
        }

        [XmlAttribute("End"), JsonName("End")]
        public string EndHex
        {
            get => End.ToString(); set => End = new Hex(value);
        }

        private Hex start;
        private Hex end;
        private sbyte startOffsetX;
        private sbyte startOffsetY;
        private sbyte endOffsetX;
        private sbyte endOffsetY;

        internal Point StartOffset
        {
            get => new Point(startOffsetX, startOffsetY);
            set { startOffsetX = (sbyte)value.X; startOffsetY = (sbyte)value.Y; }
        }

        internal Point EndOffset
        {
            get => new Point(endOffsetX, endOffsetY);
            set { endOffsetX = (sbyte)value.X; endOffsetY = (sbyte)value.Y; }
        }

        [XmlAttribute("StartOffsetX")]
        [DefaultValue(0)]
        public int StartOffsetX { get => startOffsetX; set => startOffsetX = (sbyte)value; }

        [XmlAttribute("StartOffsetY")]
        [DefaultValue(0)]
        public int StartOffsetY { get => startOffsetY; set => startOffsetY = (sbyte)value; }

        [XmlAttribute("EndOffsetX")]
        [DefaultValue(0)]
        public int EndOffsetX { get => endOffsetX; set => endOffsetX = (sbyte)value; }

        [XmlAttribute("EndOffsetY")]
        [DefaultValue(0)]
        public int EndOffsetY { get => endOffsetY; set => endOffsetY = (sbyte)value; }


        internal LineStyle? Style { get; set; }
        [XmlAttribute("Style"), JsonIgnore]
#pragma warning disable IDE1006 // Naming Styles
        public LineStyle _Style { get => Style ?? LineStyle.Solid; set => Style = value; }
#pragma warning restore IDE1006 // Naming Styles
        public bool ShouldSerialize_Style() => Style.HasValue;

        internal float? Width { get; set; }
        [XmlAttribute("Width"), JsonIgnore]
#pragma warning disable IDE1006 // Naming Styles
        public float _Width { get => Width ?? 0; set => Width = value; }
#pragma warning restore IDE1006 // Naming Styles
        public bool ShouldSerialize_Width() => Width.HasValue;

        internal Color? Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string? ColorHtml
        {
            get => Color.HasValue ? ColorTranslator.ToHtml(Color.Value) : null;
            set { if (value != null) Color = ColorUtil.ParseColor(value); }
        }

        [XmlAttribute]
        public string? Allegiance { get; set; }

        [XmlAttribute]
        public string? Type { get; set; }

        public override string ToString()
        {
            var s = "";

            if (StartOffsetX != 0 || StartOffsetY != 0)
                s += $"{StartOffsetX} ${StartOffsetY} ";
            s += $"{StartHex} ";
            if (EndOffsetX != 0 || EndOffsetY != 0)
                s += $"{EndOffsetX} ${EndOffsetY} ";
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
            Hex = new Hex(hex);
            Text = text;
        }

        public static Color DefaultColor => TravellerColors.Amber;

        internal Hex Hex { get; set; }
        [XmlAttribute("Hex"), JsonName("Hex")]
        public string? HexString { get => Hex.ToString(); set { if (value != null) Hex = new Hex(value); } }

        [XmlAttribute]
        public string? Allegiance { get; set; }

        internal Color? Color { get; set; }
        [XmlAttribute("Color"), JsonName("Color")]
        public string? ColorHtml
        {
            get => Color == null ? null : ColorTranslator.ToHtml(Color.Value);
            set { if (value != null) Color = ColorUtil.ParseColor(value); }
        }

        [XmlAttribute]
        public string? Size { get; set; }

        [XmlAttribute]
        public bool Wrap { get; set; }

        [XmlAttribute]
        [DefaultValue(0f)]
        public float OffsetX { get; set; }

        [XmlAttribute]
        [DefaultValue(0f)]
        public float OffsetY { get; set; }

        [XmlAttribute]
        // TODO: Unused
        public string? RenderType { get; set; }

        [XmlText]
        public string Text { get; set; } = string.Empty;
    }
}
