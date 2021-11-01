using Maps.Utilities;
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
    }
}
