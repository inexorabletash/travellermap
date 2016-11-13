﻿using Json;
using Maps.API.Results;
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
        protected override string ServiceName { get { return "search"; } }
        protected override DataResponder GetResponder(HttpContext context)
        {
            return new Responder(context);
        }
        private class Responder : DataResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Xml; } }

            private static readonly IReadOnlyDictionary<string, string> SpecialSearches = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase) {
                { @"(default)", @"~/res/search/Default.json"},
                { @"(grand tour)", @"~/res/search/GrandTour.json"},
                { @"(arrival vengeance)", @"~/res/search/ArrivalVengeance.json"},
                { @"(far frontiers)", @"~/res/search/FarFrontiers.json"},
                { @"(cirque)", @"~/res/search/Cirque.json"}
            };

            private static readonly Regex UWP_REGEXP = new Regex(@"^\w{7}-\w$");

            public override void Process()
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
                        SendFile(JsonConstants.MediaType, path);
                        return;
                    }

                    if (Accepts(context, JsonConstants.MediaType))
                    {
                        SendFile(JsonConstants.MediaType, path);
                        return;
                    }
                    return;
                }

                //
                // Do the search
                //
                ResourceManager resourceManager = new ResourceManager(context.Server);
                string milieu = GetStringOption("milieu", SectorMap.DEFAULT_MILIEU);
                SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, milieu);

                int NUM_RESULTS;
                IEnumerable<ItemLocation> searchResults;
                if (query == "(random world)")
                {
                    NUM_RESULTS = 1;
                    searchResults = SearchEngine.PerformSearch(milieu, null, SearchEngine.SearchResultsType.Worlds, NUM_RESULTS, random:true);
                }
                else
                {
                    query = query.Replace('*', '%'); // Support * and % as wildcards
                    query = query.Replace('?', '_'); // Support ? and _ as wildcards

                    if (UWP_REGEXP.IsMatch(query))
                        query = "uwp:" + query;

                    NUM_RESULTS = 160;
                    searchResults = SearchEngine.PerformSearch(milieu, query, SearchEngine.SearchResultsType.Default, NUM_RESULTS);
                }

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
    }
}

namespace Maps.API.Results
{
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
        [XmlElement(ElementName = "label", Type = typeof(LabelResult))]

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

        internal static Item LocationToSearchResult(SectorMap.Milieu map, ResourceManager resourceManager, ItemLocation location)
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

            if (location is LabelLocation)
            {
                LabelLocation label = location as LabelLocation;
                Location l = Astrometrics.CoordinatesToLocation(label.Coords);
                Sector sector = label.Resolve(map);

                LabelResult r = new LabelResult();
                r.Name = label.Label;
                r.SectorX = l.Sector.X;
                r.SectorY = l.Sector.Y;
                r.HexX = l.Hex.X;
                r.HexY = l.Hex.Y;
                r.Scale =
                    label.Radius > 80 ? 4 :
                    label.Radius > 40 ? 8 :
                    label.Radius > 20 ? 32 : 64;
                r.SectorTags = sector.TagString;

                return r;
            }

            throw new ArgumentException(string.Format("Unexpected result type: {0}", location.GetType().Name), "location");
        }
    }
}
