using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Mime;
using System.Text.RegularExpressions;

namespace Maps.Pages
{
    /// <summary>
    /// Summary description for Search.
    /// </summary>
    public class Codes : BasePage
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

        static List<string> s_t5SophontCodes = new List<string>
        {
            "Adda",
            "AHum",
            "Aqua",
            "Asla",
            "Bwap",
            "Chir",
            "Darm",
            "Dolp",
            "Droy",
            "Gurv",
            "Hama",
            "Huma",
            "Jala",
            "Jonk",
            "Lanc",
            "Mask",
            "Orca",
            "S'mr",
            "Scan",
            "Ss'r",
            "Sydi",
            "Tapa",
            "UApe",
            "Ursa",
            "Varg",
            "Zhod",
            "Ziad",
        };
        static RegexDictionary<string> s_knownCodes = new RegexDictionary<string>
        {
            // General
            { @"^Rs[ABGDEZHT]$", "Rs" },
            { @"^O:$", "O:unassigned" },
            { @"^O:XXXX$", "O:unassigned" },
            { @"^O:\w\w$", "O:allegiance" },
            { @"^O:[0-9]{4}(-\w+)?$", "O:nnnn" },
            { @"^Mr:[0-9]{4}(-\w+)?$", "O:nnnn" },

            // Legacy
            "Ag", "As", "Ba", "De",
            "Fa", "Fl", "Hi", "Ic",
            "In", "Lo", "Na", "Nh",
            "Ni", "Nk", "Po", "Ri",
            "St", "Va", "Wa",
            "An", "Cf", "Cm", "Cp", 
            "Cs", "Cx", "Ex", "Mr",
            "Pr", "Rs",

            "Aw", "Cw", "Dw", "Vw", // Aslan/Chirper/Droyne/Vargr world

            // Leviathan
            "Tp", "Tn", // Terra-prime, Terra-norm

            // Mongoose
            "Lt", "Ht", // Low-tech, High-tech

            // Other Common
            "Xb", // X-boat stop

            { @"^Rw:[0-9]$", "Rw#" }, // TNE: Refugee World
            { @"^A:?[0-9]$", "A#" }, // Aslan population
            { @"^C:?[0-9]$", "C#" }, // Chirper population
            { @"^D:?[0-9]$", "D#" }, // Droyne population
            { @"^M:?[0-9]$", "M#" }, // Human population (Provence/Tuglikki)
            { @"^V:?[0-9]$", "V#" }, // Vargr population
            { @"^X:?[0-9]$", "X#" }, // Addaxur population (Zhodani)
            { @"^Z:?[0-9]$", "Z#" }, // Zhodani population

            // LWLG
            "Hp", "Hn", // Hiver-prime, Hiver-norm
            "Iw", // Ithkulur world
            { @"^H:?[0-9]$", "H#" }, // Hiver population
            { @"^F:?[0-9]$", "F#" }, // Federation member (non-Hiver) population

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
            { @"^(" + String.Join("|", s_t5SophontCodes) + @")(\d|W|\?)$", "(sophont)" },
        };

        private void Page_Load(object sender, System.EventArgs e)
        {
            if (!AdminAuthorized())
                return;

            Response.ContentType = MediaTypeNames.Text.Plain;

            ResourceManager resourceManager = new ResourceManager(Server, Cache);

            string sectorName = GetStringOption("sector");
            string type = GetStringOption("type");
            string regex = GetStringOption("regex");

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
                Response.Output.Write(code + " - ");
                foreach (var sector in codes[code].OrderBy(s => s))
                {
                    Response.Output.Write(sector + " ");
                }
                Response.Output.WriteLine("");
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
