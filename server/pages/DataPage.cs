using Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Xml.Serialization;
using System.Web;

namespace Maps.Pages
{
    public interface IRequestAccepter
    {
        bool Accepts(HttpRequest request, string mediaType);
    }

    public abstract class BasePage : System.Web.UI.Page, IRequestAccepter
    {
        protected bool AdminAuthorized()
        {
            return AdminBase.AdminAuthorized(Context);
        }

        public abstract string DefaultContentType { get; }
        public IEnumerable<string> AcceptTypes(HttpRequest request, IDictionary<string, Object> queryDefaults) {
            if (request["accept"] != null) {
                yield return request["accept"];
            }
            if (queryDefaults.ContainsKey("accept")) {
                yield return queryDefaults["accept"].ToString();
            }
            if (request.AcceptTypes != null)
            {
                foreach (var type in request.AcceptTypes)
                {
                    yield return type;
                }
            }

            yield return DefaultContentType;
        }

        public bool Accepts(HttpRequest request, string mediaType)
        {
            return AcceptTypes(request, RouteData.Values).Contains(mediaType);
        }

        protected bool HasOption(string name)
        {
            return HandlerBase.HasOption(Request, name, RouteData.Values);
        }

        protected string GetStringOption(string name)
        {
            return HandlerBase.GetStringOption(Request, name, RouteData.Values);
        }

        protected int GetIntOption(string name, int defaultValue)
        {
            return HandlerBase.GetIntOption(Request, name, RouteData.Values, defaultValue);
        }

        protected double GetDoubleOption(string name, double defaultValue)
        {
            return HandlerBase.GetDoubleOption(Request, name, RouteData.Values, defaultValue);
        }

        protected bool GetBoolOption(string name, bool defaultValue)
        {
            return HandlerBase.GetBoolOption(Request, name, RouteData.Values, defaultValue);
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

            foreach (var type in AcceptTypes(Request, RouteData.Values))
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