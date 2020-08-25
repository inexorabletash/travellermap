#nullable enable
#define LEGACY_STYLES

using Json;
using Maps.Rendering;
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Routing;
using System.Xml.Serialization;

namespace Maps.API
{
    public interface ITypeAccepter
    {
        IEnumerable<string> AcceptTypes(HttpContext context, bool ignoreHeaderFallbacks = false);
        bool Accepts(HttpContext context, string mediaType, bool ignoreHeaderFallbacks = false);
    }

    internal abstract class DataHandlerBase : HandlerBase, IHttpHandler
    {
        public bool IsReusable => true;
        public void ProcessRequest(HttpContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

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

            try
            {
                GetResponder(context).Process(new ResourceManager(context.Server));
            }
            catch (HttpError error)
            {
                SendError(context.Response, error.Code, error.Description, error.Message);
            }
#if !DEBUG
            catch (Exception ex)
            {
                SendError(context.Response, 400, "Bad Request", ex.Message);
            }
#endif
        }

        protected class HttpError : Exception
        {
            public int Code { get; }
            public string Description { get; }
            public HttpError(int code, string description, string message) : base(message)
            {
                Code = code;
                Description = description;
            }
        }


        protected abstract DataResponder GetResponder(HttpContext context);

        protected abstract class DataResponder : ITypeAccepter
        {
            public DataResponder(HttpContext context)
            {
                Context = context;
            }

            public abstract string DefaultContentType { get; }

            public HttpContext Context { get; }

            public abstract void Process(ResourceManager resourceManager);

            #region Response Methods
            protected void SendResult(object o, Encoding? encoding = null)
            {
                SendResult(this, o, encoding);
            }

            private static readonly Regex SIMPLE_JS_IDENTIFIER_REGEX = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
            public static bool IsSimpleJSIdentifier(string s) => SIMPLE_JS_IDENTIFIER_REGEX.IsMatch(s);

            public void SendResult(ITypeAccepter accepter, object o, Encoding? encoding = null)
            {
                // Vary: * is basically ignored by browsers
                Context.Response.Cache.SetOmitVaryStar(true);

                if (Context.Request.QueryString["jsonp"] != null)
                {
                    if (!IsSimpleJSIdentifier(Context.Request.QueryString["jsonp"]))
                        throw new HttpError(400, "Bad Request", "The jsonp parameter must be a simple script identifier.");

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
                    if (type == ContentTypes.Text.Xml)
                    {
                        SendXml(o);
                        return;
                    }
                }
                SendText(o, encoding);
            }

            public void SendXml(object o)
            {
                Context.Response.ContentType = ContentTypes.Text.Xml;
                new XmlSerializer(o.GetType()).Serialize(Context.Response.OutputStream, o);
            }

