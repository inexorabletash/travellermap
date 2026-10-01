#nullable enable
using Maps.Utilities;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Routing;

namespace Maps
{
    internal abstract class HandlerBase
    {
        // TODO: Enforce verbs (i.e. GET or POST)

        public static void SendError(HttpResponse response, int code, string description, string message)
        {
            response.TrySkipIisCustomErrors = true;
            response.StatusCode = code;
            response.StatusDescription = description;
            response.ContentType = ContentTypes.Text.Plain;
            response.Output.WriteLine(message);
        }

        public static RouteValueDictionary Defaults(HttpContext context)
        {
            RouteData data = context.Items["RouteData"] as RouteData ??
                throw new System.ApplicationException("RouteData not assigned by RouteHandler");
            return data.Values;
        }

        #region Option Parsing
        // Shared by the data/image APIs and the admin pages. Options come from the request
        // (query string or form) first, then from the route's default values.

        public static bool HasOption(HttpRequest request, IDictionary<string, object>? routeDefaults, string name)
            => request[name] != null || (routeDefaults != null && routeDefaults.ContainsKey(name));

        public static string? GetStringOption(HttpRequest request, IDictionary<string, object>? routeDefaults, string name, string? defaultValue = null)
        {
            if (request[name] != null)
                return request[name];
            if (routeDefaults != null && routeDefaults.TryGetValue(name, out object? value) && value != null)
                return value.ToString();
            return defaultValue;
        }

        /// <summary>
        /// Integer values: non-zero is true (e.g. "?routes=0"). A bare flag with no value
        /// (e.g. "?hide-uwp") is true. Otherwise the default.
        /// </summary>
        public static bool GetBoolOption(HttpRequest request, IDictionary<string, object>? routeDefaults, string name, bool defaultValue)
        {
            string? s = GetStringOption(request, routeDefaults, name);
            if (s != null)
                return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n != 0 : defaultValue;

            // Parameters without "=value" are collected under the null key, comma-separated.
            string? bare = request.QueryString[null];
            if (bare != null && bare.Split(',').Contains(name))
                return true;

            return defaultValue;
        }
        #endregion
    }
}
