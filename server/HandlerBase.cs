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
            response.ContentType = System.Net.Mime.MediaTypeNames.Text.Plain;
            response.Output.WriteLine(message);
        }

        public static RouteValueDictionary Defaults(HttpContext context)
        {
            RouteData data = context.Items["RouteData"] as RouteData;
            if (data == null)
                throw new System.ApplicationException("RouteData not assigned by RouteHandler");
            return data.Values;
        }
    }
}
