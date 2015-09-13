#define LEGACY_STYLES

using Maps.Rendering;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using System.Web.Routing;

namespace Maps
{
    internal abstract class HandlerBase
    {
        // TODO: Enforce verbs (i.e. GET or POST)

        protected static void ParseOptions(HttpContext context, ref MapOptions options, ref Stylesheet.Style style)
        {
            ParseOptions(context.Request, Defaults(context), ref options, ref style);
        }

        private static Dictionary<string, Stylesheet.Style> s_nameToStyle = new Dictionary<string, Stylesheet.Style>() {
            { "poster",Stylesheet.Style.Poster },
            { "atlas" ,Stylesheet.Style.Atlas },
            { "print" , Stylesheet.Style.Print },
            { "candy" ,Stylesheet.Style.Candy },
            { "draft" ,Stylesheet.Style.Draft },
            { "fasa"  ,Stylesheet.Style.FASA },
        };

        public static void ParseOptions(HttpRequest request, IDictionary<string, object> queryDefaults, ref MapOptions options, ref Stylesheet.Style style)
        {
            options = (MapOptions)GetIntOption(request, "options", queryDefaults, (int)options);

#if LEGACY_STYLES
            // Handle deprecated/legacy options bits for selecting style
            style =
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.PrintStyleDeprecated ? Stylesheet.Style.Atlas :
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.CandyStyleDeprecated ? Stylesheet.Style.Candy :
                Stylesheet.Style.Poster;
#endif // LEGACY_STYLES

            if (HasOption(request, "style", queryDefaults))
            {
                try
                {
                    style = s_nameToStyle[GetStringOption(request, "style", queryDefaults).ToLowerInvariant()];
                }
                catch (KeyNotFoundException)
                {
                    // TODO: Report error?
                }
            }
        }

        private static RouteValueDictionary Defaults(HttpContext context)
        {
            RouteData data = context.Items["RouteData"] as RouteData;
            if (data == null)
                throw new ApplicationException("RouteData not assigned by RouteHandler");
            return data.Values;
        }

        protected static bool HasOption(HttpContext context, string name)
        {
            return HasOption(context.Request, name, Defaults(context));
        }
        public static bool HasOption(HttpRequest request, string name, IDictionary<string, object> queryDefaults)
        {
           return request[name] != null || (queryDefaults != null && queryDefaults.ContainsKey(name));
        }

        protected static string GetStringOption(HttpContext context, string name, string defaultValue = null)
        {
            return GetStringOption(context.Request, name, Defaults(context), defaultValue);
        }
        public static string GetStringOption(HttpRequest request, string name, IDictionary<string, object> queryDefaults, string defaultValue = null)
        {
            if (request[name] != null)
                return request[name];
            if (queryDefaults != null && queryDefaults.ContainsKey(name))
                return queryDefaults[name].ToString();
            return defaultValue;
        }

        protected static string[] GetStringsOption(HttpContext context, string name, string[] defaultValue = null)
        {
            string s = GetStringOption(context, name);
            if (string.IsNullOrWhiteSpace(s))
                return defaultValue;
            return s.Split(new char[] { '|' });
        }

        protected static int GetIntOption(HttpContext context, string name, int defaultValue)
        {
            return GetIntOption(context.Request, name, Defaults(context), defaultValue);
        }
        public static int GetIntOption(HttpRequest request, string name, IDictionary<string, object> queryDefaults, int defaultValue)
        {
            double temp;
            if (double.TryParse(GetStringOption(request, name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out temp))
                return (int)Math.Round(temp);
            return defaultValue;
        }

        protected static double GetDoubleOption(HttpContext context, string name, double defaultValue)
        {
            return GetDoubleOption(context.Request, name, Defaults(context), defaultValue);
        }
        public static double GetDoubleOption(HttpRequest request, string name, IDictionary<string, object> queryDefaults, double defaultValue)
        {
            double temp;
            if (double.TryParse(GetStringOption(request, name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out temp))
                return temp;
            return defaultValue;
        }

        protected static bool GetBoolOption(HttpContext context, string name, bool defaultValue)
        {
            return GetBoolOption(context.Request, name, Defaults(context), defaultValue);
        }
        public static bool GetBoolOption(HttpRequest request, string name, IDictionary<string, object> queryDefaults, bool defaultValue)
        {
            int temp;
            if (int.TryParse(GetStringOption(request, name, queryDefaults), NumberStyles.Integer, CultureInfo.InvariantCulture, out temp))
                return temp != 0;
            return defaultValue;
        }

        public static void SendError(HttpResponse response, int code, string description, string message)
        {
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = code;
            response.StatusDescription = description;
            response.ContentType = System.Net.Mime.MediaTypeNames.Text.Plain;
            response.Output.WriteLine(message);
        }

        public abstract string DefaultContentType { get; }
    }
}
