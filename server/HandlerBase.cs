using System.Web;

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
    }
}