            public void SendText(object o, Encoding? encoding = null)
            {
                Context.Response.ContentType = ContentTypes.Text.Plain;
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

            public bool CheckFile(string filename)
            {
                return File.Exists(Context.Server.MapPath(filename));
            }

            public void SendJson(object o)
            {
                Context.Response.ContentType = JsonConstants.MediaType;

                // TODO: Subclass this from a DataResponsePage
                SendPreamble(JsonConstants.MediaType);
                new JsonSerializer().Serialize(Context.Response.OutputStream, o);
                SendPostamble(JsonConstants.MediaType);
            }

            private void SendPreamble(string contentType)
            {
                if (contentType == JsonConstants.MediaType && Context.Request.QueryString["jsonp"] != null)
                {
                    if (!IsSimpleJSIdentifier(Context.Request.QueryString["jsonp"]))
                        throw new HttpError(400, "Bad Request", "The jsonp parameter must be a simple script identifier.");

                    using var w = new StreamWriter(Context.Response.OutputStream);
                    w.Write(Context.Request.QueryString["jsonp"]);
                    w.Write("(");
                }
            }

            private void SendPostamble(string contentType)
            {
                if (contentType == JsonConstants.MediaType && Context.Request.QueryString["jsonp"] != null)
                {
                    using var w = new StreamWriter(Context.Response.OutputStream);
                    w.Write(");");
                }
            }
            #endregion

            #region Option Parsing
            protected bool HasOption(string name) => HasOption(name, Defaults(Context));

            public bool HasOption(string name, IDictionary<string, object> queryDefaults)
                => Context.Request[name] != null || (queryDefaults != null && queryDefaults.ContainsKey(name));

            protected string? GetStringOption(string name, string? defaultValue = null)
                => GetStringOption(name, Defaults(Context), defaultValue);

            public string? GetStringOption(string name, IDictionary<string, object> queryDefaults, string? defaultValue = null)
            {
                if (Context.Request[name] != null)
                    return Context.Request[name];
                if (queryDefaults != null && queryDefaults.ContainsKey(name))
                    return queryDefaults[name].ToString();
                return defaultValue;
            }

            protected string[]? GetStringsOption(string name, string[]? defaultValue = null)
            {
                string? s = GetStringOption(name);
                if (string.IsNullOrWhiteSpace(s))
                    return defaultValue;
                return s!.Split('|');
            }

            protected int GetIntOption(string name, int defaultValue) => GetIntOption(name, Defaults(Context), defaultValue);

            public int GetIntOption(string name, IDictionary<string, object> queryDefaults, int defaultValue)
            {
                if (double.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
                    return (int)Math.Round(temp);
                return defaultValue;
            }

            protected double GetDoubleOption(string name, double defaultValue) => GetDoubleOption(name, Defaults(Context), defaultValue);

            public double GetDoubleOption(string name, IDictionary<string, object> queryDefaults, double defaultValue)
            {
                if (double.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Float, CultureInfo.InvariantCulture, out double temp))
                    return temp;
                return defaultValue;
            }

            protected bool GetBoolOption(string name, bool defaultValue) => GetBoolOption(name, Defaults(Context), defaultValue);

            public bool GetBoolOption(string name, IDictionary<string, object> queryDefaults, bool defaultValue)
            {
                if (int.TryParse(GetStringOption(name, queryDefaults), NumberStyles.Integer, CultureInfo.InvariantCulture, out int temp))
                    return temp != 0;
                return defaultValue;
            }

            public bool HasLocation() => (HasOption("sx") && HasOption("sy")) || (HasOption("x") && HasOption("y"));

            public Location GetLocation()
            {
                if (HasOption("sx") && HasOption("sy"))
                {
                    return new Location(new Point(GetIntOption("sx", 0), GetIntOption("sy", 0)),
                                        new Hex((byte)GetIntOption("hx", 0), (byte)GetIntOption("hy", 0)));
                }

                if (HasOption("x") && HasOption("y"))
                    return Astrometrics.CoordinatesToLocation(GetIntOption("x", 0), GetIntOption("y", 0));

                throw new ArgumentException("Context is missing required parameters", nameof(Context));
            }

            protected void ParseOptions(ref MapOptions options, ref Style style)
            {
                ParseOptions(Context.Request, Defaults(Context), ref options, ref style);
            }

            private static readonly IReadOnlyDictionary<string, Style> s_nameToStyle = new EasyInitConcurrentDictionary<string, Style> {
                { "poster", Style.Poster },
                { "atlas" , Style.Atlas },
                { "print" , Style.Print },
                { "candy" , Style.Candy },
                { "draft" , Style.Draft },
                { "fasa"  , Style.FASA },
                { "terminal", Style.Terminal },
                { "mongoose", Style.Mongoose },
            };

            public void ParseOptions(HttpRequest request, IDictionary<string, object> queryDefaults, ref MapOptions options, ref Style style)
            {
                options = (MapOptions)GetIntOption("options", queryDefaults, (int)options);

#if LEGACY_STYLES
                // Handle deprecated/legacy options bits for selecting style
                style =
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.PrintStyleDeprecated ? Style.Atlas :
                (options & MapOptions.StyleMaskDeprecated) == MapOptions.CandyStyleDeprecated ? Style.Candy :
                Style.Poster;
#endif // LEGACY_STYLES

                if (HasOption("style", queryDefaults))
                {
                    string opt = GetStringOption("style", queryDefaults)!.ToLowerInvariant();
                    if (!s_nameToStyle.ContainsKey(opt))
                        throw new HttpError(400, "Bad Request", $"Invalid style option: {opt}");
                    style = s_nameToStyle[opt];
                }
            }

            #endregion

            #region ITypeAccepter
            // ITypeAccepter
            public bool Accepts(HttpContext context, string mediaType, bool ignoreHeaderFallbacks = false)
                => AcceptTypes(context, ignoreHeaderFallbacks).Contains(mediaType);

            // ITypeAccepter
            public IEnumerable<string> AcceptTypes(HttpContext context, bool ignoreHeaderFallbacks = false)
            {
                IDictionary<string, object>? queryDefaults = null;
                if (context.Items.Contains("RouteData"))
                    queryDefaults = (context.Items["RouteData"] as RouteData)!.Values;

                if (context.Request["accept"] != null)
                    yield return context.Request["accept"].Replace(' ', '+'); // Hack to allow "image/svg+xml" w/o escaping

                if (context.Request.Headers["accept"] != null)
                    yield return context.Request.Headers["accept"];

                if (queryDefaults != null && queryDefaults.ContainsKey("accept"))
                    yield return queryDefaults["accept"].ToString();

                if (!ignoreHeaderFallbacks && context.Request.AcceptTypes != null)
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