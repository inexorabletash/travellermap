#nullable enable
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Web;
using System.Text.RegularExpressions;

namespace Maps.Search
{
    static class DBUtil
    {
        public static SqlConnection MakeConnection()
        {
            string connectionStringName = HttpContext.Current.Request.IsLocal ? "SqlDev" : "SqlProd";
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            return conn;
        }
    }

    /// <summary>
    /// Summary description for SearchEngine.
    /// </summary>
    internal static class SearchEngine
    {
        [Flags]
        public enum SearchResultsType : int
        {
            Sectors = 1 << 0,
            Subsectors = 1 << 1,
            Worlds = 1 << 2,
            Labels = 1 << 3,
            Default = Sectors | Subsectors | Worlds | Labels
        }

        private static readonly object s_lock = new object();

        private static string SanifyLabel(string s) => Regex.Replace(s.Trim(), @"\s+", " ");

        private static readonly string[] SECTORS_COLUMNS = {
            "milieu nvarchar(12) NULL",
            "x int NOT NULL",
            "y int NOT NULL",
            "name nvarchar(50) NULL"
        };
        private static readonly string[] SUBSECTORS_COLUMNS = {
            "milieu nvarchar(12) NULL",
            "sector_x int NOT NULL",
            "sector_y int NOT NULL",
            "subsector_index char(1) NOT NULL",
            "name nvarchar(50) NULL"
        };
        private static readonly string[] WORLDS_COLUMNS = {
            "milieu nvarchar(12) NULL",
            "x int NOT NULL",
            "y int NOT NULL",
            "sector_x int NOT NULL",
            "sector_y int NOT NULL",
            "hex_x int NOT NULL",
            "hex_y int NOT NULL",
            "name nvarchar(50) NULL",
            "uwp nchar(9) NULL",
            "remarks nvarchar(50) NULL",
            "pbg nchar(3) NULL",
            "zone nchar(1) NULL",
            "alleg nchar(4) NULL",
            "ix int NOT NULL",
            "ex nchar(5) NULL",
            "cx nchar(4) NULL",
            "sector_name nvarchar(50) NULL"
        };
        private static readonly string[] LABELS_COLUMNS = {
            "milieu nvarchar(12) NULL",
            "x int NOT NULL",
            "y int NOT NULL",
            "radius int NOT NULL",
            "name nvarchar(100) NULL"
        };

