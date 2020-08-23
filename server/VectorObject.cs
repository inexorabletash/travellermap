using Maps.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;

namespace Maps.Rendering
{
#nullable enable
    public class MapObject
    {

    }

    public class VectorObject : MapObject
    {
        private Byte[]? pathDataTypes;

        public VectorObject()
        {
        }

        public string? Name { get; set; }


        internal float MinScale { get; set; }
        internal float MaxScale { get; set; }

        public float OriginX { get; set; }
        public float OriginY { get; set; }
        public float ScaleX { get; set; }
        public float ScaleY { get; set; }
        public float NameX { get; set; }
        public float NameY { get; set; }

        private RectangleF cachedBounds;
        public RectangleF Bounds
        {
            get
            {
                // Compute bounds if not already set
                if (cachedBounds.IsEmpty && PathDataPoints?.Length > 0)
                {
                    cachedBounds.Location = PathDataPoints[0];

                    for (int i = 1; i < PathDataPoints.Length; ++i)
                    {
                        PointF pt = PathDataPoints[i];
                        if (pt.X < cachedBounds.X) { float d = cachedBounds.X - pt.X; cachedBounds.X = pt.X; cachedBounds.Width += d; }
                        if (pt.Y < cachedBounds.Y) { float d = cachedBounds.Y - pt.Y; cachedBounds.Y = pt.Y; cachedBounds.Height += d; }

                        if (pt.X > cachedBounds.Right) { cachedBounds.Width = pt.X - cachedBounds.X; }
                        if (pt.Y > cachedBounds.Bottom) { cachedBounds.Height = pt.Y - cachedBounds.Y; }
                    }
                }
                return cachedBounds;
            }

            set { cachedBounds = value; }
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

        public PointF[]? PathDataPoints { get; set; }
        public Byte[] PathDataTypes
        {
            get
            {
                if (PathDataPoints == null)
                    throw new ApplicationException("Invalid VectorObject - PathDataPoints required");

                if (pathDataTypes == null)
                {
                    List<byte> types = new List<byte>(PathDataPoints.Length)
                    {
                        (byte)PathPointType.Start
                    };
                    for (int i = 1; i < PathDataPoints.Length; ++i)
                        types.Add((byte)PathPointType.Line);
                    pathDataTypes = types.ToArray();
                }

                return pathDataTypes;
            }
            set { pathDataTypes = value; }
        }

        internal AbstractPath? Path
        {
            get
            {
                if (PathDataPoints == null)
                    return null;

                return new AbstractPath(PathDataPoints, PathDataTypes);
            }
        }

        internal void Draw(AbstractGraphics graphics, RectangleF rect, AbstractPen pen)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));

            RectangleF bounds = TransformedBounds;
            if (bounds.IntersectsWith(rect))
            {
                var path = Path;
                using (graphics.Save())
                {
                    graphics.ScaleTransform(ScaleX, ScaleY);
                    graphics.TranslateTransform(-OriginX, -OriginY);
                    graphics.DrawPath(pen, path);
                }
            }
        }

        // Used for names from vector files (macro borders, rifts)
        internal void DrawName(AbstractGraphics graphics, RectangleF rect, AbstractFont font, AbstractBrush textBrush, LabelStyle labelStyle)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));

            RectangleF bounds = TransformedBounds;

            if (bounds.IntersectsWith(rect))
            {
                if (Name != null)
                {
                    string str = Name;
                    if (labelStyle.Uppercase)
                        str = str.ToUpperInvariant();
            
                    PointF pos = NamePosition;// PointF( bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2 );

                    using (graphics.Save())
                    {
                        graphics.TranslateTransform(pos.X, pos.Y);
                        graphics.ScaleTransform(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                        graphics.RotateTransform(-labelStyle.Rotation); // Rotate it

                        RenderUtil.DrawString(graphics, str, font, textBrush, 0, 0);
                    }
                }
            }
        }
    }


    [XmlRoot(ElementName = "Worlds")]
    public class WorldObjectCollection
    {
        [XmlElement("World")]
        public List<WorldObject> Worlds { get; } = new List<WorldObject>();
    }

    public class WorldObject : MapObject
    {
        public string? Name { get; set; }

        internal float MinScale { get; set; }
        internal float MaxScale { get; set; }

        public MapOptions MapOptions { get; set; }

        public Location Location { get; set; }

        public int LabelBiasX { get; set; } = 1;
        public int LabelBiasY { get; set; } = 1;


        internal void Paint(AbstractGraphics graphics, Color dotColor, AbstractBrush labelBrush, AbstractFont labelFont)
        {
            if (graphics == null)
                throw new ArgumentNullException(nameof(graphics));

            Point pt = Astrometrics.LocationToCoordinates(Location);

            using (graphics.Save())
            {
                graphics.TranslateTransform(pt.X, pt.Y);
                graphics.ScaleTransform(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);

                const float radius = 3;

                AbstractBrush brush = new AbstractBrush(dotColor);
                AbstractPen pen = new AbstractPen(dotColor);
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawEllipse(pen, brush, -radius / 2, -radius / 2, radius, radius);

                RenderUtil.TextFormat format;
                if (LabelBiasX > 0)
                    format = LabelBiasY < 0 ? RenderUtil.TextFormat.BottomLeft : LabelBiasY > 0 ? RenderUtil.TextFormat.TopLeft : RenderUtil.TextFormat.MiddleLeft;
                else if (LabelBiasX < 0)
                    format = LabelBiasY < 0 ? RenderUtil.TextFormat.BottomRight : LabelBiasY > 0 ? RenderUtil.TextFormat.TopRight : RenderUtil.TextFormat.MiddleRight;
                else
                    format = LabelBiasY < 0 ? RenderUtil.TextFormat.BottomCenter : LabelBiasY > 0 ? RenderUtil.TextFormat.TopCenter : RenderUtil.TextFormat.Center;

                float y = (LabelBiasY * radius / 2);
                float x = (LabelBiasX * radius / 2);

                RenderUtil.DrawString(graphics, Name, labelFont, labelBrush, x, y, format);
            }
        }

    }
#nullable restore
}
