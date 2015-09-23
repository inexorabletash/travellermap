using System.Collections.Generic;
using System.Xml.Serialization;

namespace Maps
{
    public interface IMetadata
    {
        [XmlAttribute]
        string Title { get; set; }

        [XmlAttribute]
        string Author { get; set; }

        [XmlAttribute]
        string Source { get; set; }

        [XmlAttribute]
        string Publisher { get; set; }

        [XmlAttribute]
        string Copyright { get; set; }

        [XmlAttribute]
        string Era { get; set; }

        [XmlAttribute]
        string Ref { get; set; }
    }

    public abstract class MetadataItem : IMetadata
    {
        private Dictionary<string, string> metaData = new Dictionary<string, string>();

        [XmlAttribute]
        public string Author { get { string s; metaData.TryGetValue("author", out s); return s; } set { metaData["author"] = value; } }

        [XmlAttribute]
        public string Source { get { string s; metaData.TryGetValue("source", out s); return s; } set { metaData["source"] = value; } }

        [XmlAttribute]
        public string Title{ get { string s; metaData.TryGetValue("title", out s); return s; } set { metaData["title"] = value; } }

        [XmlAttribute]
        public string Publisher { get { string s; metaData.TryGetValue("publisher", out s); return s; } set { metaData["publisher"] = value; } }

        [XmlAttribute]
        public string Copyright { get { string s; metaData.TryGetValue("copyright", out s); return s; } set { metaData["copyright"] = value; } }

        [XmlAttribute]
        public string Era { get { string s; metaData.TryGetValue("era", out s); return s; } set { metaData["era"] = value; } }

        [XmlAttribute]
        public string Ref { get { string s; metaData.TryGetValue("ref", out s); return s; } set { metaData["ref"] = value; } }
    }

    public class MetadataCollection<T> : List<T>, IMetadata
    {
        #region IMetadata Members

        private Dictionary<string, string> metaData = new Dictionary<string, string>();

        [XmlAttribute]
        public string Author { get { string s; metaData.TryGetValue("author", out s); return s; } set { metaData["author"] = value; } }

        [XmlAttribute]
        public string Title { get { string s; metaData.TryGetValue("title", out s); return s; } set { metaData["title"] = value; } }

        [XmlAttribute]
        public string Source { get { string s; metaData.TryGetValue("source", out s); return s; } set { metaData["source"] = value; } }

        [XmlAttribute]
        public string Publisher { get { string s; metaData.TryGetValue("publisher", out s); return s; } set { metaData["publisher"] = value; } }

        [XmlAttribute]
        public string Copyright { get { string s; metaData.TryGetValue("copyright", out s); return s; } set { metaData["copyright"] = value; } }

        [XmlAttribute]
        public string Era { get { string s; metaData.TryGetValue("era", out s); return s; } set { metaData["era"] = value; } }

        [XmlAttribute]
        public string Ref { get { string s; metaData.TryGetValue("ref", out s); return s; } set { metaData["ref"] = value; } }

        #endregion
    }
}