using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;

namespace Maps.Rendering
{
    public class MapObject
    {

    }

    public class VectorObject : MapObject
    {
        private PointF[] pathDataPoints;
        private Byte[] pathDataTypes;

        public VectorObject()
        {
        }

        public string Name { get; set; }


        internal float MinScale { get; set; }
        internal float MaxScale { get; set; }

        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float NameX { get; set; }
        public float NameY { get; set; }

        private RectangleF bounds;
        public RectangleF Bounds
        {
            get
            {
                // Compute bounds if not already set
                if (bounds.IsEmpty && pathDataPoints != null && pathDataPoints.Length > 0)
                {
                    bounds.Location = pathDataPoints[0];

                    for (int i = 1; i < pathDataPoints.Length; ++i)
                    {
                        PointF pt = pathDataPoints[i];
                        if (pt.X < bounds.X) { float d = bounds.X - pt.X; bounds.X = pt.X; bounds.Width += d; }
                        if (pt.Y < bounds.Y) { float d = bounds.Y - pt.Y; bounds.Y = pt.Y; bounds.Height += d; }

                        if (pt.X > bounds.Right) { bounds.Width = pt.X - bounds.X; }
                        if (pt.Y > bounds.Bottom) { bounds.Height = pt.Y - bounds.Y; }
                    }
                }
                return bounds;
            }

            set { bounds = value; }
        }

        internal RectangleF TransformedBounds
        {
            get
            {
                RectangleF bounds = Bounds;

                bounds.X -= OriginX;
                bounds.Y -= OriginY;

                bounds.X *= ScaleX;
                bounds.Y *= ScaleY;
                bounds.Width *= ScaleX;
                bounds.Height *= ScaleY;
                if (bounds.Width < 0)
                {
                    bounds.X += bounds.Width;
                    bounds.Width = -bounds.Width;
                }
                if (bounds.Height < 0)
                {
                    bounds.Y += bounds.Height;
                    bounds.Height = -bounds.Height;
                }

                return bounds;
            }
        }

        internal PointF NamePosition
        {
            get
            {
                RectangleF bounds = TransformedBounds;

                PointF center = new PointF(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);

                center.X += bounds.Width * (NameX / Bounds.Width);
                center.Y += bounds.Height * (NameY / Bounds.Height);

                return center;
            }
        }


        public MapOptions MapOptions { get; set; }

        public PointF[] PathDataPoints { get { return pathDataPoints; } set { pathDataPoints = value; } }
        public Byte[] PathDataTypes
        {
            get
            {
                if (pathDataTypes == null)
                {
                    List<byte> types = new List<byte>(pathDataPoints.Length);
                    types.Add((byte)PathPointType.Start);
                    for (int i = 1; i < pathDataPoints.Length; ++i)
                        types.Add((byte)PathPointType.Line);
                    pathDataTypes = types.ToArray();
                }

                return pathDataTypes;
            }
            set { pathDataTypes = value; }
        }

        // NOTE: Can't cacheResults a GraphicsPath - not free threaded
        internal XGraphicsPath Path
        {
            get
            {
                if (PathDataPoints == null)
                    return null;

                return new XGraphicsPath(PathDataPoints, PathDataTypes, XFillMode.Alternate);
            }
        }

        public void Draw(XGraphics graphics, RectangleF rect, XPen pen)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");

            RectangleF bounds = TransformedBounds;

            //graphics.DrawRectangle( new XPen(XColors.Yellow, 1), bounds.X, bounds.Y, bounds.Width, bounds.Height );

