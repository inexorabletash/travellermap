using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;

namespace Maps.Serialization
{
    internal abstract class SectorMetadataSerializer
    {
        public abstract void Serialize(TextWriter writer, Sector sector);

        public static SectorMetadataSerializer ForType(string mediaType)
        {
            switch (mediaType)
            {
                case "MSEC": return new MSECSerializer();
                case "XML":
                default: return new XMLSectorMetadataSerializer();
            }
        }
    }

    // NOTE: This is unused; see SectorMetaDataHandler
    internal class XMLSectorMetadataSerializer : SectorMetadataSerializer
    {
        public override void Serialize(TextWriter writer, Sector sector)
        {
            new XmlSerializer(typeof(Sector)).Serialize(writer, sector);
        }
    }

}