using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text.RegularExpressions;

namespace Maps.Rendering
{
    // Wrapper to allow locking, since Image is [MarshalByRefObject]
    internal class ImageHolder
    {
        public ImageHolder(Image image) { this.image = image; }
        public Image Image { get { return image; } }
        private Image image;
    }

    internal static class RenderUtil
    {
        /*
                    1
            +-*------------*x+
            |/              \|
            /                \
           /|                |\
          * |                +x*  x = tan( pi / 6 ) / 4
           \|                |/
            \                /
            |\              /|
            +-*------------*-+
        */
        public static readonly double HEX_EDGE = Math.Tan(Math.PI / 6) / 4 / Astrometrics.ParsecScaleX;
        public static readonly float HEX_EDGE_F = (float)HEX_EDGE;

        private static readonly float[] HexEdgesX = { -0.5f + HEX_EDGE_F, -0.5f - HEX_EDGE_F, -0.5f + HEX_EDGE_F, 0.5f - HEX_EDGE_F, 0.5f + HEX_EDGE_F, 0.5f - HEX_EDGE_F };
        private static readonly float[] HexEdgesY = { 0.5f, 0f, -0.5f, -0.5f, 0, 0.5f };

        private static readonly float[] SquareEdgesX = { -0.5f, -0.5f, -0.5f, 0.5f, 0.5f, 0.5f };
        private static readonly float[] SquareEdgesY = { 0.5f, 0f, -0.5f, -0.5f, 0, 0.5f };

        public static void HexEdges(PathUtil.PathType type, out float[] edgeX, out float[] edgeY)
        {
            edgeX = (type == PathUtil.PathType.Hex) ? RenderUtil.HexEdgesX : RenderUtil.SquareEdgesX;
            edgeY = (type == PathUtil.PathType.Hex) ? RenderUtil.HexEdgesY : RenderUtil.SquareEdgesY;
        }

