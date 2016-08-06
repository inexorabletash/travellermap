using PdfSharp.Drawing;
using System;
using System.Drawing;

namespace Maps.Rendering
{
    internal interface AbstractGraphics : IDisposable
    {
        XSmoothingMode SmoothingMode { get; set; }
        Graphics Graphics { get; }
        bool SupportsWingdings { get; }

        void ScaleTransform(double scaleXY);
        void ScaleTransform(double scaleX, double scaleY);
        void TranslateTransform(double dx, double dy);
        void RotateTransform(double angle);
        void MultiplyTransform(XMatrix m);

        void IntersectClip(XGraphicsPath path);
        void IntersectClip(RectangleF rect);

        void DrawLine(AbstractPen pen, double x1, double y1, double x2, double y2);
        void DrawLine(AbstractPen pen, PointF pt1, PointF pt2);
        void DrawLines(AbstractPen pen, XPoint[] points);
        void DrawPath(AbstractPen pen, XGraphicsPath path);
        void DrawPath(AbstractBrush brush, XGraphicsPath path);
        void DrawCurve(AbstractPen pen, PointF[] points, double tension = 0.5);
        void DrawClosedCurve(AbstractPen pen, PointF[] points, double tension = 0.5);
        void DrawClosedCurve(AbstractBrush brush, PointF[] points, double tension = 0.5);
        void DrawRectangle(AbstractPen pen, double x, double y, double width, double height);
        void DrawRectangle(AbstractBrush brush, double x, double y, double width, double height);
        void DrawRectangle(AbstractBrush brush, RectangleF rect);
        void DrawEllipse(AbstractPen pen, double x, double y, double width, double height);
        void DrawEllipse(AbstractBrush brush, double x, double y, double width, double height);
        void DrawEllipse(AbstractPen pen, AbstractBrush brush, double x, double y, double width, double height);
        void DrawArc(AbstractPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle);
        void DrawImage(AbstractImage image, double x, double y, double width, double height);
        void DrawImageAlpha(float alpha, AbstractImage image, Rectangle targetRect);

        XSize MeasureString(string text, XFont font);
        void DrawString(string s, XFont font, AbstractBrush brush, double x, double y, XStringFormat format);

        AbstractGraphicsState Save();
        void Restore(AbstractGraphicsState state);
    }
    internal interface AbstractGraphicsState { }
    internal class AbstractImage
    {
        private string path;
        private string url;
        private Image image;
        private XImage ximage;

        public string Url { get { return url; } }
        public XImage XImage
        {
            get
            {
                lock (this)
                {
                    if (ximage == null)
                        ximage = XImage.FromGdiPlusImage(Image);
                    return ximage;
                }
            }
        }
        public Image Image
        {
            get
            {
                lock (this)
                {
                    if (image == null)
                        image = Image.FromFile(path);
                    return image;
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
        public double Width { get; set; }
        public XDashStyle DashStyle { get; set; }
        public AbstractPen() { }
        public AbstractPen(Color color, double width = 1)
        {
            Color = color;
            Width = 1;
            DashStyle = XDashStyle.Solid;
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
}
