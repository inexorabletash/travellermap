using PdfSharp.Drawing;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Maps.Graphics
{
    internal interface AbstractGraphics : IDisposable
    {
        SmoothingMode SmoothingMode { get; set; }
        System.Drawing.Graphics Graphics { get; }
        bool SupportsWingdings { get; }

        void ScaleTransform(float scaleXY);
        void ScaleTransform(float scaleX, float scaleY);
        void TranslateTransform(float dx, float dy);
        void RotateTransform(float angle);
        void MultiplyTransform(AbstractMatrix m);

        void IntersectClip(AbstractPath path);
        void IntersectClip(RectangleF rect);

        void DrawLine(AbstractPen pen, float x1, float y1, float x2, float y2);
        void DrawLine(AbstractPen pen, PointF pt1, PointF pt2);
        void DrawLines(AbstractPen pen, PointF[] points);
        void DrawPath(AbstractPen pen, AbstractPath path);
        void DrawPath(AbstractBrush brush, AbstractPath path);
        void DrawCurve(AbstractPen pen, PointF[] points, float tension = 0.5f);
        void DrawClosedCurve(AbstractPen pen, PointF[] points, float tension = 0.5f);
        void DrawClosedCurve(AbstractBrush brush, PointF[] points, float tension = 0.5f);
        void DrawRectangle(AbstractPen pen, float x, float y, float width, float height);
        void DrawRectangle(AbstractPen pen, RectangleF rect);
        void DrawRectangle(AbstractBrush brush, float x, float y, float width, float height);
        void DrawRectangle(AbstractBrush brush, RectangleF rect);
        void DrawEllipse(AbstractPen pen, float x, float y, float width, float height);
        void DrawEllipse(AbstractBrush brush, float x, float y, float width, float height);
        void DrawEllipse(AbstractPen pen, AbstractBrush brush, float x, float y, float width, float height);
        void DrawArc(AbstractPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle);

        void DrawImage(AbstractImage image, float x, float y, float width, float height);
        void DrawImageAlpha(float alpha, AbstractImage image, RectangleF targetRect);

        SizeF MeasureString(string text, Font font);
        void DrawString(string s, Font font, AbstractBrush brush, float x, float y, StringAlignment format);

        AbstractGraphicsState Save();
        void Restore(AbstractGraphicsState state);
    }

    internal abstract class AbstractGraphicsState : IDisposable {

        private AbstractGraphics g;

        protected AbstractGraphicsState(AbstractGraphics graphics)
        {
            g = graphics;
        }

        public void Restore()
        {
            g.Restore(this);
            g = null;
        }

        #region IDisposable Members

        public void Dispose()
        {
            g?.Restore(this);
            g = null;
        }

        #endregion
    }


    // This is a concrete class (despite the name) since we want instances without a factory
    internal struct AbstractMatrix
    {
        public XMatrix matrix;

        public AbstractMatrix(float m11, float m12, float m21, float m22, float dx, float dy)
        {
            matrix = new XMatrix(m11, m12, m21, m22, dx, dy);
        }

        public float M11 => (float)matrix.M11;
        public float M12 => (float)matrix.M12;
        public float M21 => (float)matrix.M21;
        public float M22 => (float)matrix.M22;
        public float OffsetX => (float)matrix.OffsetX;
        public float OffsetY => (float)matrix.OffsetY;

        public void Invert() { matrix.Invert(); }
        public void RotatePrepend(float angle) { matrix.RotatePrepend(angle); }
        public void ScalePrepend(float sx, float sy) { matrix.ScalePrepend(sx, sy); }
        public void TranslatePrepend(float dx, float dy) { matrix.TranslatePrepend(dx, dy); }

        public XMatrix XMatrix => matrix;
        public Matrix Matrix => matrix.ToGdiMatrix();

        public static readonly AbstractMatrix Identity = new AbstractMatrix(1, 0, 0, 1, 0, 0);
    }


    // This is a concrete class (despite the name) since we want static instances held by the server which
    // span different concrete instances.
    internal class AbstractImage
    {
        private string path;
        private string url;
        private Image image;
        private XImage ximage;

        public string Url => url;

        private string dataUrl;
        public string DataUrl
        {
            get
            {
                if (dataUrl == null)
                {
                    string contentType = Utilities.ContentTypes.TypeForPath(path);
                    byte[] bytes = System.IO.File.ReadAllBytes(path);
                    dataUrl = "data:" + contentType + ";base64," + Convert.ToBase64String(bytes, Base64FormattingOptions.None);
                }
                return dataUrl;
            }
        }

        public XImage XImage
        {
            get
            {
                lock (this)
                {
                    return ximage ?? (ximage = XImage.FromGdiPlusImage(Image));
                }
            }
        }
        public Image Image
        {
            get
            {
                lock (this)
                {
                    return image ?? (image = Image.FromFile(path));
                }
            }
        }

        public AbstractImage(string path, string url)
        {
            this.path = path;
            this.url = url;
        }
    }

    internal class AbstractPen
    {
        public Color Color { get; set; }
        public float Width { get; set; }
        public DashStyle DashStyle { get; set; } = DashStyle.Solid;
        public float[] CustomDashPattern { get; set; }

        public AbstractPen() { }
        public AbstractPen(Color color, float width = 1)
        {
            Color = color;
            Width = width;
        }
    }

    internal class AbstractBrush
    {
        public Color Color { get; set; }
        public AbstractBrush() { }
        public AbstractBrush(Color color)
        {
            Color = color;
        }
    }

    internal enum StringAlignment
    {
        Baseline,
        Centered,
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
    };

    internal class AbstractPath
    {
        public PointF[] Points { get; set; }
        public byte[] Types { get; set; }

        public AbstractPath(PointF[] points, byte[] types)
        {
            Points = points;
            Types = types;
        }
    }

    internal enum DashStyle
    {
        Solid,
        Dot,
        Dash,
        DashDot,
        DashDotDot,
        Custom,
    }
}
