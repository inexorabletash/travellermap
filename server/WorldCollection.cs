using Maps.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;

namespace Maps
{
    /// <summary>
    /// Summary description for SectorData.
    /// </summary>
    internal class WorldCollection : IDeserializable, IEnumerable<World>
    {
        public WorldCollection(bool isUserData = false)
        {
            IsUserData = isUserData;
#if DEBUG
            errors = new ErrorLogger();
#endif
        }
        
        public bool IsUserData { get; }
        private World[,] worlds = new World[Astrometrics.SectorWidth, Astrometrics.SectorHeight];
        public World this[int x, int y]
        {
            get
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException("x");
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException("y");

                return worlds[x - 1, y - 1];
            }
            set
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException("x");
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException("y");

                worlds[x - 1, y - 1] = value;
            }
        }
        public World this[int hex] { get { return this[hex / 100, hex % 100]; } }
        public World this[Hex hex] { get { return this[hex.X, hex.Y]; } }

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

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

        private ErrorLogger errors = null;
        public ErrorLogger ErrorList { get { return errors; } }

        public void Serialize(TextWriter writer, string mediaType, bool includeHeader = true, bool sscoords = false, WorldFilter filter = null)
        {
            SectorFileSerializer.ForType(mediaType).Serialize(writer, this.Where(world => filter == null || filter(world)), includeHeader: includeHeader, sscoords: sscoords);
        }

        public void Deserialize(Stream stream, string mediaType, ErrorLogger errors = null)
        {
            if (mediaType == null || mediaType == MediaTypeNames.Text.Plain || mediaType == MediaTypeNames.Application.Octet)
                mediaType = SectorFileParser.SniffType(stream);
            SectorFileParser parser = SectorFileParser.ForType(mediaType);
            parser.Parse(stream, this, errors);
            if (errors != null && !errors.Empty)
            {
                errors.Prepend(ErrorLogger.Severity.Warning, string.Format("Parsing as: {0}", parser.Name));
            }
        }

        public HashSet<string> AllegianceCodes()
        {
            var set = new HashSet<string>();
            foreach (var world in this)
                set.Add(world.Allegiance);
            return set;
        }
    }
}
