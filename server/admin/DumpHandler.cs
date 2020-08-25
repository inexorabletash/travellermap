#nullable enable
using Maps.Utilities;
using System.Collections.Generic;

namespace Maps.Admin
{
    /// <summary>
    /// Fetch data about the universe.
    /// </summary>
    internal class DumpHandler : AdminHandlerBase
    {
        protected override void Process(System.Web.HttpContext context, ResourceManager resourceManager)
        {
            // NOTE: This (re)initializes a static data structure used for 
            // resolving names into sector locations, so needs to be run
            // before any other objects (e.g. Worlds) are loaded.
            SectorMap map = SectorMap.GetInstance(resourceManager);

            context.Response.ContentType = ContentTypes.Text.Plain;
            context.Response.BufferOutput = false;

            foreach (Sector sector in map.Sectors)
            {
                WorldCollection? worlds = sector.GetWorlds(resourceManager);
                if (worlds == null)
                    continue;
                foreach (World world in worlds)
                {
                    List<string> list = new List<string> {
                        sector.Names[0].Text,
                        sector.X.ToString(),
                        sector.Y.ToString(),
                        world.X.ToString(),
                        world.Y.ToString(),
                        world.Name,
                        world.UWP,
                        world.Bases,
                        world.Remarks,
                        world.PBG,
                        world.Allegiance,
                        world.Stellar
                    };
                    WriteCSV(context.Response.Output, list);
                }
            }
        }

        private static void WriteCSV(System.IO.TextWriter output, List<string> values)
        {
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                    output.Write(',');
                else
                    first = false;

                if (value.IndexOf(',') == -1 && value.IndexOf('"') == -1)
                {
                    // plain
                    output.Write(value);
                }
                else
                {
                    // quoted
                    output.Write('"');
                    output.Write(value.Replace("\"", "\"\""));
                    output.Write('"');
                }
            }
            output.Write("\n");
        }
    }
}
