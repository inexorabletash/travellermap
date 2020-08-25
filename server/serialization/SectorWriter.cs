#nullable enable

using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Maps.Serialization
{
    internal class SectorSerializeOptions
    {
        public bool includeMetadata = true;
        public bool includeHeader = true;
        public bool includeRoutes = false;
        public bool sscoords = false;
        public Func<World, bool>? filter = null;
    }

    internal abstract class SectorFileSerializer
    {
        public abstract Encoding Encoding { get; }

        public virtual void Serialize(Stream stream, IEnumerable<World> worlds, SectorSerializeOptions options)
        {
            using (var writer = new StreamWriter(stream, Encoding))
            {
                Serialize(writer, worlds, options);
            }
        }

        public abstract void Serialize(TextWriter writer, IEnumerable<World> worlds, SectorSerializeOptions options);

        public static SectorFileSerializer ForType(string? mediaType) =>
            mediaType switch
            {
                "SecondSurvey" => new SecondSurveySerializer(),
                "TabDelimited" => new TabDelimitedSerializer(),
                "SEC" => new SecSerializer(),
                _ => new SecSerializer(),
            };
    }

    internal class SecSerializer : SectorFileSerializer
    {
        public override Encoding Encoding => Encoding.GetEncoding(1252);
        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, SectorSerializeOptions options)
        {

            if (options.includeHeader)
            {
                foreach (var line in new string[] {
                    " 1-14: Name",
                    "15-18: HexNbr",
                    "20-28: UWP",
                    "   31: Bases",
                    "33-47: Codes & Comments",
                    "   49: Zone",
                    "52-54: PBG",
                    "56-57: Allegiance",
                    "59-74: Stellar Data",
                    "",
                    "....+....1....+....2....+....3....+....4....+....5....+....6....+....7....+....8",
                    ""
                })
                {
                    writer.WriteLine(line);
                }
            }

            const string worldFormat = "{0,-14}{1,4} {2,9}  {3,1} {4,-15} {5,1}  {6,3} {7,2} {8,-15}";

            foreach (World world in worlds.OrderBy(world => world.SS))
            {
                writer.WriteLine(worldFormat,
                    world.Name.Truncate(14),
                    options.sscoords ? world.SubsectorHex : world.Hex,
                    world.UWP,
                    world.LegacyBaseCode,
                    world.Remarks.Truncate(15),
                    world.Zone,
                    world.PBG,
                    world.LegacyAllegiance.Truncate(2),
                    world.Stellar.Truncate(15)
                    );
            }
        }
    }

    internal class SecondSurveySerializer : SectorFileSerializer
    {
        public override Encoding Encoding => Util.UTF8_NO_BOM;
        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, SectorSerializeOptions options)
        {
            List<string> cols = new List<string> {
                "Hex",
                "Name",
                "UWP",
                "Remarks",
                "{Ix}",
                "(Ex)",
                "[Cx]",
                "N",
                "B",
                "Z",
                "PBG",
                "W",
                "A",
                "Stellar"
            };
            if (options.includeRoutes)
                cols.Add("Routes");

            ColumnSerializer formatter = new ColumnSerializer(cols);

            formatter.SetMinimumWidth("Name", 20);
            formatter.SetMinimumWidth("Remarks", 20);

            foreach (World world in worlds.OrderBy(world => world.SS))
            {
                List<string> row = new List<string> {
                    options.sscoords? world.SubsectorHex: world.Hex,
                    world.Name,
                    world.UWP,
                    world.Remarks,
                    world.Importance ?? "",
                    world.Economic ?? "",
                    world.Cultural ?? "",
                    DashIfEmpty(world.Nobility ?? ""),
                    DashIfEmpty(world.Bases),
                    DashIfEmpty(world.Zone),
                    world.PBG,
                    world.Worlds > 0 ? world.Worlds.ToString() : "",
                    world.Allegiance,
                    world.Stellar
                };
                if (options.includeRoutes)
                    row.Add(world.Routes ?? "");
                formatter.AddRow(row); 
            }
            formatter.Serialize(writer, options.includeHeader);
        }

        private static string DashIfEmpty(string s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return "-";
            return s;
        }
    }

    internal class TabDelimitedSerializer : SectorFileSerializer
    {
        public override Encoding Encoding => Util.UTF8_NO_BOM;
        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, SectorSerializeOptions options)
        {
            if (options.includeHeader)
            {
                writer.WriteLine(string.Join("\t", new string[] {
                    "Sector", "SS", "Hex", "Name", "UWP", "Bases", "Remarks", "Zone", "PBG", "Allegiance", "Stars",
                    "{Ix}", "(Ex)", "[Cx]", "Nobility", "W", "RU" }));
            }
            foreach (World world in worlds.OrderBy(world => world.Subsector))
            {
                writer.WriteLine(string.Join("\t", new string[] {
                    world.Sector?.Abbreviation ?? "",
                    world.SS,
                    options.sscoords ? world.SubsectorHex : world.Hex,
                    world.Name,
                    world.UWP,
                    world.Bases,
                    world.Remarks,
                    world.Zone,
                    world.PBG,
                    world.Allegiance,
                    world.Stellar,
                    world.Importance ?? "",
                    world.Economic ?? "",
                    world.Cultural ?? "",
                    world.Nobility ?? "",
                    world.Worlds > 0 ? world.Worlds.ToString(CultureInfo.InvariantCulture) : "",
                    world.ResourceUnits.ToString(CultureInfo.InvariantCulture)
                }));
            }
        }
    }
}