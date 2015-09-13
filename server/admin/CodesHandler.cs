using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace Maps.Admin
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    internal class CodesHandler : AdminHandler
    {

        static List<string> s_legacySophontCodes = new List<string>
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

        static RegexDictionary<string> s_knownCodes = new RegexDictionary<string>
        {
            // General
            { @"^Rs[ABGDEZHT]$", "Rs" },
            { @"^O:[0-9]{4}(-\w+)?$", "O:nnnn" },
            { @"^O:[A-Za-z]{4}-[0-9]{4}$", "O:nnnn (outsector)" },

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

            { @"^Rw:[0-9]$", "Rw#" }, // TNE: Refugee World

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

            { @"^\[.*?\]\d*$", "(major race homeworld)" },
            { @"^\(.*?\)\d*$", "(minor race homeworld)" },
            { @"^(" + string.Join("|", SecondSurvey.SophontCodes) + @")(\d|W|\?)$", "(sophont)" },
        };

        protected override void Process(System.Web.HttpContext context)
        {
            context.Response.ContentType = MediaTypeNames.Text.Plain;
            context.Response.StatusCode = 200;

            ResourceManager resourceManager = new ResourceManager(context.Server);

            string sectorName = GetStringOption(context, "sector");
            string type = GetStringOption(context, "type");
            string regex = GetStringOption(context, "regex");

            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap.Flush();
            SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

            var sectorQuery = from sector in map.Sectors
                              where (sectorName == null || sector.Names[0].Text.StartsWith(sectorName, ignoreCase: true, culture: CultureInfo.InvariantCulture))
                              && (sector.DataFile != null)
                              && (type == null || sector.DataFile.Type == type)
                              && (!sector.DataFile.FileName.Contains(System.IO.Path.PathSeparator)) // Skip ZCR sectors
                              orderby sector.Names[0].Text
                              select sector;

            Dictionary<string, HashSet<string>> codes = new Dictionary<string, HashSet<string>>();

            Regex filter = (regex == null) ? new Regex(".*") : new Regex(regex);

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
                        HashSet<string> hash = new HashSet<string>();
                        hash.Add(sector.Names[0].Text);
                        codes.Add(code, hash);
                    }
                    else
                    {
                        codes[code].Add(sector.Names[0].Text);
                    }
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
