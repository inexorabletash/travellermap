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
        private Dictionary<string, string> m_metaData = new Dictionary<string, string>();

        [XmlAttribute]
        public string Author { get { string s; m_metaData.TryGetValue("author", out s); return s; } set { m_metaData["author"] = value; } }

        [XmlAttribute]
        public string Source { get { string s; m_metaData.TryGetValue("source", out s); return s; } set { m_metaData["source"] = value; } }

        [XmlAttribute]
        public string Title{ get { string s; m_metaData.TryGetValue("title", out s); return s; } set { m_metaData["title"] = value; } }

        [XmlAttribute]
        public string Publisher { get { string s; m_metaData.TryGetValue("publisher", out s); return s; } set { m_metaData["publisher"] = value; } }

        [XmlAttribute]
        public string Copyright { get { string s; m_metaData.TryGetValue("copyright", out s); return s; } set { m_metaData["copyright"] = value; } }

        [XmlAttribute]
        public string Era { get { string s; m_metaData.TryGetValue("era", out s); return s; } set { m_metaData["era"] = value; } }

        [XmlAttribute]
        public string Ref { get { string s; m_metaData.TryGetValue("ref", out s); return s; } set { m_metaData["ref"] = value; } }
    }

    public class MetadataCollection<T> : List<T>, IMetadata
    {
        #region IMetadata Members

        private Dictionary<string, string> m_metaData = new Dictionary<string, string>();

        [XmlAttribute]
        public string Author { get { string s; m_metaData.TryGetValue("author", out s); return s; } set { m_metaData["author"] = value; } }

        [XmlAttribute]
        public string Title { get { string s; m_metaData.TryGetValue("title", out s); return s; } set { m_metaData["title"] = value; } }

        [XmlAttribute]
        public string Source { get { string s; m_metaData.TryGetValue("source", out s); return s; } set { m_metaData["source"] = value; } }

        [XmlAttribute]
        public string Publisher { get { string s; m_metaData.TryGetValue("publisher", out s); return s; } set { m_metaData["publisher"] = value; } }

        [XmlAttribute]
        public string Copyright { get { string s; m_metaData.TryGetValue("copyright", out s); return s; } set { m_metaData["copyright"] = value; } }

        [XmlAttribute]
        public string Era { get { string s; m_metaData.TryGetValue("era", out s); return s; } set { m_metaData["era"] = value; } }

        [XmlAttribute]
        public string Ref { get { string s; m_metaData.TryGetValue("ref", out s); return s; } set { m_metaData["ref"] = value; } }

        #endregion
    }
}