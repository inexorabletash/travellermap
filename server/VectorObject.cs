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
        private PointF[] m_pathDataPoints;
        private Byte[] m_pathDataTypes;

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

        private RectangleF m_bounds;
        public RectangleF Bounds
        {
            get
            {
                // Compute bounds if not already set
                if (m_bounds.IsEmpty && m_pathDataPoints != null && m_pathDataPoints.Length > 0)
                {
                    m_bounds.Location = m_pathDataPoints[0];

                    for (int i = 1; i < m_pathDataPoints.Length; ++i)
                    {
                        PointF pt = m_pathDataPoints[i];
                        if (pt.X < m_bounds.X) { float d = m_bounds.X - pt.X; m_bounds.X = pt.X; m_bounds.Width += d; }
                        if (pt.Y < m_bounds.Y) { float d = m_bounds.Y - pt.Y; m_bounds.Y = pt.Y; m_bounds.Height += d; }

                        if (pt.X > m_bounds.Right) { m_bounds.Width = pt.X - m_bounds.X; }
                        if (pt.Y > m_bounds.Bottom) { m_bounds.Height = pt.Y - m_bounds.Y; }
                    }
                }
                return m_bounds;
            }

            set { m_bounds = value; }
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

        public PointF[] PathDataPoints { get { return m_pathDataPoints; } set { m_pathDataPoints = value; } }
        public Byte[] PathDataTypes
        {
            get
            {
                if (m_pathDataTypes == null)
                {
                    List<byte> types = new List<byte>(m_pathDataPoints.Length);
                    types.Add((byte)PathPointType.Start);
                    for (int i = 1; i < m_pathDataPoints.Length; ++i)
                        types.Add((byte)PathPointType.Line);
                    m_pathDataTypes = types.ToArray();
                }

                return m_pathDataTypes;
            }
            set { m_pathDataTypes = value; }
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

        #region EditingTools
#if DEBUG
        private bool flattened = false;
        public void Flatten( XMatrix matrix, double flatness )
        {
            lock( this )
            {
                if( flattened )
                    return;
                flattened = true;

                XGraphicsPath path = this.Path;
                path.Flatten( matrix, flatness );

                m_pathDataPoints = path.Internals.GdiPath.PathPoints;
                m_pathDataTypes = path.Internals.GdiPath.PathTypes;
            }
        }

        public void Decimate()
        {
            if (m_pathDataPoints == null || m_pathDataTypes == null)
                return;

            int length = m_pathDataPoints.Length;
            List<PointF> newPoints = new List<PointF>(length);
            List<byte> newTypes = new List<byte>(length);

            for (int i = 0; i < length; i++)
            {
                if (((PathPointType)(m_pathDataTypes[i]) != PathPointType.Line) ||
                    (i % 2 == 1))
                {
                    newPoints.Add(m_pathDataPoints[i]);
                    newTypes.Add(m_pathDataTypes[i]);
                }
            }

            m_pathDataPoints = newPoints.ToArray();
            m_pathDataTypes = newTypes.ToArray();
        }
#endif
        #endregion EditingTools

        public void Draw(XGraphics graphics, RectangleF rect, MapOptions options, XPen pen)
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

        internal void DrawName(XGraphics graphics, RectangleF rect, MapOptions options, XFont font, XBrush textBrush, LabelStyle labelStyle)
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


        public void Paint(XGraphics graphics, RectangleF rect, MapOptions options, Color dotColor, XBrush labelBrush, XFont labelFont)
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
