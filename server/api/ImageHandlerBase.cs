#nullable enable
using Maps.Graphics;
using Maps.Rendering;
using Maps.Serialization;
using Maps.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Mime;
using System.Web;

namespace Maps.API
{
    internal abstract class ImageHandlerBase : DataHandlerBase
    {
        public const double MinScale = 0.0078125; // Math.Pow(2, -7);
        public const double MaxScale = 512; // Math.Pow(2, 9);

        protected abstract class ImageResponder : DataResponder
        {
            protected ImageResponder(HttpContext context) : base(context) { }
            public override string DefaultContentType => ContentTypes.Image.Png;
            protected void ProduceResponse(HttpContext context, string title, RenderContext ctx, Size tileSize,
                AbstractMatrix transform,
                bool transparent = false)
            {
                ProduceResponse(context, this, title, ctx, tileSize, transform, transparent,
                    (context.Items["RouteData"] as System.Web.Routing.RouteData)!.Values);
            }

            protected void ProduceResponse(HttpContext context, ITypeAccepter accepter, string title, RenderContext ctx, Size tileSize,
                AbstractMatrix transform,
                bool transparent, IDictionary<string, object> queryDefaults)
            {
                // New-style Options

                #region URL Parameters
                // TODO: move to ParseOptions (maybe - requires options to be parsed after stylesheet creation?)
                if (GetBoolOption("sscoords", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.hexCoordinateStyle = HexCoordinateStyle.Subsector;

                if (GetBoolOption("allhexes", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.numberAllHexes = true;

                if (GetBoolOption("nogrid", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.parsecGrid.visible = false;

                if (!GetBoolOption("routes", queryDefaults: queryDefaults, defaultValue: true))
                {
                    ctx.Styles.macroRoutes.visible = false;
                    ctx.Styles.microRoutes.visible = false;
                }

                if (!GetBoolOption("rifts", queryDefaults: queryDefaults, defaultValue: true))
                    ctx.Styles.showRiftOverlay = false;

                if (GetBoolOption("po", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.populationOverlay.visible = true;

                if (GetBoolOption("im", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.importanceOverlay.visible = true;

                if (GetBoolOption("cp", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.capitalOverlay.visible = true;

                if (GetBoolOption("stellar", queryDefaults: queryDefaults, defaultValue: false))
                    ctx.Styles.showStellarOverlay = true;

                ctx.Styles.dimUnofficialSectors = GetBoolOption("dimunofficial", queryDefaults: queryDefaults, defaultValue: false);
                ctx.Styles.colorCodeSectorStatus = GetBoolOption("review", queryDefaults: queryDefaults, defaultValue: false);
                ctx.Styles.droyneWorlds.visible = GetBoolOption("dw", queryDefaults: queryDefaults, defaultValue: false);
                ctx.Styles.minorHomeWorlds.visible = GetBoolOption("mh", queryDefaults: queryDefaults, defaultValue: false);
                ctx.Styles.ancientsWorlds.visible = GetBoolOption("an", queryDefaults: queryDefaults, defaultValue: false);

                // TODO: Return an error if pattern is invalid?
                ctx.Styles.highlightWorldsPattern = HighlightWorldPattern.Parse(
                    GetStringOption("hw", queryDefaults: queryDefaults, defaultValue: String.Empty)!.Replace(' ', '+'));
                ctx.Styles.highlightWorlds.visible = ctx.Styles.highlightWorldsPattern != null;

                double devicePixelRatio = GetDoubleOption("dpr", defaultValue: 1, queryDefaults: queryDefaults);
                devicePixelRatio = Math.Round(devicePixelRatio, 1);
                if (devicePixelRatio <= 0)
                    devicePixelRatio = 1;
                if (devicePixelRatio > 2)
                    devicePixelRatio = 2;

                ctx.Styles.routeEndAdjust = (float)GetDoubleOption("rea", defaultValue: 0.25, queryDefaults: queryDefaults);

                bool dataURI = GetBoolOption("datauri", queryDefaults: queryDefaults, defaultValue: false);

                if (GetStringOption("milieu", SectorMap.DEFAULT_MILIEU) != SectorMap.DEFAULT_MILIEU)
                {
                    // TODO: Make this declarative in resource files.
                    if (ctx.Styles.macroBorders.visible)
                    {
                        ctx.Styles.macroBorders.visible = false;
                        ctx.Styles.microBorders.visible = true;
                    }
                    ctx.Styles.macroNames.visible = false;
                    ctx.Styles.macroRoutes.visible = false;
                }
                #endregion

                // "content-disposition: inline" is not used as Chrome opens that in a tab, then
                // (sometimes?) fails to allow it to be saved due to being served via POST. 
                string disposition = context.Request.HttpMethod == "POST"
                    && context.Request.UserAgent.Contains("Chrome")
                    ? "attachment" : "inline";

                MemoryStream? ms = null;
                if (dataURI)
                    ms = new MemoryStream();
                Stream outputStream = ms ?? Context.Response.OutputStream;

                if (accepter.Accepts(context, ContentTypes.Image.Svg, ignoreHeaderFallbacks: true))
                {
                    #region SVG Generation
                    using var svg = new SVGGraphics(tileSize.Width, tileSize.Height);
                    RenderToGraphics(ctx, transform, svg);

                    using var stream = new MemoryStream();
                    svg.Serialize(new StreamWriter(stream));
                    context.Response.ContentType = ContentTypes.Image.Svg;
                    if (!dataURI)
                    {
                        context.Response.AddHeader("content-length", stream.Length.ToString());
                        context.Response.AddHeader("content-disposition", $"{disposition};filename=\"{Util.SanitizeFilename(title)}.svg\"");
                    }
                    stream.WriteTo(outputStream);
                    #endregion
                }

                else if (accepter.Accepts(context, ContentTypes.Application.Pdf, ignoreHeaderFallbacks: true))
                {
                    #region PDF Generation

                    using var stream = new MemoryStream();

                    // PDFSharp 1.5 is not thread-safe, so serialize usage
                    lock (ImageHandlerBase.s_pdf_serialization_lock)
                    {
                        using var document = new PdfDocument();
                        document.Version = 14; // 1.4 for opacity
                        document.Info.Title = title;
                        document.Info.Author = "Joshua Bell";
                        document.Info.Creator = "TravellerMap.com";
                        document.Info.Subject = DateTime.Now.ToString("F", CultureInfo.InvariantCulture);
                        document.Info.Keywords = "The Traveller game in all forms is owned by Mongoose Publishing. Copyright 1977 - 2024 Mongoose Publishing.";

                        // TODO: Credits/Copyright
                        // This is close, but doesn't define the namespace correctly:
                        // document.Info.Elements.Add( new KeyValuePair<string, PdfItem>( "/photoshop/Copyright", new PdfString( "HelloWorld" ) ) );

                        PdfPage page = document.AddPage();

                        // NOTE: only PageUnit currently supported in MGraphics is Points
                        page.Width = XUnit.FromPoint(tileSize.Width);
                        page.Height = XUnit.FromPoint(tileSize.Height);

                        using var gfx = new PdfSharpGraphics(XGraphics.FromPdfPage(page));
                        RenderToGraphics(ctx, transform, gfx);

                        document.Save(stream, closeStream: false);
                    }
                    context.Response.ContentType = ContentTypes.Application.Pdf;
                    if (!dataURI)
                    {
                        context.Response.AddHeader("content-length", stream.Length.ToString());
                        context.Response.AddHeader("content-disposition", $"{disposition};filename=\"{Util.SanitizeFilename(title)}.pdf\"");
                    }
                    stream.WriteTo(outputStream);
                    #endregion
                }
                else
                {
                    #region Bitmap Generation
                    int width = (int)Math.Floor(tileSize.Width * devicePixelRatio);
                    int height = (int)Math.Floor(tileSize.Height * devicePixelRatio);
                    using var bitmap = TryConstructBitmap(width, height, PixelFormat.Format32bppArgb);
                    if (bitmap == null)
                    {
                        throw new HttpError(500, "Internal Server Error",
                            $"Failed to allocate bitmap ({width}x{height}). Insufficient memory?");
                    }

                    if (transparent)
                        bitmap.MakeTransparent();

                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                        using var graphics = new BitmapGraphics(g);
                        graphics.ScaleTransform((float)devicePixelRatio);
                        RenderToGraphics(ctx, transform, graphics);
                    }

                    BitmapResponse(context.Response, disposition, outputStream, ctx.Styles, bitmap, transparent ? ContentTypes.Image.Png : null, title);
                    #endregion
                }

                if (dataURI)
                {
                    string contentType = context.Response.ContentType;
                    context.Response.ContentType = ContentTypes.Text.Plain;
                    ms!.Seek(0, SeekOrigin.Begin);

                    context.Response.Output.Write("data:");
                    context.Response.Output.Write(contentType);
                    context.Response.Output.Write(";base64,");
                    context.Response.Output.Flush();

                    System.Security.Cryptography.ICryptoTransform encoder = new System.Security.Cryptography.ToBase64Transform();
                    using System.Security.Cryptography.CryptoStream cs = new System.Security.Cryptography.CryptoStream(context.Response.OutputStream, encoder, System.Security.Cryptography.CryptoStreamMode.Write);
                    ms!.WriteTo(cs);
                    cs.FlushFinalBlock();
                }

                context.Response.Flush();
                context.Response.Close();
                return;
            }

            private static Bitmap? TryConstructBitmap(int width, int height, PixelFormat pixelFormat)
            {
                try
                {
                    return new Bitmap(width, height, pixelFormat);
                }
                catch (ArgumentException)
                {
                    // See http://stackoverflow.com/questions/1949045/net-bitmap-class-constructor-int-int-and-int-int-pixelformat-throws-argu
                    return null;
                }
            }

            private static void RenderToGraphics(RenderContext ctx, AbstractMatrix transform, AbstractGraphics graphics)
            {
                graphics.MultiplyTransform(transform);

                if (ctx.DrawBorder && ctx.ClipPath != null)
                {
                    using (graphics.Save())
                    {
                        // Render border in world space
                        AbstractMatrix m = ctx.ImageSpaceToWorldSpace;
                        graphics.MultiplyTransform(m);
                        AbstractPen pen = new AbstractPen(ctx.Styles.imageBorderColor, ctx.Styles.imageBorderWidth);

                        // SVG/PdfSharp can't ExcludeClip so we take advantage of the fact that we know
                        // the path starts on the left edge and proceeds clockwise. We extend the
                        // path with a counterclockwise border around it, then use that to exclude
                        // the original path's region for rendering the border.
                        RectangleF bounds = PathUtil.Bounds(ctx.ClipPath);
                        bounds.Inflate(2 * (float)pen.Width, 2 * (float)pen.Width);
                        List<byte> types = new List<byte>(ctx.ClipPath.Types);
                        List<PointF> points = new List<PointF>(ctx.ClipPath.Points);

                        PointF key = points[0];
                        points.Add(new PointF(bounds.Left, key.Y)); types.Add(1);
                        points.Add(new PointF(bounds.Left, bounds.Bottom)); types.Add(1);
                        points.Add(new PointF(bounds.Right, bounds.Bottom)); types.Add(1);
                        points.Add(new PointF(bounds.Right, bounds.Top)); types.Add(1);
                        points.Add(new PointF(bounds.Left, bounds.Top)); types.Add(1);
                        points.Add(new PointF(bounds.Left, key.Y)); types.Add(1);
                        points.Add(new PointF(key.X, key.Y)); types.Add(1);

                        graphics.IntersectClip(new AbstractPath(points.ToArray(), types.ToArray()));
                        graphics.DrawPath(pen, ctx.ClipPath);
                    }
                }

                using (graphics.Save())
                {
                    ctx.Render(graphics);
                }
            }

            private static void BitmapResponse(HttpResponse response, string disposition, Stream outputStream, Stylesheet styles, Bitmap bitmap, string? mimeType, string? title)
            {
                try
                {
                    // JPEG or PNG if not specified, based on style
                    mimeType ??= styles.preferredMimeType;

                    response.ContentType = mimeType;
                    string? extension = mimeType switch
                    {
                        ContentTypes.Image.Jpeg => "jpg",
                        ContentTypes.Image.Gif => "gif",
                        ContentTypes.Image.Png => "png",
                        _ => null
                    };


                    // Searching for a matching encoder
                    ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                        .FirstOrDefault(e => e.MimeType == response.ContentType);

                    if (encoder != null)
                    {
                        EncoderParameters encoderParams;
                        if (mimeType == ContentTypes.Image.Jpeg)
                        {
                            encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)95);
                        }
                        else if (mimeType == ContentTypes.Image.Png)
                        {
                            encoderParams = new EncoderParameters(1);
                            encoderParams.Param[0] = new EncoderParameter(Encoder.ColorDepth, 8);
                        }
                        else
                        {
                            encoderParams = new EncoderParameters(0);
                        }

                        if (mimeType == ContentTypes.Image.Png)
                        {
                            // PNG encoder is picky about streams - need to do an indirection
                            // http://www.west-wind.com/WebLog/posts/8230.aspx
                            using var ms = new MemoryStream();
                            bitmap.Save(ms, encoder, encoderParams);
                            ms.WriteTo(outputStream);
                        }
                        else
                        {
                            bitmap.Save(outputStream, encoder, encoderParams);
                        }

                        encoderParams.Dispose();
                    }
                    else
                    {
                        // Default to GIF if we can't find anything
                        response.ContentType = ContentTypes.Image.Gif;
                        bitmap.Save(outputStream, ImageFormat.Gif);
                    }

                    if (title != null && extension != null)
                    {
                        response.AddHeader("content-disposition", $"{disposition};filename=\"{Util.SanitizeFilename(title)}.{extension}\"");
                    }

                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    // Saving seems to throw "A generic error occurred in GDI+." on low memory.
                    throw new HttpError(500, "Internal Server Error",
                        $"Unknown GDI error encoding bitmap ({bitmap.Width}x{bitmap.Height}). Insufficient memory?");
                }
            }

            protected static Sector? GetPostedSector(HttpRequest request, ErrorLogger errors)
            {
                Sector? sector;

                if (request.Files["file"] != null && request.Files["file"].ContentLength > 0)
                {
                    HttpPostedFile hpf = request.Files["file"];
                    sector = new Sector(hpf.InputStream, hpf.ContentType, errors);
                }
                else if (!string.IsNullOrEmpty(request.Form["data"]))
                {
                    string data = request.Form["data"];
                    sector = new Sector(data.ToStream(), ContentTypes.Text.Plain, errors);
                }
                else if (new ContentType(request.ContentType).MediaType == ContentTypes.Text.Plain)
                {
                    sector = new Sector(request.InputStream, ContentTypes.Text.Plain, errors);
                }
                else
                {
                    return null;
                }

                if (request.Files["metadata"] != null && request.Files["metadata"].ContentLength > 0)
                {
                    HttpPostedFile hpf = request.Files["metadata"];

                    string type = SectorMetadataFileParser.SniffType(hpf.InputStream);
                    Sector meta = SectorMetadataFileParser.ForType(type).Parse(hpf.InputStream);
                    sector.Merge(meta);
                }
                else if (!string.IsNullOrEmpty(request.Form["metadata"]))
                {
                    string metadata = request.Form["metadata"];
                    string type = SectorMetadataFileParser.SniffType(metadata.ToStream());
                    var parser = SectorMetadataFileParser.ForType(type);
                    using var reader = new StringReader(metadata);
                    Sector meta = parser.Parse(reader);
                    sector.Merge(meta);
                }

                return sector;
            }
        }

        protected static void ApplyHexRotation(int hrot, Stylesheet stylesheet, ref Size bitmapSize, ref AbstractMatrix transform)
        {
            float degrees = -hrot;
            double radians = degrees * Math.PI / 180f;
            double newWidth = Math.Abs(Math.Sin(radians)) * bitmapSize.Height + Math.Abs(Math.Cos(radians)) * bitmapSize.Width;
            double newHeight = Math.Abs(Math.Sin(radians)) * bitmapSize.Width + Math.Abs(Math.Cos(radians)) * bitmapSize.Height;

            transform.TranslatePrepend((float)newWidth / 2, (float)newHeight / 2);
            transform.RotatePrepend(-degrees);
            transform.TranslatePrepend(-bitmapSize.Width / 2, -bitmapSize.Height / 2);
            bitmapSize.Width = (int)Math.Ceiling(newWidth);
            bitmapSize.Height = (int)Math.Ceiling(newHeight);

            stylesheet.hexRotation = (float)degrees;
            stylesheet.microBorders.textStyle.Rotation = degrees;
        }

        private static object s_pdf_serialization_lock = new object();
    }
}
