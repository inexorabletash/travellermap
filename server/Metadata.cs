#nullable enable
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Maps
{
    public interface IMetadata
    {
        [XmlAttribute]
        string? Title { get; set; }

        [XmlAttribute]
        string? Author { get; set; }

        [XmlAttribute]
        string? Source { get; set; }

        [XmlAttribute]
        string? Publisher { get; set; }

        [XmlAttribute]
        string? Copyright { get; set; }

        [XmlAttribute]
        string? Milieu { get; set; }

        [XmlAttribute]
        string? Ref { get; set; }
    }

    public abstract class MetadataItem : IMetadata
    {
        private readonly Dictionary<string, string?> metaData = new Dictionary<string, string?>();
        private string? TryGet(string key)
        {
            metaData.TryGetValue(key, out string? s);
            return s;
        }

        [XmlAttribute]
        public string? Author { get => TryGet("author"); set => metaData["author"] = value; }

        [XmlAttribute]
        public string? Source { get => TryGet("source"); set => metaData["source"] = value; }

        [XmlAttribute]
        public string? Title{ get => TryGet("title"); set => metaData["title"] = value; }

        [XmlAttribute]
        public string? Publisher { get => TryGet("publisher"); set => metaData["publisher"] = value; }

        [XmlAttribute]
        public string? Copyright { get => TryGet("copyright"); set => metaData["copyright"] = value; }

        [XmlAttribute]
        public string? Milieu { get => TryGet("era"); set => metaData["era"] = value; }

        [XmlAttribute]
        public string? Ref { get => TryGet("ref"); set => metaData["ref"] = value; }
    }

    public class MetadataCollection<T> : List<T>, IMetadata
    {
        #region IMetadata Members

        private readonly Dictionary<string, string> metaData = new Dictionary<string, string>();
        private string TryGet(string key)
        {
            metaData.TryGetValue(key, out string s);
            return s;
        }

        [XmlAttribute]
        public string? Author { get => TryGet("author"); set { if (value != null) metaData["author"] = value; } }

        [XmlAttribute]
        public string? Title { get => TryGet("title"); set { if (value != null) metaData["title"] = value; } }

        [XmlAttribute]
        public string? Source { get => TryGet("source"); set { if (value != null) metaData["source"] = value; } }

        [XmlAttribute]
        public string? Publisher { get => TryGet("publisher"); set { if (value != null) metaData["publisher"] = value; } }

        [XmlAttribute]
        public string? Copyright { get => TryGet("copyright"); set { if (value != null) metaData["copyright"] = value; } }

        [XmlAttribute]
        public string? Milieu { get => TryGet("era"); set { if (value != null) metaData["era"] = value; } }

        [XmlAttribute]
        public string? Ref { get { metaData.TryGetValue("ref", out string s); return s; } set { if (value != null) metaData["ref"] = value; } }

        #endregion
    }
}