using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.IO;
using System.Text.RegularExpressions;

namespace Maps
{
    static class DBUtil
    {
        public static SqlConnection MakeConnection()
        {
            string connectionStringName = (HttpContext.Current.Request.Url.Host == "localhost") ? "SqlDev" : "SqlProd";
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
            SqlConnection conn = new SqlConnection(connectionString);
            conn.Open();
            return conn;
        }
    }



    /// <summary>
    /// Summary description for SearchEngine.
    /// </summary>
    public static class SearchEngine
    {
        [Flags]
        public enum SearchResultsType : int
        {
            Sectors = 0x0001,
            Subsectors = 0x0002,
            Worlds = 0x0004,
            UWP = 0x0008,
            Default = Sectors | Subsectors | Worlds // NOTE: doesn't include UWP
        }

        public delegate void StatusCallback(string status);

        public static void PopulateDatabase(ResourceManager resourceManager, StatusCallback callback)
        {
            // Lock on this class rather than the cacheResults. Tile requests are not
            // blocked but we don't index twice.
            lock (typeof(SearchEngine))
            {
                // NOTE: This (re)initializes a static data structure used for 
                // resolving names into sector locations, so needs to be run
                // before any other objects (e.g. Worlds) are loaded.
                SectorMap map = SectorMap.FromName(SectorMap.DefaultSetting, resourceManager);

                using (var connection = DBUtil.MakeConnection())
                {
                    SqlCommand sqlCommand;

                    //
                    // Repopulate the tables - locally first
                    // FUTURE: will need to batch this up rather than keep it in memory!
                    //

                    DataTable dt_sectors = new DataTable();
                    for (int i = 0; i < 3; ++i)
                        dt_sectors.Columns.Add(new DataColumn());

                    DataTable dt_subsectors = new DataTable();
                    for (int i = 0; i < 4; ++i)
                        dt_subsectors.Columns.Add(new DataColumn());

                    DataTable dt_worlds = new DataTable();
                    for (int i = 0; i < 7; ++i)
                        dt_worlds.Columns.Add(new DataColumn());

                    callback("Parsing data...");
                    foreach (Sector sector in map.Sectors)
                    {
                        if (!sector.Tags.Contains("OTU") && !sector.Tags.Contains("Faraway"))
                            continue;

                        foreach (Name name in sector.Names)
                        {
                            DataRow row = dt_sectors.NewRow();
                            row.ItemArray = new object[] { sector.X, sector.Y, name.Text };
                            dt_sectors.Rows.Add(row);
                        }


                        foreach (Subsector subsector in sector.Subsectors)
                        {
                            DataRow row = dt_subsectors.NewRow();
                            row.ItemArray = new object[] { sector.X, sector.Y, subsector.Index, subsector.Name };
                            dt_subsectors.Rows.Add(row);
                        }

#if DEBUG
                        if (!sector.Selected)
                            continue;
#endif
                        // NOTE: May need to page this at some point
                        WorldCollection worlds = sector.GetWorlds(resourceManager, cacheResults: false);
                        if (worlds == null)
                            continue;

                        var world_query = from world in worlds
                                          where !world.IsPlaceholder
                                          select world;
                        foreach (World world in worlds)
                        {
                            DataRow row = dt_worlds.NewRow();
                            row.ItemArray = new object[] { 
                                    sector.X, 
                                    sector.Y, 
                                    world.X, 
                                    world.Y, 
                                    world.Name != null && world.Name.Length > 0
                                        ? (object)world.Name
                                        : (object)DBNull.Value,
                                    world.UWP,
                                    sector.Names.Count > 0 ? (object)sector.Names[0] : (object)DBNull.Value
                            };

                            dt_worlds.Rows.Add(row);
                        }
                    }


                    //
                    // Rebuild the tables with fresh schema
                    //

                    const string INDEX_OPTIONS = " WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON)";

                    string[] rebuild_schema = {
                        "IF EXISTS(SELECT 1 FROM sys.objects WHERE OBJECT_ID = OBJECT_ID(N'sectors') AND type = (N'U')) DROP TABLE sectors",
                        "CREATE TABLE sectors (x int NOT NULL, y int NOT NULL, name nvarchar(50) NULL)",
                        "CREATE NONCLUSTERED INDEX sector_name ON sectors ( name ASC )" + INDEX_OPTIONS,

                        "IF EXISTS(SELECT 1 FROM sys.objects WHERE OBJECT_ID = OBJECT_ID(N'subsectors') AND type = (N'U')) DROP TABLE subsectors",
                        "CREATE TABLE subsectors (sector_x int NOT NULL, sector_y int NOT NULL, subsector_index char(1) NOT NULL, name nvarchar(50) NULL)",
                        "CREATE NONCLUSTERED INDEX subsector_name ON subsectors ( name ASC )" + INDEX_OPTIONS,

                        "IF EXISTS(SELECT 1 FROM sys.objects WHERE OBJECT_ID = OBJECT_ID(N'worlds') AND type = (N'U')) DROP TABLE worlds",
                        "CREATE TABLE worlds (sector_x int NOT NULL, sector_y int NOT NULL, hex_x int NOT NULL, hex_y int NOT NULL, name nvarchar(50) NULL, uwp nchar(9) NULL, sector_name nvarchar(50) NULL)",
                        "CREATE NONCLUSTERED INDEX world_name ON worlds ( name ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_uwp ON worlds ( uwp ASC )" + INDEX_OPTIONS,
                        "CREATE NONCLUSTERED INDEX world_sector_name ON worlds ( sector_name ASC )" + INDEX_OPTIONS,
                    };

                    callback("Rebuilding schema...");
                    foreach (string cmd in rebuild_schema)
                    {
                        sqlCommand = new SqlCommand(cmd, connection);
                        sqlCommand.ExecuteNonQuery();
                    }

                    //
                    // And shovel the data into the database en masse
                    //
                    using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
                    {
                        callback(String.Format("Writing {0} sectors...", dt_sectors.Rows.Count));
                        bulk.BatchSize = dt_sectors.Rows.Count;
                        bulk.DestinationTableName = "sectors";
                        bulk.WriteToServer(dt_sectors);
                        bulk.Close();
                    }
                    using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
                    {
                        callback(String.Format("Writing {0} subsectors...", dt_subsectors.Rows.Count));
                        bulk.BatchSize = dt_subsectors.Rows.Count;
                        bulk.DestinationTableName = "subsectors";
                        bulk.WriteToServer(dt_subsectors);
                        bulk.Close();
                    }
                    using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null))
                    {
                        callback(String.Format("Writing {0} worlds...", dt_worlds.Rows.Count));
                        bulk.BatchSize = 4096;
                        bulk.DestinationTableName = "worlds";
                        bulk.WriteToServer(dt_worlds);
                        bulk.Close();
                    }
                }
                callback("Complete!");
            }
        }

        public static IEnumerable<ItemLocation> PerformSearch(string query, ResourceManager resourceManager, SearchResultsType types, int maxResultsPerType)
        {
            List<ItemLocation> results = new List<ItemLocation>();

            List<string> clauses;
            List<string> terms;
            types = ParseQuery(query, types, out clauses, out terms);

            string where = String.Join(" AND ", clauses.ToArray());

            // NOTE: DISTINCT is to filter out "Ley" and "Ley Sector" (different names, same result). 
            // TODO: Include the searched-for name in the results, and show alternate names in the result set.
            // {0} is the list of distinct fields (i.e. coordinates), {1} is the list of fields in the subquery (same as {0} but with "name" added, {2} is the table, {3} is the filter
            // Since we need the distinct values from the term {0} but don't use the name for the results construction, we can ignore name in the resultset.
            // This allows us to get the top N results from the database, sort by name, and then toss out duplicates not based on name but on the other, used, columns
            // Here's a sample subquery that works for the sector table.
            // SELECT DISTINCT TOP 160 tt.x, tt.y FROM (SELECT TOP 160 x, y,name FROM sectors WHERE (name LIKE 'LEY%' OR name LIKE '%LEY%') ORDER BY name ASC) AS tt;
            string query_format = "SELECT DISTINCT TOP " + maxResultsPerType + " {0} FROM (SELECT TOP " + maxResultsPerType + " {1} FROM {2} WHERE {3}) AS TT";

            using (var connection = DBUtil.MakeConnection())
            {
                // Sectors
                if (types.HasFlag(SearchResultsType.Sectors))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = String.Format(query_format, "TT.x, TT.y", "x, y", "sectors", where);
                    using (var sqlCommand = new SqlCommand(sql, connection))
                    {
                        for (int i = 0; i < terms.Count; ++i)
                            sqlCommand.Parameters.AddWithValue(String.Format("@term{0}", i), terms[i]);

                        using (var row = sqlCommand.ExecuteReader())
                        {
                            while (row.Read())
                            {
                                results.Add(new SectorLocation(row.GetInt32(0), row.GetInt32(1)));
                            }
                        }
                    }
                }

                // Subsectors
                if (types.HasFlag(SearchResultsType.Subsectors))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = String.Format(query_format, "TT.sector_x, TT.sector_y, TT.subsector_index", "sector_x, sector_y, subsector_index", "subsectors", where);
                    using (var sqlCommand = new SqlCommand(sql, connection))
                    {
                        for (int i = 0; i < terms.Count; ++i)
                            sqlCommand.Parameters.AddWithValue(String.Format("@term{0}", i), terms[i]);

                        using (var row = sqlCommand.ExecuteReader())
                        {
                            while (row.Read())
                            {
                                char[] chars = new char[1];
                                row.GetChars(2, 0, chars, 0, chars.Length);

                                results.Add(new SubsectorLocation(row.GetInt32(0), row.GetInt32(1), chars[0]));
                            }
                        }
                    }
                }

                // Worlds & UWPs
                if ((types.HasFlag(SearchResultsType.Worlds) || types.HasFlag(SearchResultsType.UWP)))
                {
                    // Note duplicated field names so the results of both queries can come out right.
                    string sql = String.Format(query_format, "TT.sector_x, TT.sector_y, TT.hex_x, TT.hex_y", "sector_x, sector_y, hex_x, hex_y", "worlds", where);
                    using (var sqlCommand = new SqlCommand(sql, connection))
                    {
                        for (int i = 0; i < terms.Count; ++i)
                            sqlCommand.Parameters.AddWithValue(String.Format("@term{0}", i), terms[i]);

                        using (var row = sqlCommand.ExecuteReader())
                        {
                            while (row.Read())
                                results.Add(new WorldLocation(row.GetInt32(0), row.GetInt32(1), row.GetInt32(2), row.GetInt32(3)));
                        }
                    }
                }
            }
            return results;
        }

        private static readonly string[] OPS = { "uwp:", "exact:", "like:", "in:" };
        private static readonly Regex RE_TERMS = new Regex("(" + String.Join("|", OPS) + ")?(\"[^\"]+\"|\\S+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static IEnumerable<string> ParseTerms(string q)
        {
            return RE_TERMS.Matches(q).Cast<Match>().Select(m => m.Value).Where(s => !String.IsNullOrWhiteSpace(s));
        }



        private static SearchResultsType ParseQuery(string query, SearchResultsType types, out List<string> clauses, out List<string> terms)
        {
            clauses = new List<string>();
            terms = new List<string>();
            foreach (string t in ParseTerms(query))
            {
                string term = t;
                string op = null;
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
                    types = SearchResultsType.UWP;
                }
                else if (op == "exact:")
                {
                    clause = "name LIKE @term";
                }
                else if (op == "like:")
                {
                    clause = "SOUNDEX(name) = SOUNDEX(@term)";
                }
                else if (op == "in:")
                {
                    clause = "sector_name LIKE @term + '%'";
                }
                else if (quoted)
                {
                    clause = "name LIKE @term";
                }
                else if (term.Contains("%") || term.Contains("_"))
                {
                    clause = "name LIKE @term";
                }
                else
                {
                    clause = "name LIKE @term + '%' OR name LIKE '% ' + @term + '%'";
                }

                clause = clause.Replace("@term", String.Format("@term{0}", terms.Count));
                clauses.Add("(" + clause + ")");
                terms.Add(term);
            }
            return types;
        }
    }
}