            if (bounds.IntersectsWith(rect))
            {
                XGraphicsPath path = Path;
                using (RenderUtil.SaveState(graphics))
                {
                    XMatrix matrix = new XMatrix();
                    matrix.ScalePrepend(ScaleX, ScaleY);
                    matrix.TranslatePrepend(-OriginX, -OriginY);
                    graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);
                    graphics.DrawPath(pen, path);
                }
            }
        }

        internal void DrawName(XGraphics graphics, RectangleF rect, XFont font, XBrush textBrush, LabelStyle labelStyle)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");

            RectangleF bounds = TransformedBounds;

            if (bounds.IntersectsWith(rect))
            {
                if (Name != null)
                {
                    string str = Name;
                    if (labelStyle.Uppercase)
                        str = str.ToUpperInvariant();
            
                    PointF pos = NamePosition;// PointF( bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2 );

                    using (RenderUtil.SaveState(graphics))
                    {
                        XMatrix matrix = new XMatrix();
                        matrix.TranslatePrepend(pos.X, pos.Y);
                        matrix.ScalePrepend(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                        matrix.RotatePrepend(-labelStyle.Rotation); // Rotate it
                        graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                        XSize size = graphics.MeasureString(str, font);
                        graphics.TranslateTransform(-size.Width / 2, -size.Height / 2); // Center the text
                        RectangleF textBounds = new RectangleF(0, 0, (float)size.Width, (float)size.Height * 2); // *2 or it gets cut off at high sizes

                        XTextFormatter tf = new XTextFormatter(graphics);
                        tf.Alignment = XParagraphAlignment.Center;
                        tf.DrawString(str, font, textBrush, textBounds);
                    }
                }
            }
        }

        public void Fill(XGraphics graphics, RectangleF rect, Brush fillBrush)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");

            RectangleF bounds = TransformedBounds;

            if (bounds.IntersectsWith(rect))
            {
                XGraphicsPath path = Path;

                using (RenderUtil.SaveState(graphics))
                {
                    XMatrix matrix = new XMatrix();
                    matrix.ScalePrepend(ScaleX, ScaleY);
                    matrix.TranslatePrepend(-OriginX, -OriginY);
                    graphics.MultiplyTransform(matrix);
                    graphics.DrawPath(fillBrush, path);
                }
            }
        }
    }


    [XmlRoot(ElementName = "Worlds")]
    public class WorldObjectCollection
    {
        public WorldObjectCollection()
        {
            Worlds = new List<WorldObject>();
        }

        [XmlElement("World")]
        public List<WorldObject> Worlds { get; }
    }


    public class WorldObject : MapObject
    {
        public WorldObject()
        {
            LabelBiasX = 1;
            LabelBiasY = 1;
        }

        public string Name { get; set; }

        internal float MinScale { get; set; }
        internal float MaxScale { get; set; }

        public MapOptions MapOptions { get; set; }

        public Location Location { get; set; }

        public int LabelBiasX { get; set; }
        public int LabelBiasY { get; set; }


        public void Paint(XGraphics graphics, Color dotColor, XBrush labelBrush, XFont labelFont)
        {
            if (graphics == null)
                throw new ArgumentNullException("graphics");

            Point pt = Astrometrics.LocationToCoordinates(Location);

            using (RenderUtil.SaveState(graphics))
            {

                graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                graphics.TranslateTransform(pt.X, pt.Y);
                graphics.ScaleTransform(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);

                const float radius = 3;

                XBrush brush = new XSolidBrush(dotColor);
                XPen pen = new XPen(dotColor);
                graphics.DrawEllipse(brush, -radius / 2, -radius / 2, radius, radius);

                graphics.SmoothingMode = XSmoothingMode.HighQuality;
                graphics.DrawEllipse(pen, -radius / 2, -radius / 2, radius, radius);

                XStringFormat format = (LabelBiasX == -1) ? RenderUtil.StringFormatTopRight :
                    (LabelBiasX == 1) ? RenderUtil.StringFormatTopLeft : RenderUtil.StringFormatTopCenter;

                XSize size = graphics.MeasureString(Name, labelFont);
                XPoint pos = new XPoint(0, 0);

                //pos.X += ( LabelBiasX * radius / 2 ) + ( -size.Width  * ( 1 - LabelBiasX ) / 2.0f );
                pos.Y += (LabelBiasY * radius / 2) + (-size.Height * (1 - LabelBiasY) / 2.0f);
                pos.X += (LabelBiasX * radius / 2);
                //pos.Y += ( LabelBiasY * radius / 2 );

                graphics.DrawString(Name, labelFont, labelBrush, pos.X, pos.Y, format);

            }
        }

    }

}
