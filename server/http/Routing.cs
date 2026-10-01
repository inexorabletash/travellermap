#nullable enable
using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Routing;

namespace Maps.HTTP
{
    // From https://web.archive.org/web/20080401025712/http://www.iridescence.no/Posts/Defining-Routes-using-Regular-Expressions-in-ASPNET-MVC.aspx
    internal class RegexRoute : System.Web.Routing.Route
    {
        private readonly Regex regex;

        // Routes always match case-insensitively (e.g. /data/Spin/TAB).
        public RegexRoute(string pattern, IRouteHandler handler, RouteValueDictionary? defaults = null)
            : base(null, defaults, handler)
        {
            regex = new Regex("^" + pattern + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
        }

        public override RouteData? GetRouteData(HttpContextBase context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            Match match = regex.Match(context.Request.Path);
            if (!match.Success)
                return null;

            RouteData data = new RouteData(this, RouteHandler);

            if (Defaults != null)
            {
                foreach (var def in Defaults)
                    data.Values[def.Key] = def.Value;
            }

            foreach (var name in regex.GetGroupNames())
                data.Values[name] = match.Groups[name];

            return data;
        }
    }

    internal class GenericRouteHandler : IRouteHandler
    {
        private readonly Type type;

        public GenericRouteHandler(Type type)
        {
            this.type = type;
        }

        internal Type HandlerType => type;

        IHttpHandler? IRouteHandler.GetHttpHandler(RequestContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            IHttpHandler? handler = Activator.CreateInstance(type) as IHttpHandler;

            // Pass in RouteData
            // suggested by http://weblog.west-wind.com/posts/2011/Mar/28/Custom-ASPNET-Routing-to-an-HttpHandler
            context.HttpContext.Items["RouteData"] = context.RouteData;

            // Can be accessed in ProcessRequest via:
            // RouteData routeData = HttpContext.Current.Items["RouteData"] as RouteData;

            return handler;
        }
    }

    internal class RedirectRouteHandler : IRouteHandler
    {
        private static readonly Regex replacer = new Regex(@"{(.*?)}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string pattern;
        internal string Target => pattern;
        private readonly int statusCode;

        public RedirectRouteHandler(string target, int statusCode = 301)
        {
            pattern = target;
            this.statusCode = statusCode;
        }

        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            return new RedirectHandler(ExpandTarget(pattern, context.RouteData.Values), statusCode);
        }

        /// <summary>
        /// Substitutes {name} placeholders in a redirect target with route values. Values
        /// are URL-encoded since all current targets place them in the query string, and
        /// names may contain characters like '&amp;' or '#'.
        /// </summary>
        internal static string ExpandTarget(string pattern, RouteValueDictionary values)
        {
            return replacer.Replace(pattern, m => Uri.EscapeDataString(values[m.Groups[1].Value].ToString()));
        }

        private class RedirectHandler : IHttpHandler
        {
            private readonly string url;
            private readonly int statusCode;
            public RedirectHandler(string url, int statusCode)
            {
                this.url = url;
                this.statusCode = statusCode;
            }

            bool IHttpHandler.IsReusable => false;
            void IHttpHandler.ProcessRequest(HttpContext context)
            {
                if (context == null)
                    throw new ArgumentNullException(nameof(context));

                string target = url;
                if (context.Request.QueryString.Count > 0)
                    target += (target.Contains("?") ? "&" : "?") + context.Request.QueryString;
                context.Response.StatusCode = statusCode;
                context.Response.AddHeader("Location", target);
            }
        }
    }
}
