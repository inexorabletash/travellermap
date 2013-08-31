using Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class Search : DataPage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        private static string[] SpecialSearchTerms = { @"(default)", @"(grand tour)", @"(arrival vengeance)", @"(far frontiers)" };
        private static string[] SpecialSearchResultsXml = { @"~/res/search/Default.xml", @"~/res/search/GrandTour.xml", @"~/res/search/ArrivalVengeance.xml", @"~/res/search/FarFrontiers.xml" };
        private static string[] SpecialSearchResultsJson = { @"~/res/search/Default.json", @"~/res/search/GrandTour.json", @"~/res/search/ArrivalVengeance.json", @"~/res/search/FarFrontiers.json" };

        private static Regex UWP_REGEXP = new Regex(@"^\w{7}-\w$");

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!ServiceConfiguration.CheckEnabled("search", Response))
            {
                return;
            }

            string query = Request.QueryString["q"];
            if (query == null)
                return;

            // Look for special searches
            var index = Array.FindIndex(SpecialSearchTerms, s => String.Compare(s, query, ignoreCase: true, culture: CultureInfo.InvariantCulture) == 0);
            if (index != -1)
            {
                if (Request.QueryString["jsonp"] != null)
                {
                    // TODO: Does this include the JSONP headers?
                    SendFile(JsonConstants.MediaType, SpecialSearchResultsJson[index]);
                    return;
                }

                foreach (var type in AcceptTypes)
                {
                    if (type == JsonConstants.MediaType)
                    {
                        SendFile(JsonConstants.MediaType, SpecialSearchResultsJson[index]);
                        return;
                    }
                    if (type == MediaTypeNames.Text.Xml)
                    {
                        SendFile(MediaTypeNames.Text.Xml, SpecialSearchResultsXml[index]);
                        return;
                    }
                }
                SendFile(MediaTypeNames.Text.Xml, SpecialSearchResultsXml[index]);
                return;
            }

            //
            // Do the search
            //
            ResourceManager resourceManager = new ResourceManager(Server, Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            query = query.Replace('*', '%'); // Support * and % as wildcards
            query = query.Replace('?', '_'); // Support ? and _ as wildcards

            if (UWP_REGEXP.IsMatch(query))
            {
                query = "uwp:" + query;
            }

            var searchResults = SearchEngine.PerformSearch(query, resourceManager, SearchEngine.SearchResultsType.Default, 160);

            Results resultsList = new Results();

            if (searchResults != null)
            {
                resultsList.AddRange(searchResults
                    .Select(loc => Results.LocationToSearchResult(map, resourceManager, loc))
                    .OfType<Results.SearchResultItem>());
            }

            SendResult(resultsList);
        }

        [JsonName("Results")]
        [XmlRoot(ElementName = "results")]
        public class Results
        {
            public Results()
            {
                Items = new List<SearchResultItem>();
            }

            [XmlAttribute]
            public int Count { get { return Items.Count; } set { /* We only want to serialize, not deserialize */ } }

            // This is necessary to get "clean" XML serialization of a heterogeneous list;
            // otherwise the output is sprinkled with xsi:type declarations and the base class
            // is used as the element name
            [XmlElement(ElementName = "world", Type = typeof(WorldResult))]
            [XmlElement(ElementName = "subsector", Type = typeof(SubsectorResult))]
            [XmlElement(ElementName = "sector", Type = typeof(SectorResult))]

            public List<SearchResultItem> Items { get; set; }

            public void Add(SearchResultItem item)
            {
                Items.Add(item);
            }
            public void AddRange(IEnumerable<SearchResultItem> items)
            {
                Items.AddRange(items);
            }

            [JsonName("World")]
            public class WorldResult : SearchResultItem
            {
                [XmlAttribute("hexX")]
                public int HexX { get; set; }

                [XmlAttribute("hexY")]
                public int HexY { get; set; }

                [XmlAttribute("sector")]
                public string Sector { get; set; }

                [XmlAttribute("uwp")]
                public string Uwp { get; set; }
            }

            [JsonName("Subsector")]
            public class SubsectorResult : SearchResultItem
            {
                [XmlAttribute("sector")]
                public string Sector { get; set; }

                [XmlAttribute("index")]
                public string Index { get; set; }
            }

            [JsonName("Sector")]
            [XmlRoot(ElementName = "sector")]
            public class SectorResult : SearchResultItem
            {
            }

            public class SearchResultItem
            {
                [XmlAttribute("sectorX")]
                public int SectorX { get; set; }

                [XmlAttribute("sectorY")]
                public int SectorY { get; set; }

                [XmlAttribute("name")]
                public string Name { get; set; }
            }

            public static SearchResultItem LocationToSearchResult(SectorMap map, ResourceManager resourceManager, ItemLocation location)
            {
                if (location is WorldLocation)
                {
                    Sector sector;
                    World world;
                    ((WorldLocation)location).Resolve(map, resourceManager, out sector, out world);

                    if (sector == null || world == null)
                        return null;

                    WorldResult r = new WorldResult();
                    r.SectorX = sector.X;
                    r.SectorY = sector.Y;
                    r.HexX = (world.Hex / 100);
                    r.HexY = (world.Hex % 100);
                    r.Name = world.Name;
                    r.Sector = sector.Names[0].Text;
                    r.Uwp = world.UWP;

                    return r;
                }
                else if (location is SubsectorLocation)
                {
                    Sector sector;
                    Subsector subsector;
                    ((SubsectorLocation)location).Resolve(map, out sector, out subsector);

                    if (sector == null || subsector == null)
                        return null;

                    SubsectorResult r = new SubsectorResult();
                    r.SectorX = sector.X;
                    r.SectorY = sector.Y;
                    r.Name = subsector.Name;
                    r.Index = subsector.Index;
                    r.Sector = sector.Names[0].Text;

                    return r;
                }
                else if (location is SectorLocation)
                {
                    Sector sector = ((SectorLocation)location).Resolve(map);

                    if (sector == null)
                        return null;

                    SectorResult r = new SectorResult();
                    r.SectorX = sector.X;
                    r.SectorY = sector.Y;
                    r.Name = sector.Names[0].Text;

                    return r;
                }

                return null;
            }
        }


        #region Web Form Designer generated code
        override protected void OnInit(EventArgs e)
        {
            //
            // CODEGEN: This call is required by the ASP.NET Web Form Designer.
            //
            InitializeComponent();
            base.OnInit(e);
        }

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Load += new System.EventHandler(this.Page_Load);
        }
        #endregion
    }
}
