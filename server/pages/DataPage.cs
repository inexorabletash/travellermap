using Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Web;
using System.Xml.Serialization;

namespace Maps.Pages
{
    public interface ITypeAccepter
    {
        IEnumerable<string> AcceptTypes(HttpContext context);
        bool Accepts(HttpContext context, string mediaType);
    }

    public abstract class BasePage : System.Web.UI.Page, ITypeAccepter
    {
        protected abstract string ServiceName { get; }

        protected bool AdminAuthorized()
        {
            return AdminBase.AdminAuthorized(Context);
        }

        protected abstract string DefaultContentType { get; }
        public IEnumerable<string> AcceptTypes(HttpContext context)
        {
            if (context.Request["accept"] != null) {
                yield return context.Request["accept"];
            }
            if (RouteData.Values.ContainsKey("accept")) {
                yield return RouteData.Values["accept"].ToString();
            }
            if (context.Request.AcceptTypes != null)
            {
                foreach (var type in context.Request.AcceptTypes)
                {
                    yield return type;
                }
            }

            yield return DefaultContentType;
        }

        public bool Accepts(HttpContext context, string mediaType)
        {
            return AcceptTypes(context).Contains(mediaType);
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
        protected void SendResult(object o, Encoding encoding = null)
        {
            DataHandlerBase.SendResult(Context, this, o, encoding);
        }

        private void SendXml(object o)
        {
            DataHandlerBase.SendXml(Context, o);
        }

        private void SendText(object o, Encoding encoding)
        {
            DataHandlerBase.SendText(Context, o, encoding);
        }

        private void SendJson(object o)
        {
            DataHandlerBase.SendJson(Context, o);
        }

        public void SendFile(string contentType, string filename)
        {
            DataHandlerBase.SendFile(Context, contentType, filename);
        }
    }

    public abstract class DataHandlerBase : HandlerBase, IHttpHandler
    {
        protected abstract string ServiceName { get; }
        public abstract void Process(HttpContext context);

        bool IHttpHandler.IsReusable { get { return true; } }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            if (!ServiceConfiguration.CheckEnabled(ServiceName, context.Response))
                return;

            // Configure caching
            context.Response.Cache.SetCacheability(HttpCacheability.Public);
            context.Response.Cache.SetMaxAge(TimeSpan.FromHours(1));
            context.Response.Cache.VaryByParams["*"] = true;
            context.Response.Cache.VaryByHeaders["Accept"] = true;

            Process(context);
        }

        public static void SendResult(HttpContext context, ITypeAccepter accepter, object o, Encoding encoding = null)
        {
            // CORS - allow from any origin
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");

            // Vary: * is basically ignored by browsers
            context.Response.Cache.SetOmitVaryStar(true);

            if (context.Request.QueryString["jsonp"] != null)
            {
                SendJson(context, o);
                return;
            }

            // Check in priority order, since JSON will always be a default.
            foreach (var type in accepter.AcceptTypes(context))
            {
                if (type == JsonConstants.MediaType)
                {
                    SendJson(context, o);
                    return;
                }
                if (type == MediaTypeNames.Text.Xml)
                {
                    SendXml(context, o);
                    return;
                }
            }
            SendText(context, o, encoding);
        }

        public static void SendXml(HttpContext context, object o)
        {
            context.Response.ContentType = MediaTypeNames.Text.Xml;
            XmlSerializer xs = new XmlSerializer(o.GetType());
            xs.Serialize(context.Response.OutputStream, o);
        }

        public static void SendText(HttpContext context, object o, Encoding encoding)
        {
            context.Response.ContentType = MediaTypeNames.Text.Plain;
            if (encoding == null)
            {
                context.Response.Output.Write(o.ToString());
            }
            else
            {
                context.Response.ContentEncoding = encoding;
                context.Response.Output.Write(o.ToString());
            }
        }

        public static void SendFile(HttpContext context, string contentType, string filename)
        {
            // CORS - allow from any origin
            context.Response.AddHeader("Access-Control-Allow-Origin", "*");

            context.Response.ContentType = contentType;
            SendPreamble(context, contentType);
            context.Response.TransmitFile(filename);
            SendPostamble(context, contentType);
        }

        public static void SendJson(HttpContext context, object o)
        {
            context.Response.ContentType = JsonConstants.MediaType;
            JsonSerializer js = new JsonSerializer();

            // TODO: Subclass this from a DataResponsePage
            SendPreamble(context, JsonConstants.MediaType);
            js.Serialize(context.Response.OutputStream, o);
            SendPostamble(context, JsonConstants.MediaType);
        }

        private static void SendPreamble(HttpContext context, string contentType)
        {
            if (contentType == JsonConstants.MediaType && context.Request.QueryString["jsonp"] != null)
            {
                using (var w = new StreamWriter(context.Response.OutputStream))
                {
                    w.Write(context.Request.QueryString["jsonp"]);
                    w.Write("(");
                }
            }
        }

        private static void SendPostamble(HttpContext context, string contentType)
        {
            if (contentType == JsonConstants.MediaType && context.Request.QueryString["jsonp"] != null)
            {
                using (var w = new StreamWriter(context.Response.OutputStream))
                {
                    w.Write(");");
                }
            }
        }
    }
}