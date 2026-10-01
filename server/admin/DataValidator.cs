#nullable enable
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Maps.Admin
{
    /// <summary>
    /// Checks sector data and metadata for problems that break or degrade the map.
    /// Has no ASP.NET dependency, so it can run from unit tests or tools (set
    /// Util.ContentRoot first when not hosted).
    /// </summary>
    internal class DataValidator
    {
        internal enum Severity { Warning, Error }

        internal class Finding
        {
            public Finding(Severity severity, string category, string where, string message)
            {
                Severity = severity;
                Category = category;
                Where = where;
                Message = message;
            }
            public Severity Severity { get; }
            /// <summary>Short, stable kind of problem, for grouping and baselines.</summary>
            public string Category { get; }
            /// <summary>Sector and milieu, or file.</summary>
            public string Where { get; }
            public string Message { get; }
            public override string ToString() => $"{Severity}: [{Category}] {Where}: {Message}";
        }

        private readonly List<Finding> findings = new List<Finding>();
        public IReadOnlyList<Finding> Findings => findings;

        private void Add(Severity severity, string category, string where, string message)
            => findings.Add(new Finding(severity, category, where, message));

        /// <summary>
        /// Validates every sector with a data file. World-level data warnings from the
        /// parser (UWP, stellar, etc.) are only available in DEBUG builds.
        /// </summary>
        public void ValidateSectors(SectorMap map, ResourceManager resourceManager, Func<Sector, bool>? filter = null)
        {
            foreach (var sector in map.Sectors.Where(s => s.DataFile != null && (filter == null || filter(s))))
            {
                string where = $"{sector.Names[0].Text} ({sector.CanonicalMilieu})";

                WorldCollection? worlds = null;
                try
                {
                    worlds = sector.GetWorlds(resourceManager, cacheResults: false);
                }
                catch (Exception ex)
                {
                    Add(Severity.Error, "data-file", where, $"{sector.DataFile!.FileName}: {ex.Message}");
                }

                if (worlds != null)
                {
                    if (worlds.ErrorList != null)
                    {
                        foreach (var record in worlds.ErrorList.Records)
                        {
                            if (record.severity >= ErrorLogger.Severity.Error)
                                Add(Severity.Error, "world-parse", where, record.message);
                            else if (record.severity == ErrorLogger.Severity.Warning)
                                Add(Severity.Warning, "world-data", where, record.message);
                        }
                    }

                    foreach (var world in worlds)
                    {
                        if (world.Allegiance != "" && sector.GetAllegianceFromCode(world.Allegiance) == null)
                            Add(Severity.Error, "world-allegiance", where, $"Undefined allegiance code {world.Allegiance} on {world.Name} {world.Hex}");
                    }
                }

                foreach (IAllegiance item in sector.Borders.AsEnumerable<IAllegiance>()
                    .Concat(sector.Routes)
                    .Concat(sector.Labels))
                {
                    if (!string.IsNullOrWhiteSpace(item.Allegiance) && sector.GetAllegianceFromCode(item.Allegiance!) == null)
                        Add(Severity.Error, "metadata-allegiance", where, $"Undefined allegiance code {item.Allegiance} on {item.GetType().Name}");
                }

                foreach (var route in sector.Routes)
                {
                    var startSector = sector.Location;
                    var endSector = sector.Location;
                    startSector.Offset(route.StartOffset);
                    endSector.Offset(route.EndOffset);
                    int distance = Astrometrics.HexDistance(
                        Astrometrics.LocationToCoordinates(new Location(startSector, route.Start)),
                        Astrometrics.LocationToCoordinates(new Location(endSector, route.End)));
                    if (distance == 0)
                        Add(Severity.Error, "route-length", where, $"Zero-length route: {route}");
                    else if (distance > 4)
                        Add(Severity.Warning, "route-length", where, $"Route length {distance}: {route}");
                }
            }
        }

        /// <summary>
        /// Validates XML files (sector metadata and milieu index files) against
        /// res/sectors.xsd.
        /// </summary>
        public void ValidateXml(IEnumerable<string> virtualPaths)
        {
            var settings = new XmlReaderSettings { ValidationType = ValidationType.Schema };
            settings.Schemas.Add(null, Util.MapPath("~/res/sectors.xsd"));

            foreach (var path in virtualPaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fileSettings = settings.Clone();
                fileSettings.ValidationEventHandler += (sender, e) =>
                    Add(e.Severity == XmlSeverityType.Error ? Severity.Error : Severity.Warning, "xml-schema", path,
                        $"line {e.Exception.LineNumber}: {e.Message}");
                try
                {
                    using var reader = XmlReader.Create(Util.MapPath(path), fileSettings);
                    while (reader.Read()) { }
                }
                catch (XmlException ex)
                {
                    Add(Severity.Error, "xml-syntax", path, ex.Message);
                }
            }
        }
    }
}