        public static void PopulateDatabase(ResourceManager resourceManager, Action<string> statusCallback)
        {
            // Lock to prevent indexing twice, without blocking tile requests.
            lock (SearchEngine.s_lock)
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap map = SectorMap.GetInstance(resourceManager);

                using (var connection = DBUtil.MakeConnection())
                {
                    SqlCommand sqlCommand;

                    //
                    // Repopulate the tables - locally first
                    // FUTURE: will need to batch this up rather than keep it in memory!
                    //

                    DataTable dt_sectors = new DataTable();
                    for (int i = 0; i < SECTORS_COLUMNS.Length; ++i)
                        dt_sectors.Columns.Add(new DataColumn());

                    DataTable dt_subsectors = new DataTable();
                    for (int i = 0; i < SUBSECTORS_COLUMNS.Length; ++i)
                        dt_subsectors.Columns.Add(new DataColumn());

                    DataTable dt_worlds = new DataTable();
                    for (int i = 0; i < WORLDS_COLUMNS.Length; ++i)
                        dt_worlds.Columns.Add(new DataColumn());

                    DataTable dt_labels = new DataTable();
                    for (int i = 0; i < LABELS_COLUMNS.Length; ++i)
                        dt_labels.Columns.Add(new DataColumn());

                    // Map of (milieu, string) => [ points ... ]
                    Dictionary<Tuple<string, string>, List<Point>> labels = new Dictionary<Tuple<string, string>, List<Point>>();
                    void AddLabel(string milieu, string text, Point coords)
                    {
                        if (text == null) return;
                        text = SanifyLabel(text);
                        var key = Tuple.Create(milieu, text);
                        if (!labels.ContainsKey(key))
                            labels.Add(key, new List<Point>());
                        labels[key].Add(coords);
                    }

                    statusCallback("Parsing data...");
                    foreach (Sector sector in map.Sectors)
                    {
                        // TODO: Allow searching non-OTU additions (Orion OB-1, etc)
                        if (!sector.Tags.Contains("OTU") && !sector.Tags.Contains("Faraway"))
                            continue;

                        // Apply suffix to borders/labels so that alternate versions are not merged.
                        string suffix = sector.Tags.Contains("Apocryphal") ? " (Apocryphal)" :
                            sector.Tags.Contains("Alternate") ? " (Alternate)" : "";

                        foreach (Name name in sector.Names)
                        {
                            DataRow row = dt_sectors.NewRow();
                            row.ItemArray = new object[] { sector.CanonicalMilieu, sector.X, sector.Y, name.Text };
                            dt_sectors.Rows.Add(row);
                        }
                        if (!string.IsNullOrEmpty(sector.Abbreviation))
                        {
                            DataRow row = dt_sectors.NewRow();
                            row.ItemArray = new object[] { sector.CanonicalMilieu, sector.X, sector.Y, sector.Abbreviation! };
                            dt_sectors.Rows.Add(row);
                        }

                        foreach (Subsector subsector in sector.Subsectors)
                        {
                            DataRow row = dt_subsectors.NewRow();
                            row.ItemArray = new object[] { sector.CanonicalMilieu, sector.X, sector.Y, subsector.Index, subsector.Name };
                            dt_subsectors.Rows.Add(row);
                        }

                        foreach (Border border in sector.BordersAndRegions.Where(b => b.ShowLabel))
                        {
                            AddLabel(
                                sector.CanonicalMilieu,
                                border.GetLabel(sector) + suffix,
                                Astrometrics.LocationToCoordinates(new Location(sector.Location, border.LabelPosition)));
                        }

                        foreach (Label label in sector.Labels)
                        {
                            AddLabel(
                                sector.CanonicalMilieu,
                                label.Text + suffix,
                                Astrometrics.LocationToCoordinates(new Location(sector.Location, label.Hex)));
                        }

#if DEBUG
                        if (!sector.Selected)
                            continue;
#endif
                        // NOTE: May need to page this at some point
                        WorldCollection? worlds = sector.GetWorlds(resourceManager, cacheResults: false);
                        if (worlds == null)
                            continue;

                        var world_query = from world in worlds
                                          where !world.IsPlaceholder
                                          select world;
                        foreach (World world in world_query)
                        {
                            DataRow row = dt_worlds.NewRow();
                            row.ItemArray = new object[] {
                                    sector.CanonicalMilieu,
                                    world.Coordinates.X,
                                    world.Coordinates.Y,
                                    sector.X,
                                    sector.Y,
                                    world.X,
                                    world.Y,
                                    string.IsNullOrEmpty(world.Name) ? (object)DBNull.Value : (object)world.Name,
                                    world.UWP,
                                    world.Remarks,
                                    world.PBG,
                                    string.IsNullOrEmpty(world.Zone) ? "G" : world.Zone,
                                    world.Allegiance,
                                    world.CalculatedImportance,
                                    StripBrackets(world.Economic) ?? (object)DBNull.Value,
                                    StripBrackets(world.Cultural) ?? (object)DBNull.Value,
                                    sector.Names.Count > 0 ? (object)sector.Names[0] : (object)DBNull.Value
                            };

                            dt_worlds.Rows.Add(row);
                        }
                    }

                    foreach (KeyValuePair<Tuple<string, string>, List<Point>> entry in labels)
                    {
                        var (milieu, name) = entry.Key;
                        List<Point> points = entry.Value;

                        Point avg = new Point(
                            (int)Math.Round(points.Select(p => p.X).Average()),
                            (int)Math.Round(points.Select(p => p.Y).Average()));
                        Point min = new Point(points.Select(p => p.X).Min(), points.Select(p => p.Y).Min());
                        Point max = new Point(points.Select(p => p.X).Max(), points.Select(p => p.Y).Max());
                        Size size = new Size(max.X - min.X, max.Y - min.Y);
                        int radius = Math.Max(size.Width, size.Height);

                        DataRow row = dt_labels.NewRow();
                        row.ItemArray = new object[] {
                            milieu,
                            avg.X,
                            avg.Y,
                            radius,
                            name
                        };
                        dt_labels.Rows.Add(row);
                    }

                    //
                    // Rebuild the tables with fresh schema
                    //

                    const string INDEX_OPTIONS = " WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)";

                    const string DROP_TABLE_IF_EXISTS = "IF EXISTS(SELECT 1 FROM sys.objects WHERE OBJECT_ID = OBJECT_ID(N'{0}') AND type = (N'U')) DROP TABLE {0}";

                    string[] rebuild_schema = {
                        string.Format(DROP_TABLE_IF_EXISTS, "sectors"),
                        "CREATE TABLE sectors (" + string.Join(",", SECTORS_COLUMNS) + ")",
                        "CREATE NONCLUSTERED INDEX sector_name ON sectors ( name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX sector_milieu ON sectors ( milieu ASC )" + INDEX_OPTIONS,

                        string.Format(DROP_TABLE_IF_EXISTS, "subsectors"),
                        "CREATE TABLE subsectors (" + string.Join(",", SUBSECTORS_COLUMNS) + ")",
                        "CREATE NONCLUSTERED INDEX subsector_name ON subsectors ( name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX subsector_milieu ON subsectors ( milieu ASC )" + INDEX_OPTIONS,

                        string.Format(DROP_TABLE_IF_EXISTS, "worlds"),
                        "CREATE TABLE worlds (" + string.Join(",", WORLDS_COLUMNS) + ")",
                        "CREATE NONCLUSTERED INDEX world_name ON worlds ( name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_uwp ON worlds ( uwp ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_pbg ON worlds ( pbg ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_alleg ON worlds ( alleg ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_sector_name ON worlds ( sector_name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_milieu ON worlds ( milieu ASC )" + INDEX_OPTIONS,

                        string.Format(DROP_TABLE_IF_EXISTS, "labels"),
                        "CREATE TABLE labels (" + string.Join(",", LABELS_COLUMNS) + ")",
                        "CREATE NONCLUSTERED INDEX name ON labels ( name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX milieu ON labels ( milieu ASC )" + INDEX_OPTIONS,
                    };

                    statusCallback("Rebuilding schema...");
                    foreach (string cmd in rebuild_schema)
                    {
                        sqlCommand = new SqlCommand(cmd, connection);
                        sqlCommand.ExecuteNonQuery();
                    }

                    //
                    // And shovel the data into the database en masse
                    //
                    void BulkInsert(string name, DataTable table, int batchSize)
                    {
                        using var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null);
                        statusCallback($"Writing {table.Rows.Count} {name}...");
                        bulk.BatchSize = batchSize;
                        bulk.DestinationTableName = name;
                        bulk.WriteToServer(table);
                    }

                    BulkInsert("sectors", dt_sectors, dt_sectors.Rows.Count);
                    BulkInsert("subsectors", dt_subsectors, dt_subsectors.Rows.Count);
                    BulkInsert("worlds", dt_worlds, 4096);
                    BulkInsert("labels", dt_labels, dt_labels.Rows.Count);
                }
                statusCallback("Complete!");
            }
        }

        public static IEnumerable<SearchResult> PerformSearch(string? milieu, string? query, SearchResultsType types, int maxResultsPerType, bool random = false)
        {
            List<SearchResult> results = new List<SearchResult>();

            types = ParseQuery(query, types, out var clauses, out var terms);

            if (clauses.Count() == 0 && !random)
                return results;

            clauses.Insert(0, "milieu = @term");
            terms.Insert(0, milieu ?? SectorMap.DEFAULT_MILIEU);

            string where = string.Join(" AND ",
                clauses.Select((clause, index) => "(" + clause.Replace("@term", $"@term{index}") + ")"));

            string orderBy = random ? "ORDER BY NEWID()" : "";

            // NOTE: DISTINCT is to filter out "Ley" and "Ley Sector" (different names, same result). 
            // * {0} is the list of distinct fields (i.e. coordinates), 
            // * {1} is the list of fields in the subquery (same as {0} but with "name" added,
            // * {2} is the table
            // * {3} is the filter (WHERE clause, not including "WHERE")
            // * {4} is the order (ORDERBY clause)
            // * {5} is the limit (TOP clause, not including "TOP")
            // Since we need the distinct values from the term {0} but don't use the name for the results construction,
            // we can ignore name in the resultset. This allows us to get the top N results from the database, sort by
            // name, and then toss out duplicates not based on name but on the other, used, columns. Here's a sample 
            // subquery that works for the sector table:
            //   SELECT DISTINCT TOP 160 tt.x, tt.y 
            //   FROM (SELECT TOP 160 x, y,name
            //         FROM sectors
            //         WHERE (name LIKE 'LEY%' OR name LIKE '%LEY%')
            //         ORDER BY name ASC) AS tt;
            // TODO: Include the searched-for name in the results, and show alternate names in the result set.
            string query_format = "SELECT DISTINCT TOP {5} {0} FROM (SELECT TOP {5} {1} FROM {2} WHERE {3} {4}) AS TT";

            using (var connection = DBUtil.MakeConnection())
            {
                // Sectors
                if (types.HasFlag(SearchResultsType.Sectors))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = string.Format(query_format, "TT.x, TT.y", "x, y", "sectors", where, orderBy, maxResultsPerType);
                    using var sqlCommand = new SqlCommand(sql, connection);
                    for (int i = 0; i < terms.Count; ++i)
                        sqlCommand.Parameters.AddWithValue($"@term{i}", terms[i]);

                    using var row = sqlCommand.ExecuteReader();
                    while (row.Read())
                    {
                        results.Add(new SectorResult(row.GetInt32(0), row.GetInt32(1)));
                    }
                }

                // Subsectors
                if (types.HasFlag(SearchResultsType.Subsectors))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = string.Format(query_format, "TT.sector_x, TT.sector_y, TT.subsector_index", "sector_x, sector_y, subsector_index", "subsectors", where, orderBy, maxResultsPerType);
                    using var sqlCommand = new SqlCommand(sql, connection);
                    for (int i = 0; i < terms.Count; ++i)
                        sqlCommand.Parameters.AddWithValue($"@term{i}", terms[i]);

                    using var row = sqlCommand.ExecuteReader();
                    while (row.Read())
                    {
                        char[] chars = new char[1];
                        row.GetChars(2, 0, chars, 0, chars.Length);

                        results.Add(new SubsectorResult(row.GetInt32(0), row.GetInt32(1), chars[0]));
                    }
                }

                // Worlds & UWPs, etc
                if (types.HasFlag(SearchResultsType.Worlds))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = string.Format(query_format, "TT.sector_x, TT.sector_y, TT.hex_x, TT.hex_y", "sector_x, sector_y, hex_x, hex_y", "worlds", where, orderBy, maxResultsPerType);
                    using var sqlCommand = new SqlCommand(sql, connection);
                    for (int i = 0; i < terms.Count; ++i)
                        sqlCommand.Parameters.AddWithValue($"@term{i}", terms[i]);

                    using var row = sqlCommand.ExecuteReader();
                    while (row.Read())
                        results.Add(new WorldResult(row.GetInt32(0), row.GetInt32(1), (byte)row.GetInt32(2), (byte)row.GetInt32(3)));
                }

                // Labels
                if (types.HasFlag(SearchResultsType.Labels))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = string.Format(query_format, "TT.x, TT.y, TT.radius, TT.name", "x, y, radius, name", "labels", where, orderBy, maxResultsPerType);
                    using var sqlCommand = new SqlCommand(sql, connection);
                    for (int i = 0; i < terms.Count; ++i)
                        sqlCommand.Parameters.AddWithValue($"@term{i}", terms[i]);

                    using var row = sqlCommand.ExecuteReader();
                    while (row.Read())
                    {
                        results.Add(new LabelResult(row.GetString(3), new Point(row.GetInt32(0), row.GetInt32(1)), row.GetInt32(2)));
                    }
                }
            }
            return results;
        }

        private static readonly string[] OPS = {
                                                   "uwp:",
                                                   "pbg:",
                                                   "zone:",
                                                   "alleg:",
                                                   "remark:",
                                                   "exact:",
                                                   "like:",
                                                   "in:",
                                                   "ix:", "ex:", "cx:"
                                               };
        private static readonly Regex RE_TERMS = new Regex("(" + string.Join("|", OPS) + ")?(\"[^\"]+\"|\\S+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static IEnumerable<string> ParseTerms(string q) =>
            RE_TERMS.Matches(q).Cast<Match>().Select(m => m.Value).Where(s => !string.IsNullOrWhiteSpace(s));

        public static WorldResult? FindNearestWorldMatch(string name, string milieu, int x, int y)
        {
            const string sql = "SELECT sector_x, sector_y, hex_x, hex_y, " +
                "((@x - x) * (@x - x) + (@y - y) * (@y - y)) AS distance " +
                "FROM worlds " +
                "WHERE name = @name AND milieu = @milieu " +
                "ORDER BY distance ASC";

            milieu ??= SectorMap.DEFAULT_MILIEU;

            using var connection = DBUtil.MakeConnection();
            using var sqlCommand = new SqlCommand(sql, connection);
            sqlCommand.Parameters.AddWithValue("@x", x);
            sqlCommand.Parameters.AddWithValue("@y", y);
            sqlCommand.Parameters.AddWithValue("@milieu", milieu);
            sqlCommand.Parameters.AddWithValue("@name", name);
            using var row = sqlCommand.ExecuteReader();
            if (!row.Read())
                return null;
            return new WorldResult(row.GetInt32(0), row.GetInt32(1), (byte)row.GetInt32(2), (byte)row.GetInt32(3));
        }

        private static Regex SECTOR_HEX_REGEX = new Regex(@"^(?<sector>[A-Za-z0-9!' ]{3,}) (?<hex>\d{4})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static SearchResultsType ParseQuery(string? query, SearchResultsType types, out List<string> clauses, out List<string> terms)
        {
            clauses = new List<string>();
            terms = new List<string>();
            if (string.IsNullOrWhiteSpace(query))
                return types;
            query = query!.Trim().ToLowerInvariant();

            Match m = SECTOR_HEX_REGEX.Match(query);
            if (m.Success)
            {
                int hex = int.Parse(m.Groups["hex"].Value);
                clauses.Add("sector_name LIKE @term + '%'");
                terms.Add(m.Groups["sector"].Value);
                clauses.Add("hex_x = @term");
                terms.Add((hex / 100).ToString());
                clauses.Add("hex_y = @term");
                terms.Add((hex % 100).ToString());
                return SearchResultsType.Worlds;
            }

            foreach (string t in ParseTerms(query))
            {
                string term = t;
                string? op = null;
                bool quoted = false;

                foreach (var o in OPS)
                {
                    if (term.StartsWith(o))
                    {
                        op = o;
                        term = term.Substring(o.Length);
                        break;
                    }
                }

                // Infer a trailing "
                if (term.StartsWith("\"") && (!term.EndsWith("\"") || term.Length == 1))
                    term += '"';
                if (term.Length >= 2 && term.StartsWith("\"") && term.EndsWith("\""))
                {
                    quoted = true;
                    term = term.Substring(1, term.Length - 2);
                }
                if (term.Length == 0)
                    continue;

                string clause;
                if (op == "uwp:")
                {
                    clause = "uwp LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "pbg:")
                {
                    clause = "pbg LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "ix:")
                {
                    clause = "ix = @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "ex:")
                {
                    clause = "ex LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "cx:")
                {
                    clause = "cx LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "zone:")
                {
                    clause = "zone LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "alleg:")
                {
                    clause = "alleg LIKE @term";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "remark:")
                {
                    clause = "' ' + remarks + ' ' LIKE '% ' + @term + ' %'";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "in:")
                {
                    clause = "sector_name LIKE @term + '%'";
                    types = SearchResultsType.Worlds;
                }
                else if (op == "exact:")
                {
                    clause = "name LIKE @term";
                }
                else if (op == "like:")
                {
                    clause = "SOUNDEX(name) = SOUNDEX(@term)";
                }
                else if (quoted)
                {
                    clause = "name LIKE @term";
                }
                else if (term.Contains("%") || term.Contains("_"))
                {
                    clause = "name LIKE @term";
                }
                else if (term.Equals("sector", StringComparison.InvariantCultureIgnoreCase))
                {
                    types = SearchResultsType.Sectors;
                    continue;
                }
                else if (term.Equals("subsector", StringComparison.InvariantCultureIgnoreCase))
                {
                    types = SearchResultsType.Subsectors;
                    continue;
                }
                else if (term.Equals("world", StringComparison.InvariantCultureIgnoreCase))
                {
                    types = SearchResultsType.Worlds;
                    continue;
                }
                else
                {
                    clause = "name LIKE @term + '%' OR name LIKE '% ' + @term + '%'";
                }

                clauses.Add(clause);
                terms.Add(term);
            }
            return types;
        }

        static string? StripBrackets(string? input)
        {
            if (input == null)
                return null;
            return String.Join("", input.Split('(', ')', '[', ']', '{', '}'));
        }
    }
}
