using Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Web.Routing;
using System.Xml.Serialization;

namespace Maps.Pages
{
    public abstract class BasePage : System.Web.UI.Page
    {
        protected bool AdminAuthorized()
        {
            return AdminBase.AdminAuthorized(Context);
        }

        public abstract string DefaultContentType { get; }
        protected IEnumerable<string> AcceptTypes 
        { 
            get {
                if (Request["accept"] != null) {
                    yield return Request["accept"];
                }
                if (RouteData.Values["accept"] != null) {
                    yield return RouteData.Values["accept"].ToString();
                }
                if (Request.AcceptTypes != null)
                {
                    foreach (var type in Request.AcceptTypes)
                    {
                        yield return type;
                    }
                }

                yield return DefaultContentType;
            }
        }

        protected bool Accepts(string mediaType)
        {
            return AcceptTypes.Contains(mediaType);
        }

        protected bool HasOption(string name)
        {
            return Request[name] != null || RouteData.Values[name] != null;
        }

        protected string GetStringOption(string name)
        {
            if (Request[name] != null)
                return Request[name];
            if (RouteData.Values[name] != null)
                return RouteData.Values[name].ToString();
            return null;
        }

        protected int GetIntOption(string name, int defaultValue)
        {
            int temp;
            if (Int32.TryParse(GetStringOption(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out temp))
            {
                return temp;
            }
            return defaultValue;
        }

        protected double GetDoubleOption(string name, double defaultValue)
        {
            double temp;
            if (Double.TryParse(GetStringOption(name), NumberStyles.Float, CultureInfo.InvariantCulture, out temp))
            {
                return temp;
            }
            return defaultValue;
        }

        protected bool GetBoolOption(string name, bool defaultValue)
        {
            int temp;
            if (Int32.TryParse(GetStringOption(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out temp))
            {
                return temp != 0;
            }
            return defaultValue;
        }

        protected void SendError(int code, string description, string message)
        {
            HandlerBase.SendError(Response, code, description, message);
        }
    }

    public abstract class DataPage : BasePage
    {
        /// <summary>
        /// Send as the specified content type if no Accept: type (or query param) 
        /// requests differently.
        /// </summary>
        /// <param name="o"></param>
        /// <param name="defaultContentType"></param>
        protected void SendResult(object o, Encoding encoding = null)
        {
            // CORS - allow from any origin
            Response.AddHeader("Access-Control-Allow-Origin", "*");

            // Vary: * is basically ignored by browsers
            Response.Cache.SetOmitVaryStar(true);

            if (Request.QueryString["jsonp"] != null)
            {
                SendJson(o);
                return;
            }

            foreach (var type in AcceptTypes)
            {
                if (type == JsonConstants.MediaType)
                {
                    SendJson(o);
                    return;
                }
                if (type == MediaTypeNames.Text.Xml)
                {
                    SendXml(o);
                    return;
                }
            }

            SendText(o, encoding);
        }

        private void SendXml(object o)
        {
            Response.ContentType = MediaTypeNames.Text.Xml;
            XmlSerializer xs = new XmlSerializer(o.GetType());
            xs.Serialize(Response.OutputStream, o);
        }

        private void SendText(object o, Encoding encoding)
        {
            Response.ContentType = MediaTypeNames.Text.Plain;
            if (encoding == null)
            {
                Response.Output.Write(o.ToString());
            }
            else
            {
                Response.ContentEncoding = encoding;
                Response.Output.Write(o.ToString());
            }
        }

        private void SendJson(object o)
        {
            Response.ContentType = JsonConstants.MediaType;
            JsonSerializer js = new JsonSerializer();

            // TODO: Subclass this from a DataResponsePage
            SendPreamble(JsonConstants.MediaType);
            js.Serialize(Response.OutputStream, o);
            SendPostamble(JsonConstants.MediaType);
        }

        private void SendPreamble(string contentType)
        {
            if (contentType == JsonConstants.MediaType && Request.QueryString["jsonp"] != null)
            {
                using (var w = new StreamWriter(Response.OutputStream))
                {
                    w.Write(Request.QueryString["jsonp"]);
                    w.Write("(");
                }
            }
        }

        private void SendPostamble(string contentType)
        {
            if (contentType == JsonConstants.MediaType && Request.QueryString["jsonp"] != null)
            {
                using (var w = new StreamWriter(Response.OutputStream))
                {
                    w.Write(");");
                }
            }
        }

        public void SendFile(string contentType, string filename)
        {
            // CORS - allow from any origin
            Response.AddHeader("Access-Control-Allow-Origin", "*");

            Response.ContentType = contentType;
            SendPreamble(contentType);
            Response.TransmitFile(filename);
            SendPostamble(contentType);
        }
    }

}