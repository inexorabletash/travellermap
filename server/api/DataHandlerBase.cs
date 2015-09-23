#define LEGACY_STYLES

using Json;
using Maps.Rendering;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Serialization;
using System.Web.Routing;
using System.Drawing;
using System.Globalization;

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

            #region Response Methods
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
                        SendError(400, "Bad Request", "The jsonp parameter must be a simple script identifier.");
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

            public void SendError(int code, string description, string message)
            {
                HandlerBase.SendError(Context.Response, code, description, message);
            }
            #endregion

            #region Option Parsing
            protected bool HasOption(string name)
            {
                return HasOption(name, Defaults(context));
            }
            public bool HasOption(string name, IDictionary<string, object> queryDefaults)
            {
                return Context.Request[name] != null || (queryDefaults != null && queryDefaults.ContainsKey(name));
            }

            protected string GetStringOption(string name, string defaultValue = null)
            {
                return GetStringOption(name, Defaults(context), defaultValue);
            }
            public string GetStringOption(string name, IDictionary<string, object> queryDefaults, string defaultValue = null)
            {
                if (Context.Request[name] != null)
                    return Context.Request[name];
                if (queryDefaults != null && queryDefaults.ContainsKey(name))
                    return queryDefaults[name].ToString();
                return defaultValue;
            }

            protected string[] GetStringsOption(string name, string[] defaultValue = null)
            {
                string s = GetStringOption(name);
                if (string.IsNullOrWhiteSpace(s))
                    return defaultValue;
                return s.Split(new char[] { '|' });
            }

            protected int GetIntOption(string name, int defaultValue)
            {
                return GetIntOption(name, Defaults(context), defaultValue);
            }
            public int GetIntOption(string name, IDictionary<string, object> queryDefaults, int defaultValue)
            {
                double temp;
                if (double.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out temp))
                    return (int)Math.Round(temp);
                return defaultValue;
            }

            protected double GetDoubleOption(string name, double defaultValue)
            {
                return GetDoubleOption(name, Defaults(context), defaultValue);
            }
            public double GetDoubleOption(string name, IDictionary<string, object> queryDefaults, double defaultValue)
            {
                double temp;
                if (double.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out temp))
                    return temp;
                return defaultValue;
            }

            protected bool GetBoolOption(string name, bool defaultValue)
            {
                return GetBoolOption(name, Defaults(context), defaultValue);
            }
            public bool GetBoolOption(string name, IDictionary<string, object> queryDefaults, bool defaultValue)
            {
                int temp;
                if (int.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Integer, CultureInfo.InvariantCulture, out temp))
                    return temp != 0;
                return defaultValue;
            }

            public bool HasLocation()
            {
                return (HasOption("sx") && HasOption("sy")) ||
                       (HasOption("x") && HasOption("y"));
            }

            public Location GetLocation()
            {
                if (HasOption("sx") && HasOption("sy"))
                {
                    return new Location(new Point(GetIntOption("sx", 0), GetIntOption("sy", 0)),
                                        new Hex((byte)GetIntOption("hx", 0), (byte)GetIntOption("hy", 0)));
                }

                if (HasOption("x") && HasOption("y"))
                    return Astrometrics.CoordinatesToLocation(GetIntOption("x", 0), GetIntOption("y", 0));

                throw new ArgumentException("Context is missing required parameters", "context");
            }

            protected void ParseOptions(ref MapOptions options, ref Stylesheet.Style style)
            {
                ParseOptions(context.Request, Defaults(context), ref options, ref style);
            }

            private static Dictionary<string, Stylesheet.Style> s_nameToStyle = new Dictionary<string, Stylesheet.Style>() {
            { "poster",Stylesheet.Style.Poster },
            { "atlas" ,Stylesheet.Style.Atlas },
            { "print" , Stylesheet.Style.Print },
            { "candy" ,Stylesheet.Style.Candy },
            { "draft" ,Stylesheet.Style.Draft },
            { "fasa"  ,Stylesheet.Style.FASA },
        };

            public void ParseOptions(HttpRequest request, IDictionary<string, object> queryDefaults, ref MapOptions options, ref Stylesheet.Style style)
            {
                options = (MapOptions)GetIntOption("options", queryDefaults, (int)options);

#if LEGACY_STYLES
            // Handle deprecated/legacy options bits for selecting style
            style =
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.PrintStyleDeprecated ? Stylesheet.Style.Atlas :
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.CandyStyleDeprecated ? Stylesheet.Style.Candy :
                Stylesheet.Style.Poster;
#endif // LEGACY_STYLES

                if (HasOption("style", queryDefaults))
                {
                    try
                    {
                        style = s_nameToStyle[GetStringOption("style", queryDefaults).ToLowerInvariant()];
                    }
                    catch (KeyNotFoundException)
                    {
                        // TODO: Report error?
                    }
                }
            }

            private static RouteValueDictionary Defaults(HttpContext context)
            {
                RouteData data = context.Items["RouteData"] as RouteData;
                if (data == null)
                    throw new ApplicationException("RouteData not assigned by RouteHandler");
                return data.Values;
            }
            #endregion

            #region ITypeAccepter
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
            #endregion
        }

    }
}