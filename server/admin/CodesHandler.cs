using Maps.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Maps.Admin
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    internal class CodesHandler : AdminHandler
    {
        static readonly IReadOnlyList<string> s_legacySophontCodes = new List<string>
        {
            "A", // Aslan
            "C", // Chirper
            "D", // Droyne
            "F", // Non-Hiver Federation Member
            "H", // Hiver
            "I", // Ithklur
            "M", // Human (Provence/Tuglikki)
            "V", // Vargr
            "X", // Addaxur (Zhodani)
            "Z", // Zhodani
        };

        static readonly RegexMap<string> s_knownCodes = new RegexMap<string>
        {
            // General
            { @"^Rs[ABGDEZHT]$", "Rs" },
            { @"^O:[0-9]{4}(-\w+)?$", "O:nnnn" },
            { @"^O:[A-Za-z]{3,4}-[0-9]{4}$", "O:nnnn (outsector)" },
            { @"^C:[0-9]{4}(-\w+)?$", "C:nnnn" },
            { @"^C:[A-Za-z]{3,4}-[0-9]{4}$", "C:nnnn (outsector)" },

            // Legacy
            "Ag", "As", "Ba", "De",
            "Fa", "Fl", "Hi", "Ic",
            "In", "Lo", "Na", "Nh",
            "Ni", "Nk", "Po", "Ri",
            "St", "Va", "Wa",
            "An", "Cf", "Cm", "Cp", 
            "Cs", "Cx", "Ex", "Mr",
            "Pr", "Rs",

            { @"^(" + string.Join("|", s_legacySophontCodes) + @"):?(\d|w)$", "(sophont)" },

            // Leviathan
            "Tp", "Tn", // Terra-prime, Terra-norm

            // Mongoose
            "Lt", "Ht", // Low-tech, High-tech

            // Other Common
            "Xb", // X-boat stop

            { @"^Rw:?[0-9VZ]$", "Rw#" }, // TNE: Refugee World

            // LWLG
            "Hp", "Hn", // Hiver-prime, Hiver-norm

            // James Kundert
            { @"^S[0-9A-F]{1,2}$", "S##" }, // Companion star orbits

            // Yiklerzdanzh
            "Rn",  
            "Rv",

            // Traveller 5
            "As", "De", "Fl", "Ga", "He", "Ic", "Oc", "Va", "Wa", // Planetary
            "Di", "Ba", "Lo", "Ni", "Ph", "Hi", // Population
            "Pa", "Ag", "Na", "Pi", "In", "Po", "Pr", "Ri", // Economic
            "Fr", "Ho", "Co", "Lk", "Tr", "Tu", "Tz", // Climate
            "Fa", "Mi", "Mr", "Px", "Pe", "Re", // Secondary
            "Cp", "Cs", "Cx", "Cy", // Political
            "Sa", "Fo", "Pz", "Da", "Ab", "An", // Special 

            { @"^\[.*?\][0-9?]?$", "(major race homeworld)" },
            { @"^\(.*?\)[0-9?]?$", "(minor race homeworld)" },
            { @"^Di\(.*?\)$", "(extinct minor race homeworld)" },
            { @"^(" + string.Join("|", SecondSurvey.SophontCodes) + @")(\d|W|\?)$", "(sophont)" },

            { @"^Mr\((" + string.Join("|", SecondSurvey.AllegianceCodes) + @")\)$", "(military rule)" },

            { @"^{.*}$", "(comment)" }
        };
            
        protected override void Process(System.Web.HttpContext context, ResourceManager resourceManager)
        {
            context.Response.ContentType = ContentTypes.Text.Plain;
            context.Response.StatusCode = 200;

            string sectorName = GetStringOption(context, "sector");
            string type = GetStringOption(context, "type");
            string regex = GetStringOption(context, "regex");
            string milieu = GetStringOption(context, "milieu");

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.GetInstance(resourceManager);

            var sectorQuery = from sector in map.Sectors
                              where (sectorName == null || sector.Names[0].Text.StartsWith(sectorName, ignoreCase: true, culture: CultureInfo.InvariantCulture))
                              && (sector.DataFile != null)
                              && (type == null || sector.DataFile.Type == type)
                              && (!sector.Tags.Contains("ZCR")) // Zhodani Core Route
                              && (!sector.Tags.Contains("RE")) // Rim Expedition
                              && (!sector.Tags.Contains("meta"))
                              && (milieu == null || sector.CanonicalMilieu == milieu) 
                              orderby sector.Names[0].Text
                              select sector;

            Dictionary<string, HashSet<string>> codes = new Dictionary<string, HashSet<string>>();

            Regex filter = new Regex(regex ?? ".*");

            foreach (var sector in sectorQuery)
            {
                WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: false);
                if (worlds == null)
                    continue;

                foreach (var code in worlds
                    .SelectMany(world => world.Codes)
                    .Where(code => filter.IsMatch(code) && !s_knownCodes.IsMatch(code)))
                {
                    if (!codes.ContainsKey(code))
                    {
                        codes.Add(code, new HashSet<string>());
                    }
                    codes[code].Add($"{sector.Names[0].Text} [{sector.CanonicalMilieu}]");
                }
            }

            foreach (var code in codes.Keys.OrderBy(s => s))
            {
                context.Response.Output.Write(code + " - ");
                foreach (var sector in codes[code].OrderBy(s => s))
                    context.Response.Output.Write(sector + " ");
                context.Response.Output.WriteLine("");
            }
        }
    }
}
