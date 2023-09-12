#nullable enable
using Maps.Search;
using Maps.Utilities;
using System;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace Maps.Admin
{
    internal abstract class AdminHandlerBase : Maps.HandlerBase, IHttpHandler
    {
        public static bool AdminAuthorized(HttpContext context)
        {
            if (context.Request.IsLocal)
                return true;

            if (!context.Request.IsSecureConnection)
                return false;

            if (context.Request["key"] == System.Configuration.ConfigurationManager.AppSettings["AdminKey"])
                return true;

            SendError(context.Response, 403, "Forbidden", "Access Denied");
            return false;
        }

        public bool IsReusable => true;
        public void ProcessRequest(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (!AdminAuthorized(context))
            {
                context.Response.TrySkipIisCustomErrors = true;
                context.Response.StatusCode = 403;
                context.Response.StatusDescription = "Forbidden";
                context.Response.ContentType = ContentTypes.Text.Plain;
                context.Response.Output.WriteLine("Incorrect secret or connection not secure.");
                context.Response.Flush();
                return;
            }
            Process(context, ResourceManager.GetInstance());
        }

        protected abstract void Process(HttpContext context, ResourceManager resourceManager);

        // TODO: Dedupe w/ DataResponder
        protected static string? GetStringOption(HttpContext context, string name)
        {
            if (context.Request[name] != null)
                return context.Request[name];
            var queryDefaults = Defaults(context);
            if (queryDefaults != null && queryDefaults.ContainsKey(name))
                return queryDefaults[name].ToString();
            return null;
        }

        protected static bool GetBoolOption(HttpContext context, string name, bool defaultValue = false)
        {
            if (context.Request[name] != null)
                return context.Request[name] == "1";
            // Check for "empty" query params; a list is returned.
            var empty = context.Request.QueryString[null];
            if (empty != null && empty.Split(',').Contains(name))
                return true;
            var queryDefaults = Defaults(context);
            if (queryDefaults != null && queryDefaults.ContainsKey(name))
                return queryDefaults[name].ToString() == "1";
            return defaultValue;
        }
    }

    internal class AdminHandler : AdminHandlerBase
    {
        protected override void Process(HttpContext context, ResourceManager resourceManager)
        {
            context.Server.ScriptTimeout = 3600; // An hour should be plenty
            context.Response.ContentType = ContentTypes.Text.Html;
            context.Response.BufferOutput = false;

            void WriteLine(string s) { context.Response.Write(s); context.Response.Write("\n"); }

            WriteLine("<!DOCTYPE html>");
            WriteLine("<title>Admin Page</title>");
            WriteLine("<style>");
            using (var reader = new StreamReader(context.Server.MapPath("~/site.css"), Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    WriteLine(reader.ReadLine());
                }
            }
            WriteLine("</style>");
            WriteLine("<h1>Traveller Map - Administration</h1>");
            context.Response.Flush();

            string? action = GetStringOption(context, "action");
            switch (action)
            {
                case "reindex": Reindex(context); return;
                case "flush": Flush(context); return;
                case "profile": Profile(context); return;
                case "uptime": Uptime(context); return;
            }
            Write(context.Response, "Unknown action: <pre>" + action + "</pre>");
            Write(context.Response, "<b>&Omega;</b>");
        }

        private static void Write(HttpResponse response, string line)
        {
            response.Write("<div>");
            response.Write(line);
            response.Write("</div>\n");
            response.Flush();
        }

        private static void Flush(HttpContext context)
        {
            SectorMap.Flush();

            var enumerator = context.Cache.GetEnumerator();
            while (enumerator.MoveNext())
            {
                context.Cache.Remove(enumerator.Key.ToString());
            }

            Write(context.Response, "Sector map flushed.");
            Write(context.Response, "<b>&Omega;</b>");
        }

        private static void WriteStat<T>(HttpResponse response, string name, T value) where T : notnull
        {
            Write(response, name + ": " + value.ToString());
        }
        private static void Uptime(HttpContext context)
        {
            TimeSpan uptime = DateTime.Now - Maps.GlobalAsax.startup_time;

            Write(context.Response, $"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s<br>");
            Write(context.Response, "<b>&Omega;</b>");
        }
        private static void Profile(HttpContext context)
        {
            WriteStat(context.Response, "Cache.Count", context.Cache.Count);
            WriteStat(context.Response, "Cache.EffectivePercentagePhysicalMemoryLimit", context.Cache.EffectivePercentagePhysicalMemoryLimit);
            WriteStat(context.Response, "Cache.EffectivePrivateBytesLimit", context.Cache.EffectivePrivateBytesLimit);
            var process = System.Diagnostics.Process.GetCurrentProcess();
            WriteStat(context.Response, "Process.Id", process.Id);
            WriteStat(context.Response, "Process.MinWorkingSet", process.MinWorkingSet);
            WriteStat(context.Response, "Process.MaxWorkingSet", process.MaxWorkingSet);
            WriteStat(context.Response, "Process.PeakWorkingSet64", process.PeakWorkingSet64);
            WriteStat(context.Response, "Process.PagedMemorySize64", process.PagedMemorySize64);
            WriteStat(context.Response, "Process.PeakPagedMemorySize64", process.PeakPagedMemorySize64);
            WriteStat(context.Response, "Process.PrivateMemorySize64", process.PrivateMemorySize64);
            WriteStat(context.Response, "Process.StartTime", process.StartTime);
            WriteStat(context.Response, "Process.VirtualMemorySize64", process.VirtualMemorySize64);
            WriteStat(context.Response, "Process.WorkingSet64", process.WorkingSet64);
            WriteStat(context.Response, "Process.Threads.Count", process.Threads.Count);
            Write(context.Response, "<b>&Omega;</b>");
        }

        private static void Reindex(HttpContext context)
        {
            Write(context.Response, "Initializing resource manager...");
            ResourceManager resourceManager = ResourceManager.GetDedicatedInstance();

            SearchEngine.PopulateDatabase(resourceManager, s => Write(context.Response, s));

            Write(context.Response, "&nbsp;");
            Write(context.Response, "Summary:");
            using (var connection = DBUtil.MakeConnection())
            {
                foreach (string table in new string[] { "sectors", "subsectors", "worlds", "labels" })
                {
                    string sql = $"SELECT COUNT(*) FROM {table}";
                    using var command = new SqlCommand(sql, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Write(context.Response, $"{table}: {reader.GetInt32(0)}");
                    }
                }

                {
                    Write(context.Response, "&nbsp;");
                    Write(context.Response, "Worlds by Milieu:");
                    string sql = $"SELECT milieu, COUNT(*) FROM worlds GROUP BY milieu ORDER BY milieu";
                    using var command = new SqlCommand(sql, connection);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Write(context.Response, $"{reader.GetString(0)} &mdash; {reader.GetInt32(1)}");
                    }
                }
            }

            Write(context.Response, "<b>&Omega;</b>");
        }
    }

}
