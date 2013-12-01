using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;

namespace Maps.Admin
{
    public abstract class AdminHandlerBase : Maps.HandlerBase, IHttpHandler
    {
        public static bool AdminAuthorized(HttpContext context)
        {
            if (context.Request.Url.Host == "localhost")
                return true;

            if (context.Request["key"] == System.Configuration.ConfigurationManager.AppSettings["AdminKey"])
                return true;

            SendError(context.Response, 403, "Access Denied", "Access Denied");
            return false;
        }

        bool IHttpHandler.IsReusable { get { return true; } }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            if (!AdminAuthorized(context))
                return;
            Process(context);
        }

        protected abstract void Process(HttpContext context);
    }

    public class AdminHandler : AdminHandlerBase
    {
        public override string DefaultContentType { get { return System.Net.Mime.MediaTypeNames.Text.Plain; } }

        protected override void Process(HttpContext context)
        {
            context.Server.ScriptTimeout = 3600; // An hour should be plenty
            context.Response.ContentType = System.Net.Mime.MediaTypeNames.Text.Html;
            context.Response.BufferOutput = false;

            context.Response.Write("<!DOCTYPE html>");
            context.Response.Write("<title>Admin Page</title>");
            context.Response.Write("<style>");
            using (var reader = new StreamReader(context.Server.MapPath("~/site.css"), Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    context.Response.Write(reader.ReadLine());
                }
            }
            context.Response.Write("</style>");
            context.Response.Write("<h1>Traveller Map - Administration</h1>");
            context.Response.Flush();

            string action = GetStringOption(context, "action");
            switch (action)
            {
                case "reindex": Reindex(context); return;
                case "flush": Flush(context); return;
                case "profile": Profile(context); return;
            }
            Write(context.Response, "Unknown action: <pre>" + action + "</pre>");
            Write(context.Response, "<b>&Omega;</b>");
        }

        private void Write(HttpResponse response, string line)
        {
            response.Write("<div>");
            response.Write(line);
            response.Write("</div>");
            response.Flush();
        }

        private void Flush(HttpContext context)
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

        private void WriteStat<T>(HttpResponse response, string name, T value)
        {
            Write(response, name + ": " + value.ToString());
        }

        private void Profile(HttpContext context)
        {
            WriteStat(context.Response, "Cache.Count", context.Cache.Count);
            WriteStat(context.Response, "Cache.EffectivePercentagePhysicalMemoryLimit", context.Cache.EffectivePercentagePhysicalMemoryLimit);
            WriteStat(context.Response, "Cache.EffectivePrivateBytesLimit", context.Cache.EffectivePrivateBytesLimit);
            var process = System.Diagnostics.Process.GetCurrentProcess();
            WriteStat(context.Response, "Process.MinWorkingSet", process.MinWorkingSet);
            WriteStat(context.Response, "Process.MaxWorkingSet", process.MaxWorkingSet);
            WriteStat(context.Response, "Process.PeakWorkingSet64", process.PeakWorkingSet64);
            WriteStat(context.Response, "Process.PagedMemorySize64", process.PagedMemorySize64);
            WriteStat(context.Response, "Process.PeakPagedMemorySize64", process.PeakPagedMemorySize64);
            WriteStat(context.Response, "Process.PrivateMemorySize64", process.PrivateMemorySize64);
            WriteStat(context.Response, "Process.VirtualMemorySize64", process.VirtualMemorySize64);
            WriteStat(context.Response, "Process.WorkingSet64", process.WorkingSet64);
            Write(context.Response, "<b>&Omega;</b>");
        }

        private void Reindex(HttpContext context)
        {

            Write(context.Response, "Initializing resource manager...");
            ResourceManager resourceManager = new ResourceManager(context.Server, context.Cache);

            SearchEngine.PopulateDatabase(resourceManager, s => Write(context.Response, s));

            Write(context.Response, "&nbsp;");
            Write(context.Response, "Summary:");
            using (var connection = DBUtil.MakeConnection())
            {
                foreach (string table in new string[] { "sectors", "subsectors", "worlds" })
                {
                    string sql = String.Format("SELECT COUNT(*) FROM {0}", table);
                    using (var command = new SqlCommand(sql, connection))
                    {
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                Write(context.Response, String.Format("{0}: {1}", table, reader.GetInt32(0)));
                            }
                        }
                    }
                }
            }

            Write(context.Response, "<b>&Omega;</b>");

        }
    }

}
