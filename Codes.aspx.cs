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

        // TODO: Add T5 codes
        static RegexDictionary<string> s_knownCodes = new RegexDictionary<string>
        {
            // General
            { "^Rs[ABGDEZH]$", "Rs" },
            { "^O:[0-9]{4}$", "O:nnnn" },

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

            { "^Rw:[0-9]$", "Rw#" }, // TNE: Refugee World
            { "^A:?[0-9]$", "A#" }, // Aslan population
            { "^C:?[0-9]$", "C#" }, // Chirper population
            { "^D:?[0-9]$", "D#" }, // Droyne population
            { "^M:?[0-9]$", "M#" }, // Human population (Provence/Tuglikki)
            { "^V:?[0-9]$", "V#" }, // Vargr population
            { "^X:?[0-9]$", "X#" }, // Addaxur population (Zhodani)
            { "^Z:?[0-9]$", "Z#" }, // Zhodani population

            { "^H:?[0-9]$", "H#" }, // Hiver population (LWLG)
            { "^Hn$", "Hn" }, // Hiver-norm (LWLG)
            { "^Hp$", "Hp" }, // Hiver-prime (LWLG)
            { "^F:?[0-9]$", "F#" }, // Federation member (non-Hiver) population (LWLG)

            { "^S[0-9A-F]{1,2}$", "S##" }, // Companion star orbits (James Kundert)

            { "^Rn$", "Rn" }, // Yiklerzdanzh 
            { "^Rv$", "Rv" }, // Yiklerzdanzh 


            // Traveller5
            "As", "De", "Fl", "Ga", "He", "Ic", "Oc", "Va", 
            "Wa", "Di", "Ba", "Lo", "Ni", "Ph", "Hi", "Pa", 
            "Ag", "Na", "Pi", "In", "Po", "Pr", "Ri", "Fr", 
            "Ho", "Co", "Lk", "Tr", "Tu", "Tz", "Fa", "Mi", 
            "Co", "Mr", "Px", "Px", "Re", "Cp", "Cs", "Cx", 
            "An", "Ab", "Sa", "Fo", "Pz", "Da"
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
                    .SelectMany(world => world.Remarks.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
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