        public static void DrawImageAlpha(MGraphics graphics, float alpha, ImageHolder holder, Rectangle targetRect)
        {
            if (alpha <= 0f)
                return;

            // Clamp and Quantize
            alpha = Math.Min(1f, alpha);
            alpha = (float)Math.Round(alpha * 16f) / 16f;
            int key = (int)Math.Round(alpha * 16);

            Image image = holder.Image;
            XImage ximage;
            int w, h;

            lock (holder)
            {
                w = image.Width;
                h = image.Height;

                if (image.Tag == null || !(image.Tag is Dictionary<int, XImage>))
                    image.Tag = new Dictionary<int, XImage>();

                Dictionary<int, XImage> dict = image.Tag as Dictionary<int, XImage>;
                if (dict.ContainsKey(key))
                {
                    ximage = dict[key];
                }
                else
                {
                    if (alpha >= 1f)
                    {
                        ximage = XImage.FromGdiPlusImage(image);
                    }
                    else
                    {
                        // Need to construct a new image (PdfSharp can't alpha-render images)
                        // Memoize these in the image itself, since most requests will be from
                        // a small set

                        Bitmap scratchBitmap = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                        using (var scratchGraphics = Graphics.FromImage(scratchBitmap))
                        {
                            ColorMatrix matrix = new ColorMatrix();
                            matrix.Matrix00 = matrix.Matrix11 = matrix.Matrix22 = 1;
                            matrix.Matrix33 = alpha;

                            ImageAttributes attr = new ImageAttributes();
                            attr.SetColorMatrix(matrix);

                            scratchGraphics.DrawImage(image, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
                        }

                        ximage = XImage.FromGdiPlusImage(scratchBitmap);
                    }
                    dict[key] = ximage;
                }
            }

            lock (ximage)
            {
                graphics.DrawImage(ximage, targetRect, new Rectangle(0, 0, w, h), XGraphicsUnit.Point);
            }
        }

        public static void DrawGlyph(MGraphics g, Glyph glyph, FontCache styleRes, XSolidBrush brush, float x, float y)
        {
            XFont font = glyph.Font == GlyphFont.Ding ? styleRes.WingdingFont : styleRes.GlyphFont;
            g.DrawString(glyph.Characters, font, brush, x, y, StringFormatCentered);
        }


        private static XStringFormat CreateStringFormat(XStringAlignment alignment, XLineAlignment lineAlignment)
        {
            XStringFormat format = new XStringFormat();
            format.Alignment = alignment;
            format.LineAlignment = lineAlignment;
            return format;
        }

        public static XStringFormat StringFormatCentered { get { return centeredFormat; } }
        private static readonly XStringFormat centeredFormat = CreateStringFormat(XStringAlignment.Center, XLineAlignment.Center);

        public static XStringFormat StringFormatTopLeft { get { return topLeftFormat; } }
        private static readonly XStringFormat topLeftFormat = CreateStringFormat(XStringAlignment.Near, XLineAlignment.Near);

        public static XStringFormat StringFormatTopCenter { get { return topCenterFormat; } }
        private static readonly XStringFormat topCenterFormat = CreateStringFormat(XStringAlignment.Center, XLineAlignment.Near);

        public static XStringFormat StringFormatTopRight { get { return topRightFormat; } }
        private static readonly XStringFormat topRightFormat = CreateStringFormat(XStringAlignment.Far, XLineAlignment.Near);

        public static XStringFormat StringFormatCenterLeft { get { return centerLeftFormat; } }
        private static readonly XStringFormat centerLeftFormat = CreateStringFormat(XStringAlignment.Near, XLineAlignment.Center);

        public static void DrawLabel(MGraphics g, string text, PointF labelPos, XFont font, XSolidBrush brush, LabelStyle labelStyle)
        {
            using (RenderUtil.SaveState(g))
            {
                if (labelStyle.Uppercase)
                    text = text.ToUpper();
                if (labelStyle.Wrap)
                    text = text.Replace(' ', '\n');

                g.TranslateTransform(labelPos.X, labelPos.Y);
                g.ScaleTransform(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                g.TranslateTransform(labelStyle.Translation.X, labelStyle.Translation.Y);
                g.RotateTransform(labelStyle.Rotation);
                g.ScaleTransform(labelStyle.Scale.Width, labelStyle.Scale.Height);

                if (labelStyle.Rotation != 0 && g.Graphics != null)
                    g.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

                XSize size = g.MeasureString(text, font);
                size.Width *= 2; // prevent cut-off e.g. when rotated
                var bounds = new RectangleF((float)(-size.Width / 2), (float)(-size.Height / 2), (float)size.Width, (float)size.Height);

                var tf = new MTextFormatter(g);
                tf.Alignment = XParagraphAlignment.Center;
                tf.DrawString(text, font, brush, bounds);
            }
        }

        public static SaveGraphicsState SaveState(MGraphics g)
        {
            return new SaveGraphicsState(g);
        }

        sealed internal class SaveGraphicsState : IDisposable
        {
            private MGraphics g;
            private MGraphicsState gs;

            public SaveGraphicsState(MGraphics graphics)
            {
                g = graphics;
                gs = graphics.Save();
            }

            #region IDisposable Members

            public void Dispose()
            {
                if (g != null && gs != null)
                {
                    g.Restore(gs);
                    g = null;
                    gs = null;
                }
            }

            #endregion
        }

    }

    public enum GlyphFont
    {
        Ding,
        Normal
    }

    internal struct Glyph
    {
        public enum GlyphBias
        {
            None,
            Top,
            Bottom
        }
        public GlyphFont Font { get; set; }
        public string Characters { get; set; }
        public GlyphBias Bias { get; set; }
        public bool IsHighlighted { get; set; }

        public Glyph(GlyphFont font, string chars)
            : this()
        {
            Font = font;
            Characters = chars;
            Bias = GlyphBias.None;
            IsHighlighted = false;
        }
        public bool Printable
        {
            get { return Characters.Length > 0; }
        }
        public Glyph Highlight
        {
            get
            {
                Glyph g = this;
                g.IsHighlighted = true;
                return g;
            }
        }
        public Glyph BiasBottom
        {
            get
            {
                Glyph g = this;
                g.Bias = GlyphBias.Bottom;
                return g;
            }
        }

        public Glyph BiasTop
        {
            get
            {
                Glyph g = this;
                g.Bias = GlyphBias.Top;
                return g;
            }
        }


        public static readonly Glyph None = new Glyph(GlyphFont.Normal, "");

        // NOTE: Windings are often used instead of UNICODE equivalents in a common font 
        // because the glyphs are much higher quality.
        // See http://www.alanwood.net/demos/wingdings.html for a good mapping

#if USE_WINGDINGS
        public static readonly Glyph Diamond = new Glyph(GlyphFont.Ding, "\x74"); // U+2666 (BLACK DIAMOND SUIT)
        public static readonly Glyph DiamondX = new Glyph(GlyphFont.Ding, "\x76"); // U+2756 (BLACK DIAMOND MINUS WHITE X)
        public static readonly Glyph Circle = new Glyph(GlyphFont.Ding, "\x9f"); // Alternates: U+2022 (BULLET), U+25CF (BLACK CIRCLE)
        public static readonly Glyph Triangle = new Glyph(GlyphFont.Normal, "\x25B2"); // U+25B2 (BLACK UP-POINTING TRIANGLE)
        public static readonly Glyph Square = new Glyph(GlyphFont.Normal, "\x25A0"); // U+25A0 (BLACK SQUARE)
        public static readonly Glyph Star3Point = new Glyph(GlyphFont.Ding, "\xA9"); // U+25B2 (BLACK UP-POINTING TRIANGLE)
        public static readonly Glyph Star4Point = new Glyph(GlyphFont.Ding, "\xAA"); // U+2726 (BLACK FOUR POINTED STAR)
        public static readonly Glyph Star5Point = new Glyph(GlyphFont.Ding, "\xAB"); // U+2605 (BLACK STAR)
        public static readonly Glyph Star6Point = new Glyph(GlyphFont.Ding, "\xAC"); // U+2736 (BLACK SIX POINTED STAR)
        public static readonly Glyph WhiteStar = new Glyph(GlyphFont.Normal, "\u2606"); // U+2606 (WHITE STAR)
        public static readonly Glyph StarStar = new Glyph(GlyphFont.Normal, "**"); // Would prefer U+2217 (ASTERISK OPERATOR) but font coverage is poor
#else
        public static readonly Glyph Diamond = new Glyph(GlyphFont.Normal, "\x2666"); // U+2666 (BLACK DIAMOND SUIT)
        public static readonly Glyph DiamondX = new Glyph(GlyphFont.Normal, "\x2756"); // U+2756 (BLACK DIAMOND MINUS WHITE X)
        public static readonly Glyph Circle = new Glyph(GlyphFont.Normal, "\x2022"); // Alternates: U+2022 (BULLET), U+25CF (BLACK CIRCLE)
        public static readonly Glyph Triangle = new Glyph(GlyphFont.Normal, "\x25B2"); // U+25B2 (BLACK UP-POINTING TRIANGLE)
        public static readonly Glyph Square = new Glyph(GlyphFont.Normal, "\x25A0"); // U+25A0 (BLACK SQUARE)
        public static readonly Glyph Star3Point = new Glyph(GlyphFont.Normal, "\x25B2"); // U+25B2 (BLACK UP-POINTING TRIANGLE)
        public static readonly Glyph Star4Point = new Glyph(GlyphFont.Normal, "\x2726"); // U+2726 (BLACK FOUR POINTED STAR)
        public static readonly Glyph Star5Point = new Glyph(GlyphFont.Normal, "\x2605"); // U+2605 (BLACK STAR)
        public static readonly Glyph Star6Point = new Glyph(GlyphFont.Normal, "\x2736"); // U+2736 (BLACK SIX POINTED STAR)
        public static readonly Glyph WhiteStar = new Glyph(GlyphFont.Normal, "\u2606"); // U+2606 (WHITE STAR)
        public static readonly Glyph StarStar = new Glyph(GlyphFont.Normal, "**"); // Would prefer U+2217 (ASTERISK OPERATOR) but font coverage is poor
#endif

        // Research Stations
        public static readonly Glyph Alpha = new Glyph(GlyphFont.Normal, "\x0391").Highlight;
        public static readonly Glyph Beta = new Glyph(GlyphFont.Normal, "\x0392").Highlight;
        public static readonly Glyph Gamma = new Glyph(GlyphFont.Normal, "\x0393").Highlight;
        public static readonly Glyph Delta = new Glyph(GlyphFont.Normal, "\x0394").Highlight;
        public static readonly Glyph Epsilon = new Glyph(GlyphFont.Normal, "\x0395").Highlight;
        public static readonly Glyph Zeta = new Glyph(GlyphFont.Normal, "\x0396").Highlight;
        public static readonly Glyph Eta = new Glyph(GlyphFont.Normal, "\x0397").Highlight;
        public static readonly Glyph Theta = new Glyph(GlyphFont.Normal, "\x0398").Highlight;

        // Other Textual
        public static readonly Glyph Prison = new Glyph(GlyphFont.Normal, "P").Highlight;
        public static readonly Glyph Reserve = new Glyph(GlyphFont.Normal, "R");
        public static readonly Glyph ExileCamp = new Glyph(GlyphFont.Normal, "X");


        public static Glyph FromResearchCode(string rs)
        {
            Glyph glyph = Glyph.Gamma;

            if (rs.Length == 3)
            {
                char c = rs[2];
                switch (c)
                {
                    case 'A': glyph = Glyph.Alpha; break;
                    case 'B': glyph = Glyph.Beta; break;
                    case 'G': glyph = Glyph.Gamma; break;
                    case 'D': glyph = Glyph.Delta; break;
                    case 'E': glyph = Glyph.Epsilon; break;
                    case 'Z': glyph = Glyph.Zeta; break;
                    case 'H': glyph = Glyph.Eta; break;
                    case 'T': glyph = Glyph.Theta; break;
                }
            }
            return glyph;
        }

        private static readonly RegexDictionary<Glyph> s_baseGlyphTable = new GlobDictionary<Glyph> {
            { "*.C", Glyph.StarStar.BiasBottom }, // Vargr Corsair Base
            { "Im.D", Glyph.Square.BiasBottom }, // Imperial Depot
            { "*.D", Glyph.Square.Highlight}, // Depot
            { "*.E", Glyph.StarStar.BiasBottom }, // Hiver Embassy
            { "*.K", Glyph.Star5Point.Highlight.BiasTop }, // Naval Base
            { "*.M", Glyph.Star4Point.BiasBottom }, // Military Base
            { "*.N", Glyph.Star5Point.BiasTop }, // Imperial Naval Base
            { "*.O", Glyph.Square.Highlight.BiasTop }, // K'kree Naval Outpost (non-standard)
            { "*.R", Glyph.StarStar.BiasBottom }, // Aslan Clan Base
            { "*.S", Glyph.Triangle.BiasBottom }, // Imperial Scout Base
            { "*.T", Glyph.Star5Point.Highlight.BiasTop }, // Aslan Tlaukhu Base
            { "*.V", Glyph.Circle.BiasBottom }, // Exploration Base
            { "Zh.W", Glyph.Diamond.Highlight }, // Zhodani Relay Station
            { "*.W", Glyph.Triangle.Highlight.BiasBottom }, // Imperial Scout Waystation
            { "Zh.Z", Glyph.Diamond }, // Zhodani Base (Special case for "Zh.KM")
            { "*.*", Glyph.Circle }, // Independent Base
        };

        public static Glyph FromBaseCode(string allegiance, char code)
        {
            return s_baseGlyphTable.Match(allegiance + "." + code);
        }
    }

    // BorderPath is a render-ready representation of a Border.
    // It contains three separate chunks of data:
    // * Pair of (points, types) arrays for "straight" borders to draw/clip
    // * List of curves (open or closed plus array of control points) for "curved" borders
    // Straight borders are always complete closed polygons, since they are rendered
    // clipped against the sector hex bounds.
    // Curved borders must be segmented since simple clipping against sector bounds
    // is insufficient (they weave against the hexes).
    internal class BorderPath
    {
        public class CurveSegment
        {
            public CurveSegment(IEnumerable<PointF> points, bool closed)
            {
                this.points = points.ToArray();
                this.closed = closed;
            }
            public readonly PointF[] points;
            public readonly bool closed;
        }

        public readonly PointF[] points;
        public readonly byte[] types;
        public readonly List<CurveSegment> curves;

        public BorderPath(Border border, Sector sector, PathUtil.PathType type)
        {
            float[] edgeX, edgeY;
            RenderUtil.HexEdges(type, out edgeX, out edgeY);

            int lengthEstimate = border.Path.Count() * 3;

            List<PointF> points = new List<PointF>(lengthEstimate);
            List<byte> types = new List<byte>(lengthEstimate);
            LinkedList<LinkedList<PointF>> segments = new LinkedList<LinkedList<PointF>>();
            LinkedList<PointF> currentSegment = new LinkedList<PointF>();

            // Based on http://dotclue.org/t20/sec2pdf - J Greely rocks my world.

            int checkFirst = 0;
            int checkLast = 5;

            Hex startHex = Hex.Empty;
            bool startHexVisited = false;

            foreach (Hex hex in border.Path)
            {
                checkLast = checkFirst + 5;

                if (startHexVisited && hex == startHex)
                {
                    // I'm in the starting hex, and I've been
                    // there before, so stop testing at neighbor
                    // 5, no matter what
                    checkLast = 5;

                    // degenerate case, entering for third time
                    if (checkFirst < 3)
                        break;
                }
                else if (!startHexVisited)
                {
                    startHex = hex;
                    startHexVisited = true;

                    // PERF: This seems costly... analyze it!
                    PointF newPoint = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, hex)));
                    newPoint.X += edgeX[0];
                    newPoint.Y += edgeY[0];

                    // MOVETO
                    points.Add(newPoint);
                    types.Add((byte)PathPointType.Start);

                    // MOVETO
                    currentSegment.AddLast(newPoint);
                }

                PointF pt = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, hex)));

                int i = checkFirst;
                for (int check = checkFirst; check <= checkLast; check++)
                {
                    i = check;
                    Hex neighbor = Astrometrics.HexNeighbor(hex, i % 6);

                    if (border.Path.Contains(neighbor)) // TODO: Consider a hash here
                        break;

                    PointF newPoint = new PointF(pt.X + edgeX[(i + 1) % 6], pt.Y + edgeY[(i + 1) % 6]);

                    // LINETO
                    points.Add(newPoint);
                    types.Add((byte)PathPointType.Line);

                    if (hex.IsValid)
                    {
                        // MOVETO
                        currentSegment.AddLast(newPoint);
                    }
                    else
                    {
                        // LINETO
                        if (currentSegment.Count > 1)
                            segments.AddLast(currentSegment);
                        currentSegment = new LinkedList<PointF>();
                        currentSegment.AddLast(newPoint);
                    }

                }
                i = i % 6;
                // i is the direction to the next border hex,
                // and when we get there, we'll have come from
                // i + 3, so we start checking with i + 4.
                checkFirst = (i + 4) % 6;
            }

            types[types.Count - 1] |= (byte)PathPointType.CloseSubpath;

            if (currentSegment.Count > 1)
                segments.AddLast(currentSegment);

            this.points = points.ToArray();
            this.types = types.ToArray();

            // If last curve segment connects to first curve segment, merge them.
            // Example: Imperial border in Verge.
            if (segments.Count >= 2 && segments.First().First() == segments.Last().Last())
            {
                var first = segments.First();
                var last = segments.Last();
                segments.RemoveFirst();
                first.RemoveFirst();
                foreach (var point in first)
                    last.AddLast(point);
            }

            this.curves = segments.Select(c =>
            {
                if (c.First() == c.Last())
                {
                    c.RemoveLast();
                    return new CurveSegment(c, true);
                }
                else
                {
                    return new CurveSegment(c, false);
                }
            }).ToList();
        }
    }

    internal static class ColorUtil
    {
        public static void RGBtoXYZ(int r, int g, int b, out double x, out double y, out double z)
        {
            double rl = (double)r / 255.0;
            double gl = (double)g / 255.0;
            double bl = (double)b / 255.0;

            double sr = (rl > 0.04045) ? Math.Pow((rl + 0.055) / (1 + 0.055), 2.2) : (rl / 12.92);
            double sg = (gl > 0.04045) ? Math.Pow((gl + 0.055) / (1 + 0.055), 2.2) : (gl / 12.92);
            double sb = (bl > 0.04045) ? Math.Pow((bl + 0.055) / (1 + 0.055), 2.2) : (bl / 12.92);

            x = sr * 0.4124 + sg * 0.3576 + sb * 0.1805;
            y = sr * 0.2126 + sg * 0.7152 + sb * 0.0722;
            z = sr * 0.0193 + sg * 0.1192 + sb * 0.9505;
        }

        private static double Fxyz(double t)
        {
            return ((t > 0.008856) ? Math.Pow(t, (1.0 / 3.0)) : (7.787 * t + 16.0 / 116.0));
        }

        public static void XYZtoLab(double x, double y, double z, out double l, out double a, out double b)
        {
            const double D65X = 0.9505, D65Y = 1.0, D65Z = 1.0890;
            l = 116.0 * Fxyz(y / D65Y) - 16;
            a = 500.0 * (Fxyz(x / D65X) - Fxyz(y / D65Y));
            b = 200.0 * (Fxyz(y / D65Y) - Fxyz(z / D65Z));
        }

        public static double DeltaE76(double l1, double a1, double b1, double l2, double a2, double b2)
        {
            double c1 = l1 - l2, c2 = a1 - a2, c3 = b1 - b2;
            return Math.Sqrt(c1 * c1 + c2 * c2 + c3 * c3);
        }

        public static bool NoticeableDifference(Color a, Color b)
        {
            const double JND = 2.3;

            double ax, ay, az;
            double bx, by, bz;
            RGBtoXYZ(a.R, a.G, a.B, out ax, out ay, out az);
            RGBtoXYZ(b.R, b.G, b.B, out bx, out by, out bz);

            double al, aa, ab;
            double bl, ba, bb;
            XYZtoLab(ax, ay, az, out al, out aa, out ab);
            XYZtoLab(bx, by, bz, out bl, out ba, out bb);

            return DeltaE76(al, aa, ab, bl, ba, bb) > JND;
        }

    }
    internal static class PathUtil
    {
        public enum PathType : int
        {
            Hex = 0,
            Square = 1,
            TypeCount = 2
        };

        public static RectangleF Bounds(XGraphicsPath path)
        {
            RectangleF rect = new RectangleF();

            PointF[] points = path.Internals.GdiPath.PathPoints;

            rect.X = points[0].X;
            rect.Y = points[0].Y;

            for (int i = 1; i < points.Length; ++i)
            {
                PointF pt = points[i];
                if (pt.X < rect.X)
                {
                    float d = rect.X - pt.X;
                    rect.X = pt.X;
                    rect.Width += d;
                }
                if (pt.Y < rect.Y)
                {
                    float d = rect.Y - pt.Y;
                    rect.Y = pt.Y;
                    rect.Height += d;
                }

                if (pt.X > rect.Right)
                    rect.Width = pt.X - rect.X;
                if (pt.Y > rect.Bottom)
                    rect.Height = pt.Y - rect.Y;
            }

            return rect;
        }

        public static void ComputeBorderPath(IEnumerable<Point> clip, float[] edgeX, float[] edgeY, out PointF[] clipPathPointCoords, out byte[] clipPathPointTypes)
        {
            // TODO: Consolidate this with border path generation (which is very sector/hex-centric, alas)

            List<PointF> clipPathPoints = new List<PointF>(clip.Count() * 3);
            List<byte> clipPathTypes = new List<byte>(clip.Count() * 3);

            // Algorithm based on http://dotclue.org/t20/sec2pdf - J Greely rocks my world.

            int checkFirst = 0;
            int checkLast = 5;

            Point startHex = Point.Empty; ;
            bool startHexVisited = false;

            foreach (Point hex in clip)
            {
                checkLast = checkFirst + 5;

                if (startHexVisited && hex == startHex)
                {
                    // I'm in the starting hex, and I've been
                    // there before, so stop testing at neighbor
                    // 5, no matter what
                    checkLast = 5;

                    // degenerate case, entering for third time
                    if (checkFirst < 3)
                        break;
                }
                else if (!startHexVisited)
                {
                    startHex = hex;
                    startHexVisited = true;

                    // PERF: This seems costly... analyze it!
                    PointF newPoint = Astrometrics.HexToCenter(hex);
                    newPoint.X += edgeX[0];
                    newPoint.Y += edgeY[0];

                    // MOVETO
                    clipPathPoints.Add(newPoint);
                    clipPathTypes.Add((byte)PathPointType.Start);
                }

                PointF pt = Astrometrics.HexToCenter(hex);

                int i = checkFirst;
                for (int check = checkFirst; check <= checkLast; check++)
                {
                    i = check;
                    Point neighbor = Astrometrics.HexNeighbor(hex, i % 6);

                    if (clip.Contains(neighbor))
                        break;

                    PointF newPoint = new PointF(pt.X + edgeX[(i + 1) % 6], pt.Y + edgeY[(i + 1) % 6]);

                    // LINETO
                    clipPathPoints.Add(newPoint);
                    clipPathTypes.Add((byte)PathPointType.Line);
                }
                i = i % 6;

                // i is the direction to the next border hex,
                // and when we get there, we'll have come from
                // i + 3, so we start checking with i + 4.
                checkFirst = (i + 4) % 6;
            }

            clipPathPointCoords = clipPathPoints.ToArray();
            clipPathPointTypes = clipPathTypes.ToArray();
            clipPathPointTypes[clipPathPointTypes.Length - 1] |= (byte)PathPointType.CloseSubpath;
        }

    }

    internal struct StarProps
    {
        public StarProps(Color color, Color border, float radius) { this.color = color; this.borderColor = border;  this.radius = radius; }
        public Color color;
        public Color borderColor;
        public float radius;
    }

    internal static class StellarRendering
    {
        // Match a single non-degenerate star.
        private static Regex STAR_REGEX = new Regex(@"^([OBAFGKM])([0-9]) ?(Ia|Ib|II|III|IV|V)$",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Additions to radius based on luminosity.
        private static Dictionary<string, float> LUM = new Dictionary<string, float>
            {
                { "Ia", 7 },
                { "Ib", 5 },
                { "II", 3 },
                { "III", 2 },
                { "IV", 1 },
                { "V", 0 }
            };

        // Base radius for spectral class.
        private static Dictionary<string, float> RAD = new Dictionary<string, float>
            { { "O", 4 }, { "B", 3 }, { "A", 2 }, { "F", 1.5f }, { "G", 1 }, { "K", 0.7f }, { "M", 0.5f } };

        // Maps spectral class to color.
        private static Dictionary<string, Color> COLOR = new Dictionary<string, Color> {
                { "O", Color.FromArgb(0x9d, 0xb4, 0xff) },
                { "B", Color.FromArgb(0xbb, 0xcc, 0xff) },
                { "A", Color.FromArgb(0xfb, 0xf8, 0xff) },
                { "F", Color.FromArgb(0xff, 0xff, 0xed) },
                { "G", Color.FromArgb(0xff, 0xff, 0x00) },
                { "K", Color.FromArgb(0xff, 0x98, 0x33) },
                { "M", Color.FromArgb(0xff, 0x00, 0x00) },
            };

        public static StarProps star2props(string star)
        {
            Match m = STAR_REGEX.Match(star);
            if (m.Success)
            {
                string c = m.Groups[1].Value, f = m.Groups[2].Value, l = m.Groups[3].Value;
                return new StarProps(COLOR[c], Color.Black, RAD[c] + LUM[l]);
            }
            else if (star == "BH")
            {
                return new StarProps(Color.Black, Color.White, 0.8f);
            }
            else if (star == "BD")
            {
                return new StarProps(Color.Brown, Color.Black, 0.3f);
            }
            else
            {
                // Assume white dwarf
                return new StarProps(Color.White, Color.Black, 0.3f);
            }
        }

        private static float sinf(double r) { return (float)Math.Sin(r); }
        private static float cosf(double r) { return (float)Math.Cos(r); }
        private static float[] dx = new float[] {
                    0.0f,
                    cosf(Math.PI * 1 / 3),cosf(Math.PI * 2 / 3),cosf(Math.PI * 3 / 3),
                    cosf(Math.PI * 4 / 3),cosf(Math.PI * 5 / 3),cosf(Math.PI * 6 / 3) };
        private static float[] dy = new float[] {
                    0.0f,
                    sinf(Math.PI * 1 / 3),sinf(Math.PI * 2 / 3),sinf(Math.PI * 3 / 3),
                    sinf(Math.PI * 4 / 3),sinf(Math.PI * 5 / 3),sinf(Math.PI * 6 / 3) };
        public static PointF Offset(int index)
        {
            if (index >= dx.Length)
                index = (index % (dx.Length - 1)) + 1;
            return new PointF(dx[index], dy[index]);
        }
    }
}
