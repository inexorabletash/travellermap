using System;
using System.Web;

namespace Maps.HttpModules
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
            if (!string.IsNullOrWhiteSpace(footer))
                return;

            application.EndRequest += (Object source, EventArgs e) => {
                HttpContext context = application.Context;
                if (context.Request.IsLocal)
                    return;
                if (context.Request.Url.AbsolutePath.StartsWith("/admin/"))
                    return;
                if (context.Response.ContentType != System.Net.Mime.MediaTypeNames.Text.Html)
                    return;
                context.Response.Write(footer);
            };
        }

        public void Dispose() { }
    }

    // <modules>
    //   <add name="NoWWW" type="Maps.HttpModules.NoWWW" />
    // </modules>
    public class NoWWW : IHttpModule
    {
        public String ModuleName { get { return "NoWWW"; } }

        public void Init(HttpApplication application)
        {
            application.BeginRequest += (Object source, EventArgs e) => {
                HttpContext context = application.Context;
                if (context.Request.IsLocal)
                    return;
                Uri url = context.Request.Url;
                if (!url.Authority.StartsWith("www."))
                    return;
                context.Response.RedirectPermanent(url.Scheme + Uri.SchemeDelimiter + url.Authority.Substring(4) + url.PathAndQuery, true);
            };
        }

        public void Dispose() { }
    }

    // <modules>
    //   <add name="RequireSecureConnection" type="Maps.HttpModules.RequireSecureConnection" />
    // </modules>
    public class RequireSecureConnection : IHttpModule
    {
        public String ModuleName { get { return "RequireSecureConnection"; } }

        public void Init(HttpApplication application)
        {
            application.BeginRequest += (Object source, EventArgs e) => {
                HttpContext context = application.Context;
                if (context.Request.IsLocal)
                    return;
                if (context.Request.IsSecureConnection)
                    return;
                Uri url = context.Request.Url;
                context.Response.RedirectPermanent("https" + Uri.SchemeDelimiter + url.Authority + url.PathAndQuery, true);
            };
        }

        public void Dispose() { }
    }
}
