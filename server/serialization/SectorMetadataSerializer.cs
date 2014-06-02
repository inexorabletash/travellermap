using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;

namespace Maps.Serialization
{
    public abstract class SectorMetadataSerializer
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
    public class XMLSectorMetadataSerializer : SectorMetadataSerializer
    {
        public override void Serialize(TextWriter writer, Sector sector)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Sector));
            xs.Serialize(writer, sector);
        }
    }

}