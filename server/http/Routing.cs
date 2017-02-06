using System;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Routing;

namespace Maps.HTTP
{
    // From http://web.archive.org/web/20080401025712/http://www.iridescence.no/Posts/Defining-Routes-using-Regular-Expressions-in-ASPNET-MVC.aspx
    internal class RegexRoute : System.Web.Routing.Route
    {
        private readonly Regex regex;

        public RegexRoute(string pattern, IRouteHandler handler, RouteValueDictionary defaults = null, bool caseInsensitive = false)
            : base(null, defaults, handler)
        {
            RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture;
            if (caseInsensitive) options = options | RegexOptions.IgnoreCase;

            regex = new Regex("^" + pattern + "$", options);
        }

        public override RouteData GetRouteData(HttpContextBase context)
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

        IHttpHandler IRouteHandler.GetHttpHandler(RequestContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            IHttpHandler handler = Activator.CreateInstance(type) as IHttpHandler;

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
        private Regex replacer = new Regex(@"{(.*?)}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly string pattern;
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

            RouteValueDictionary dict = context.RouteData.Values;
            var url = replacer.Replace(pattern, new MatchEvaluator(m => dict[m.Groups[1].Value].ToString()));
            return new RedirectHandler(url, statusCode);
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

            bool IHttpHandler.IsReusable
            {
                // TODO: Why false here?
                get { return false; }
            }

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
