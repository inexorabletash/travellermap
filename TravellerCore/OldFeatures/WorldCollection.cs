#nullable enable
using Maps.Serialization;
using Maps.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Maps
{
    /// <summary>
    /// Summary description for SectorData.
    /// </summary>
    internal class WorldCollection :  IEnumerable<World>
    {
        public WorldCollection()
        {
#if DEBUG
            errors = new ErrorLogger();
#endif
        }

        // Overload rather than optional argument to disambiguate for ResourceManager reflection.
        public WorldCollection(bool isUserData) : this()
        {
            IsUserData = isUserData;
        }

        public bool IsUserData { get; }

        private World[,] worlds = new World[Astrometrics.SectorWidth, Astrometrics.SectorHeight];
        public World this[int x, int y]
        {
            get
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException(nameof(x));
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException(nameof(y));

                return worlds[x - 1, y - 1];
            }
            set
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException(nameof(x));
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException(nameof(y));

                worlds[x - 1, y - 1] = value;
            }
        }

        public World this[int hex] => this[hex / 100, hex % 100];
        public World this[Hex hex] => this[hex.X, hex.Y];

        public IEnumerator<World> GetEnumerator()
        {
            for (int x = 1; x <= Astrometrics.SectorWidth; ++x)
            {
                for (int y = 1; y <= Astrometrics.SectorHeight; ++y)
                {
                    World world = worlds[x - 1, y - 1];
                    if (world != null)
                        yield return world;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private ErrorLogger? errors = null;
        public ErrorLogger? ErrorList => errors;
        public void Serialize(TextWriter writer, string? mediaType, SectorSerializeOptions options)
        {
            SectorFileSerializer.ForType(mediaType).Serialize(writer,
                options.filter == null ? this : this.Where(world => options.filter(world)), options);
        }

        public void Deserialize(Stream stream, string? mediaType, ErrorLogger? log = null)
        {
            if (mediaType == null || mediaType == ContentTypes.Text.Plain || mediaType == ContentTypes.Application.Octet)
                mediaType = SectorFileParser.SniffType(stream);
            SectorFileParser parser = SectorFileParser.ForType(mediaType);
            parser.Parse(stream, this, log);
            if (log != null && !log.Empty)
            {
                log.Prepend(ErrorLogger.Severity.Hint, $"Parsing as: {parser.Name}");
            }
        }

        public ISet<string> AllegianceCodes() => new HashSet<string>(this.Select(world => world.Allegiance));

        // Make a copy of this collection with all the worlds replaced by placeholders.
        // The World objects will have null Sectors
        public WorldCollection MakeDotmap()
        {
            WorldCollection dots = new WorldCollection();
            foreach (var world in this)
            {
                var dot = new World()
                {
                    Hex = world.Hex,
                    UWP = "???????-?",
                    PBG = "???",
                    Allegiance = "??",
                    Sector = null
                };
                dots[dot.X, dot.Y] = dot;
            }
            return dots;
        }
    }
}
