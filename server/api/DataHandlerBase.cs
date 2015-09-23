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

    internal abstract class DataHandlerBase : HandlerBase, IHttpHandler
    {
        protected abstract string ServiceName { get; }

        public bool IsReusable { get { return true; } }

        public void ProcessRequest(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

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

            GetResponder(context).Process();
        }

        protected abstract DataResponder GetResponder(HttpContext context);

        protected abstract class DataResponder : ITypeAccepter
        {
            public DataResponder(HttpContext context)
            {
                this.context = context;
            }

            public abstract string DefaultContentType { get; }

            protected HttpContext context;
            public HttpContext Context { get { return context; } }

            public abstract void Process();

            protected void SendResult(HttpContext context, object o, Encoding encoding = null)
            {
                SendResult(this, o, encoding);
            }

            private static readonly Regex simpleJSIdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
            public static bool IsSimpleJSIdentifier(string s)
            {
                return simpleJSIdentifierRegex.IsMatch(s);
            }


            public void SendResult(ITypeAccepter accepter, object o, Encoding encoding = null)
            {
                // Vary: * is basically ignored by browsers
                Context.Response.Cache.SetOmitVaryStar(true);

                if (Context.Request.QueryString["jsonp"] != null)
                {
                    if (!IsSimpleJSIdentifier(Context.Request.QueryString["jsonp"]))
                    {
                        SendError(Context.Response, 400, "Bad Request", "The jsonp parameter must be a simple script identifier.");
                        return;
                    }

                    SendJson(o);
                    return;
                }

                // Check in priority order, since JSON will always be a default.
                foreach (var type in accepter.AcceptTypes(Context))
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

            public void SendXml(object o)
            {
                Context.Response.ContentType = MediaTypeNames.Text.Xml;
                XmlSerializer xs = new XmlSerializer(o.GetType());
                xs.Serialize(Context.Response.OutputStream, o);
            }

            public void SendText(object o, Encoding encoding = null)
            {
                Context.Response.ContentType = MediaTypeNames.Text.Plain;
                if (encoding == null)
                {
                    Context.Response.Output.Write(o.ToString());
                }
                else
                {
                    Context.Response.ContentEncoding = encoding;
                    Context.Response.Output.Write(o.ToString());
                }
            }

            public void SendFile(string contentType, string filename)
            {
                Context.Response.ContentType = contentType;
                SendPreamble(contentType);
                Context.Response.TransmitFile(filename);
                SendPostamble(contentType);
            }

            public void SendJson(object o)
            {
                Context.Response.ContentType = JsonConstants.MediaType;

                JsonSerializer js = new JsonSerializer();

                // TODO: Subclass this from a DataResponsePage
                SendPreamble(JsonConstants.MediaType);
                js.Serialize(Context.Response.OutputStream, o);
                SendPostamble(JsonConstants.MediaType);
            }

            private void SendPreamble(string contentType)
            {
                if (contentType == JsonConstants.MediaType && Context.Request.QueryString["jsonp"] != null)
                {
                    using (var w = new StreamWriter(Context.Response.OutputStream))
                    {
                        // TODO: Ensure jsonp is just an identifier
                        w.Write(Context.Request.QueryString["jsonp"]);
                        w.Write("(");
                    }
                }
            }

            private void SendPostamble(string contentType)
            {
                if (contentType == JsonConstants.MediaType && Context.Request.QueryString["jsonp"] != null)
                {
                    using (var w = new StreamWriter(Context.Response.OutputStream))
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
                IDictionary<string, object> queryDefaults = null;
                if (context.Items.Contains("RouteData"))
                    queryDefaults = (context.Items["RouteData"] as System.Web.Routing.RouteData).Values;

                if (context.Request["accept"] != null)
                    yield return context.Request["accept"];

                if (context.Request.Headers["accept"] != null)
                    yield return context.Request.Headers["accept"];

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
}