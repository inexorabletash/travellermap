using Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Xml.Serialization;

namespace Maps.API
{
    internal class SearchHandler : DataHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

        private static Dictionary<string, string> SpecialSearches = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
            { @"(default)", @"~/res/search/Default.json"},
            { @"(grand tour)", @"~/res/search/GrandTour.json"},
            { @"(arrival vengeance)", @"~/res/search/ArrivalVengeance.json"},
            { @"(far frontiers)", @"~/res/search/FarFrontiers.json"},
            { @"(cirque)", @"~/res/search/Cirque.json"}
        };

        private static Regex UWP_REGEXP = new Regex(@"^\w{7}-\w$");

        protected override string ServiceName { get { return "search"; } }

        public override void Process(HttpContext context)
        {
            string query = context.Request.QueryString["q"];
            if (query == null)
                return;

            // Look for special searches
            if (SpecialSearches.ContainsKey(query))
            {
                string path = SpecialSearches[query];

                if (context.Request.QueryString["jsonp"] != null)
                {
                    // TODO: Does this include the JSONP headers?
                    SendFile(context, JsonConstants.MediaType, path);
                    return;
                }

                if (Accepts(context, JsonConstants.MediaType))
                {
                    SendFile(context, JsonConstants.MediaType, path);
                    return;
                }
                return;
            }

            //
            // Do the search
            //
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            query = query.Replace('*', '%'); // Support * and % as wildcards
            query = query.Replace('?', '_'); // Support ? and _ as wildcards

            if (UWP_REGEXP.IsMatch(query))
                query = "uwp:" + query;

            const int NUM_RESULTS = 160;

            var searchResults = SearchEngine.PerformSearch(query, resourceManager, SearchEngine.SearchResultsType.Default, NUM_RESULTS);

            SearchResults resultsList = new SearchResults();

            if (searchResults != null)
            {
                resultsList.AddRange(searchResults
                    .Select(loc => SearchResults.LocationToSearchResult(map, resourceManager, loc))
                    .OfType<SearchResults.Item>()
                    .OrderByDescending(item => item.Importance)
                    .Take(NUM_RESULTS));
            }

            SendResult(context, resultsList);
        }
    }

    [JsonName("Results")]
    [XmlRoot(ElementName = "results")]
    public class SearchResults
    {
        public SearchResults()
        {
            Items = new List<Item>();
        }

        [XmlAttribute]
        public int Count { get { return Items.Count; } set { /* We only want to serialize, not deserialize */ } }

        // This is necessary to get "clean" XML serialization of a heterogeneous list;
        // otherwise the output is sprinkled with xsi:type declarations and the base class
        // is used as the element name
        [XmlElement(ElementName = "world", Type = typeof(WorldResult))]
        [XmlElement(ElementName = "subsector", Type = typeof(SubsectorResult))]
        [XmlElement(ElementName = "sector", Type = typeof(SectorResult))]

        public List<Item> Items { get; }

        public void Add(Item item)
        {
            Items.Add(item);
        }
        public void AddRange(IEnumerable<Item> items)
        {
            Items.AddRange(items);
        }

        [JsonName("World")]
        public class WorldResult : Item
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
        public class SubsectorResult : Item
        {
            [XmlAttribute("sector")]
            public string Sector { get; set; }

            [XmlAttribute("index")]
            public string Index { get; set; }
        }

        [JsonName("Sector")]
        [XmlRoot(ElementName = "sector")]
        public class SectorResult : Item
        {
        }

        public abstract class Item
        {
            [XmlAttribute("sectorX")]
            public int SectorX { get; set; }

            [XmlAttribute("sectorY")]
            public int SectorY { get; set; }

            [XmlAttribute("name")]
            public string Name { get; set; }

            [XmlAttribute("sectorTags")]
            public string SectorTags { get; set; }

            [XmlIgnore, JsonIgnore]
            public int? Importance { get; set; }
        }

        internal static Item LocationToSearchResult(SectorMap map, ResourceManager resourceManager, ItemLocation location)
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
                r.SectorTags = sector.TagString;
                r.HexX = world.X;
                r.HexY = world.Y;
                r.Name = world.Name;
                r.Sector = sector.Names[0].Text;
                r.Uwp = world.UWP;
                r.Importance = world.ImportanceValue;

                return r;
            }

            if (location is SubsectorLocation)
            {
                Sector sector;
                Subsector subsector;
                ((SubsectorLocation)location).Resolve(map, out sector, out subsector);

                if (sector == null || subsector == null)
                    return null;

                SubsectorResult r = new SubsectorResult();
                r.SectorX = sector.X;
                r.SectorY = sector.Y;
                r.SectorTags = sector.TagString;
                r.Name = subsector.Name;
                r.Index = subsector.Index;
                r.Sector = sector.Names[0].Text;

                return r;
            }

            if (location is SectorLocation)
            {
                Sector sector = ((SectorLocation)location).Resolve(map);

                if (sector == null)
                    return null;

                SectorResult r = new SectorResult();
                r.SectorX = sector.X;
                r.SectorY = sector.Y;
                r.SectorTags = sector.TagString;
                r.Name = sector.Names[0].Text;

                return r;
            }

            return null;
        }
    }
}
