using System.Net.Mime;
using System.Web;

namespace Maps
{
    public class ServiceConfiguration
    {
        private static bool IsEnabled(string api)
        {
            switch (api)
            {
                case "search": return true;
                case "credits": return true;
                case "universe": return true;
                case "jumpworlds": return true;

                case "sec": return true;
                case "msec": return true;
                case "sectormetadata": return true;

                case "poster": return true;
                case "jumpmap": return true;

                default: return true;
            }
        }

        public static bool CheckEnabled(string api, HttpResponse response)
        {
            if (!IsEnabled(api))
            {
                response.StatusCode = 503;
                response.StatusDescription = "Service Unavailable";
                response.ContentType = MediaTypeNames.Text.Plain;
                response.Output.WriteLine("Service temporarily unavailable - sorry, Travellers!");
                return false;
            }
            return true;
        }

    }
}
