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
    public class WorldCollection : IDeserializable, IEnumerable<World>
    {
        private World[,] m_worlds = new World[Astrometrics.SectorWidth, Astrometrics.SectorHeight];
        public World this[int x, int y]
        {
            get
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException("x");
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException("y");

                return m_worlds[x - 1, y - 1];
            }
            set
            {
                if (x < 1 || x > Astrometrics.SectorWidth)
                    throw new ArgumentOutOfRangeException("x");
                if (y < 1 || y > Astrometrics.SectorHeight)
                    throw new ArgumentOutOfRangeException("y");

                m_worlds[x - 1, y - 1] = value;
            }
        }

        public IEnumerator<World> GetEnumerator()
        {
            for (int x = 1; x <= Astrometrics.SectorWidth; ++x)
            {
                for (int y = 1; y <= Astrometrics.SectorHeight; ++y)
                {
                    World world = m_worlds[x - 1, y - 1];
                    if (world != null)
                        yield return world;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }


#if DEBUG
        private List<string> m_errorList = new List<string>();
        public List<string> ErrorList { get { return m_errorList; } }
#endif

        public void Serialize(TextWriter writer, string mediaType, bool includeHeader=true, bool sscoords=false, WorldFilter filter=null)
        {
            SectorFileSerializer.ForType(mediaType).Serialize(writer, this.Where(world => filter == null || filter(world)), includeHeader:includeHeader, sscoords:sscoords);
        }

        public void Deserialize(Stream stream, string mediaType)
        {
            if (mediaType == null || mediaType == MediaTypeNames.Text.Plain || mediaType == MediaTypeNames.Application.Octet)
                mediaType = SectorFileParser.SniffType(stream);
            SectorFileParser.ForType(mediaType).Parse(stream, this);
        }
    }
}
