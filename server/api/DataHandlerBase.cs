using Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Serialization;

namespace Maps.API
{
    public interface ITypeAccepter
    {
        IEnumerable<string> AcceptTypes(HttpContext context);
        bool Accepts(HttpContext context, string mediaType);
    }

    public abstract class DataHandlerBase : HandlerBase, IHttpHandler, ITypeAccepter
    {
        protected abstract string ServiceName { get; }
        public abstract void Process(HttpContext context);

        bool IHttpHandler.IsReusable { get { return true; } }

        void IHttpHandler.ProcessRequest(HttpContext context)
        {
            if (!ServiceConfiguration.CheckEnabled(ServiceName, context.Response))
                return;

            // Configure caching
            if (context.Request.HttpMethod == "POST")
            {
                context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
            }
            else
            {
#if DEBUG
                context.Response.Cache.SetCacheability(HttpCacheability.NoCache);
#else
                context.Response.Cache.SetCacheability(HttpCacheability.Public);
                context.Response.Cache.SetMaxAge(TimeSpan.FromHours(1));
                context.Response.Cache.SetValidUntilExpires(true);
                context.Response.Cache.VaryByParams["*"] = true;
                context.Response.Cache.VaryByHeaders["Accept"] = true;
#endif
            }

            Process(context);
        }

        protected void SendResult(HttpContext context, object o, Encoding encoding = null)
        {
            SendResult(context, this, o, encoding);
        }
            
        private static readonly Regex simpleJSIdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        public static bool IsSimpleJSIdentifier(string s)
        {
            return simpleJSIdentifierRegex.IsMatch(s);
        }


        public static void SendResult(HttpContext context, ITypeAccepter accepter, object o, Encoding encoding = null)
        {
            // Vary: * is basically ignored by browsers
            context.Response.Cache.SetOmitVaryStar(true);

            if (context.Request.QueryString["jsonp"] != null)
            {
                if (!IsSimpleJSIdentifier(context.Request.QueryString["jsonp"]))
                {
                    SendError(context.Response, 400, "Bad Request", "The jsonp parameter must be a simple script identifier.");
                    return;
                }

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
                    // TODO: Ensure jsonp is just an identifier
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

        // ITypeAccepter
        public bool Accepts(HttpContext context, string mediaType)
        {
            return AcceptTypes(context).Contains(mediaType);
        }

        // ITypeAccepter
        public IEnumerable<string> AcceptTypes(HttpContext context)
        {
            IDictionary<string, Object> queryDefaults = null;
            if (context.Items.Contains("RouteData"))
                queryDefaults = (context.Items["RouteData"] as System.Web.Routing.RouteData).Values;

            if (context.Request["accept"] != null)
                yield return context.Request["accept"];

            if (queryDefaults != null && queryDefaults.ContainsKey("accept"))
                yield return queryDefaults["accept"].ToString();

            if (context.Request.AcceptTypes != null)
            {
                foreach (var type in context.Request.AcceptTypes)
                    yield return type;
            }

            yield return DefaultContentType;
        }
    }
}