using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Maps.Serialization
{
    public abstract class SectorFileSerializer
    {
        public abstract Encoding Encoding { get; }

        public virtual void Serialize(Stream stream, IEnumerable<World> worlds, bool includeHeader=true, bool sscoords=false)
        {
            using (var writer = new StreamWriter(stream, Encoding))
            {
                Serialize(writer, worlds, includeHeader:includeHeader, sscoords:sscoords);
            }
        }

        public abstract void Serialize(TextWriter writer, IEnumerable<World> worlds, bool includeHeader=true, bool sscoords=false);

        public static SectorFileSerializer ForType(string mediaType)
        {
            switch (mediaType)
            {
                case "SecondSurvey": return new SecondSurveySerializer();
                case "TabDelimited": return new TabDelimitedSerializer();
                case "SEC":
                default: return new SecSerializer();
            }
        }
    }

    public class SecSerializer : SectorFileSerializer
    {
        public override Encoding Encoding { get { return Encoding.GetEncoding(1252); } }

        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, bool includeHeader=true, bool sscoords=false)
        {

            if (includeHeader)
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
                    sscoords ? world.SubsectorHex : world.Hex,
                    world.UWP,
                    world.CompactLegacyBases,
                    world.Remarks.Truncate(15),
                    world.Zone,
                    world.PBG,
                    world.Allegiance,
                    world.Stellar.Truncate(15)
                    );
            }
        }
    }

    public class SecondSurveySerializer : SectorFileSerializer
    {
        public override Encoding Encoding { get { return Util.UTF8_NO_BOM; } }

        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, bool includeHeader=true, bool sscoords=false)
        {
            ColumnSerializer formatter = new ColumnSerializer(new string[] {
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
            });

            formatter.SetMinimumWidth("Name", 20);
            formatter.SetMinimumWidth("Remarks", 20);

            foreach (World world in worlds.OrderBy(world => world.SS))
            {
                formatter.AddRow(new string[] {
                    sscoords ? world.SubsectorHex : world.Hex,
                    world.Name,
                    world.UWP,
                    world.Remarks,
                    world.Importance,
                    world.Economic,
                    world.Cultural,
                    DashIfEmpty(world.Nobility),
                    DashIfEmpty(world.CompactLegacyBases),
                    DashIfEmpty(world.Zone),
                    world.PBG,
                    world.Worlds > 0 ? world.Worlds.ToString() : "",
                    world.Allegiance,
                    world.Stellar
                });
            }
            formatter.Serialize(writer, includeHeader);
        }

        private static string DashIfEmpty(string s)
        {
            if (String.IsNullOrWhiteSpace(s))
                return "-";
            return s;
        }
    }

    public class TabDelimitedSerializer : SectorFileSerializer
    {
        public override Encoding Encoding { get { return Util.UTF8_NO_BOM; } }

        public override void Serialize(TextWriter writer, IEnumerable<World> worlds, bool includeHeader=true, bool sscoords=false)
        {
            if (includeHeader)
            {
                writer.WriteLine(String.Join("\t", new string[] {
                    "Sector", "SS", "Hex", "Name", "UWP", "Bases", "Remarks", "Zone", "PBG", "Allegiance", "Stars",
                    "{Ix}", "(Ex)", "[Cx]", "Nobility", "W", "RU" }));
            }
            foreach (World world in worlds.OrderBy(world => world.Subsector))
            {
                writer.WriteLine(String.Join("\t", new string[] {
                    world.Sector.Abbreviation,
                    world.SS,
                    sscoords ? world.SubsectorHex : world.Hex,
                    world.Name,
                    world.UWP,
                    world.CompactLegacyBases, // TODO: T5Bases ?
                    world.Remarks,
                    world.Zone,
                    world.PBG,
                    world.Allegiance,
                    world.Stellar,
                    world.Importance,
                    world.Economic,
                    world.Cultural,
                    world.Nobility,
                    world.Worlds > 0 ? world.Worlds.ToString(CultureInfo.InvariantCulture) : "",
                    world.ResourceUnits.ToString(CultureInfo.InvariantCulture)
                }));
            }
        }
    }
}