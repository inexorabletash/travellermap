using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Maps.Serialization
{
    internal abstract class SectorMetadataFileParser
    {
        public const int BUFFER_SIZE = 32768;

        public abstract Encoding Encoding { get; }

        public virtual Sector Parse(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding, detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
            {
                return Parse(reader);
            }
        }
        public abstract Sector Parse(TextReader reader);

        public static SectorMetadataFileParser ForType(string mediaType)
        {
            switch (mediaType)
            {
                case "MSEC": return new MSECParser();
                case "XML":
                default: return new XmlSectorMetadataParser();
            }
        }

        private static readonly Regex sniff_xml = new Regex(@"<\?xml", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static string SniffType(Stream stream)
        {
            long pos = stream.Position;
            try
            {
                using (var reader = new NoCloseStreamReader(stream, Encoding.GetEncoding(1252), detectEncodingFromByteOrderMarks: true, bufferSize: BUFFER_SIZE))
                {
                    string line = reader.ReadLine();
                    if (line != null && sniff_xml.IsMatch(line))
                        return "XML";
                }
                return "MSEC";
            }
            finally
            {
                stream.Position = pos;
            }
        }
    }

    internal class XmlSectorMetadataParser : SectorMetadataFileParser
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Sector Parse(Stream stream)
        {
            try
            {
                return new XmlSerializer(typeof(Sector)).Deserialize(stream) as Sector;
            }
            catch (System.InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                throw ex.InnerException;
            }
        }

        public override Sector Parse(TextReader reader)
        {
            try
            {
                return new XmlSerializer(typeof(Sector)).Deserialize(reader) as Sector;
            }
            catch (System.InvalidOperationException ex) when (ex.InnerException is System.Xml.XmlException)
            {
                throw ex.InnerException;
            }
        }
    }
}
