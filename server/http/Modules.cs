using System;
using System.Web;

namespace Maps.HTTP
{
    // <modules>
    //   <add name="PageFooter" type="Maps.HttpModules.PageFooter" />
    // </modules>
    // <appSettings>
    //   <add key="PageFooter" value="your footer here..." />
    // </appSettings>
    public class PageFooter : IHttpModule
    {
        public String ModuleName { get { return "PageFooter"; } }

        public void Init(HttpApplication application)
        {
            string footer = System.Configuration.ConfigurationManager.AppSettings["PageFooter"];
            if (string.IsNullOrWhiteSpace(footer))
                return;

            application.EndRequest += (Object source, EventArgs e) => {
                HttpContext context = application.Context;
                if (context.Request.IsLocal)
                    return;
                if (context.Response.ContentType != System.Net.Mime.MediaTypeNames.Text.Html)
                    return;
                if (context.Request.Url.AbsolutePath.StartsWith("/admin/"))
                    return;
                context.Response.Write(footer);
            };
        }

        public void Dispose() { }
    }
}
