﻿using System.IO;

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
                default: return new XmlSectorMetadataSerializer();
            }
        }
    }

    // NOTE: This is unused; see SectorMetaDataHandler
    internal class XmlSectorMetadataSerializer : SectorMetadataSerializer
    {
        public override void Serialize(TextWriter writer, Sector sector)
        {
            throw new System.ApplicationException("Not Implemented");
        }
    }

}