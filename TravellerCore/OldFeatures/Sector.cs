using System.Text.Json.Serialization;
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
            Worlds = wc;
        }

        public int X { get => Location.X; set => Location = new Point(value, Location.Y); }
        public int Y { get => Location.Y; set => Location = new Point(Location.X, value); }
        public Point Location { get; set; }

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

        public MetadataCollection<Subsector> Subsectors { get; private set; } = new MetadataCollection<Subsector>();
        public MetadataCollection<Border> Borders { get; private set; } = new MetadataCollection<Border>();
        public MetadataCollection<Region> Regions { get; private set; } = new MetadataCollection<Region>();
        public MetadataCollection<Label> Labels { get; private set; } = new MetadataCollection<Label>();
        public MetadataCollection<Route> Routes { get; private set; } = new MetadataCollection<Route>();
        public MetadataCollection<Allegiance> Allegiances { get; private set; } = new MetadataCollection<Allegiance>();

        public IEnumerable<Border> BordersAndRegions => Borders.Concat(Regions);

        public string? Credits { get; set; }


        [XmlAttribute("Tags")]
        [JsonPropertyName("Tags")]
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

        internal WorldCollection? Worlds { get; private set; }

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

        public override string ToString() => Text ?? "";
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
        [XmlAttribute("Code")]
        [JsonPropertyName("Code")]
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

}
