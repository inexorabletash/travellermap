using Json;
using Maps.API.Results;
using Maps.Search;
using Maps.Utilities;
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
        protected override string ServiceName => "search";
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Text.Xml;
            private static readonly IReadOnlyDictionary<string, string> SpecialSearches = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
                { @"(default)", @"~/res/search/Default.json"},
                { @"(grand tour)", @"~/res/search/GrandTour.json"},
                { @"(arrival vengeance)", @"~/res/search/ArrivalVengeance.json"},
                { @"(far frontiers)", @"~/res/search/FarFrontiers.json"},
                { @"(cirque)", @"~/res/search/Cirque.json"}
            };

            private static readonly Regex UWP_REGEXP = new Regex(@"^\w{7}-\w$");

            public override void Process(ResourceManager resourceManager)
            {
                string query = Context.Request.QueryString["q"];
                if (query == null)
                    return;

                // Look for special searches
                if (SpecialSearches.ContainsKey(query))
                {
                    string path = SpecialSearches[query];

                    if (Context.Request.QueryString["jsonp"] != null)
                    {
                        // TODO: Does this include the JSONP headers?
                        SendFile(JsonConstants.MediaType, path);
                        return;
                    }

                    if (Accepts(Context, JsonConstants.MediaType))
                    {
                        SendFile(JsonConstants.MediaType, path);
                        return;
                    }
                    return;
                }

                //
                // Do the search
                //
                string milieu = GetStringOption("milieu", SectorMap.DEFAULT_MILIEU);
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, milieu);

                int NUM_RESULTS;
                IEnumerable<SearchResult> searchResults;
                if (query == "(random world)")
                {
                    NUM_RESULTS = 1;
                    searchResults = SearchEngine.PerformSearch(milieu, null, SearchEngine.SearchResultsType.Worlds, NUM_RESULTS, random:true);
                }
                else
                {
                    SearchEngine.SearchResultsType types = 0;
                    foreach (var type in GetStringsOption("types", new string[] { "default" }))
                    {
                        switch (type) {
                            case "worlds": types |= SearchEngine.SearchResultsType.Worlds; break;
                            case "subsectors": types |= SearchEngine.SearchResultsType.Subsectors; break;
                            case "sectors": types |= SearchEngine.SearchResultsType.Sectors; break;
                            case "labels": types |= SearchEngine.SearchResultsType.Labels; break;
                            case "default": types |= SearchEngine.SearchResultsType.Default; break;
                        }
                    }

                    query = query.Replace('*', '%'); // Support * and % as wildcards
                    query = query.Replace('?', '_'); // Support ? and _ as wildcards

                    if (UWP_REGEXP.IsMatch(query))
                        query = "uwp:" + query;

                    NUM_RESULTS = 160;
                    searchResults = SearchEngine.PerformSearch(milieu, query, types, NUM_RESULTS);
                }

                SearchResults resultsList = new SearchResults();

                if (searchResults != null)
                {
                    resultsList.AddRange(searchResults
                        .Select(loc => SearchResults.SearchResultToItem(map, resourceManager, loc))
                        .OfType<SearchResults.Item>()
                        .OrderByDescending(item => item.Importance)
                        .Take(NUM_RESULTS));
                }

                SendResult(resultsList);
            }
        }
    }
}

namespace Maps.API.Results
{
    [JsonName("Results")]
    [XmlRoot(ElementName = "results")]
    public class SearchResults
    {
        [XmlAttribute]
        public int Count { get => Items.Count; set { /* We only want to serialize, not deserialize */ } }

        // This is necessary to get "clean" XML serialization of a heterogeneous list;
        // otherwise the output is sprinkled with xsi:type declarations and the base class
        // is used as the element name
        [XmlElement(ElementName = "world", Type = typeof(WorldResult))]
        [XmlElement(ElementName = "subsector", Type = typeof(SubsectorResult))]
        [XmlElement(ElementName = "sector", Type = typeof(SectorResult))]
        [XmlElement(ElementName = "label", Type = typeof(LabelResult))]

        public List<Item> Items { get; } = new List<Item>();

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

        [JsonName("Label")]
        [XmlRoot(ElementName = "Label")]
        public class LabelResult : Item
        {
            [XmlAttribute("hexX")]
            public int HexX { get; set; }

            [XmlAttribute("hexY")]
            public int HexY { get; set; }

            [XmlAttribute("radius")]
            public double Scale { get; set; }
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

            internal int? Importance { get; set; }
        }

        internal static Item SearchResultToItem(SectorMap.Milieu map, ResourceManager resourceManager, SearchResult result)
        {
            if (result is Search.WorldResult worldResult)
            {
                worldResult.Resolve(map, resourceManager, out Sector sector, out World world);

                if (sector == null || world == null)
                    return null;

                return new WorldResult()
                {
                    SectorX = sector.X,
                    SectorY = sector.Y,
                    SectorTags = sector.TagString,
                    HexX = world.X,
                    HexY = world.Y,
                    Name = world.Name,
                    Sector = sector.Names[0].Text,
                    Uwp = world.UWP,
                    Importance = world.ImportanceValue
                };
            }

            if (result is Search.SubsectorResult subsectorResult)
            {
                subsectorResult.Resolve(map, out Sector sector, out Subsector subsector);

                if (sector == null || subsector == null)
                    return null;

                return new SubsectorResult()
                {
                    SectorX = sector.X,
                    SectorY = sector.Y,
                    SectorTags = sector.TagString,
                    Name = subsector.Name,
                    Index = subsector.Index,
                    Sector = sector.Names[0].Text
                };
            }

            if (result is Search.SectorResult sectorResult)
            {
                Sector sector = sectorResult.Resolve(map);

                if (sector == null)
                    return null;

                return new SectorResult()
                {
                    SectorX = sector.X,
                    SectorY = sector.Y,
                    SectorTags = sector.TagString,
                    Name = sector.Names[0].Text
                };
            }

            if (result is Search.LabelResult label)
            {
                Location l = Astrometrics.CoordinatesToLocation(label.Coords);
                Sector sector = label.Resolve(map);

                return new LabelResult()
                {
                    Name = label.Label,
                    SectorX = l.Sector.X,
                    SectorY = l.Sector.Y,
                    HexX = l.Hex.X,
                    HexY = l.Hex.Y,
                    Scale =
                        label.Radius > 80 ? 4 :
                        label.Radius > 40 ? 8 :
                        label.Radius > 20 ? 32 : 64,
                    SectorTags = sector.TagString
                };
            }

            throw new ArgumentException($"Unexpected result type: {result.GetType().Name}", nameof(result));
        }
    }
}
