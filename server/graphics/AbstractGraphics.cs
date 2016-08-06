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

        void DrawLine(XPen pen, double x1, double y1, double x2, double y2);
        void DrawLine(XPen pen, PointF pt1, PointF pt2);
        void DrawLines(XPen pen, XPoint[] points);
        void DrawPath(XPen pen, XGraphicsPath path);
        void DrawPath(XSolidBrush brush, XGraphicsPath path);
        void DrawCurve(XPen pen, PointF[] points, double tension = 0.5);
        void DrawClosedCurve(XPen pen, PointF[] points, double tension = 0.5);
        void DrawClosedCurve(XSolidBrush brush, PointF[] points, double tension = 0.5);
        void DrawRectangle(XPen pen, double x, double y, double width, double height);
        void DrawRectangle(XSolidBrush brush, double x, double y, double width, double height);
        void DrawRectangle(XSolidBrush brush, RectangleF rect);
        void DrawEllipse(XPen pen, double x, double y, double width, double height);
        void DrawEllipse(XSolidBrush brush, double x, double y, double width, double height);
        void DrawEllipse(XPen pen, XSolidBrush brush, double x, double y, double width, double height);
        void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle);
        void DrawImage(AbstractImage image, double x, double y, double width, double height);
        void DrawImageAlpha(float alpha, AbstractImage image, Rectangle targetRect);

        XSize MeasureString(string text, XFont font);
        void DrawString(string s, XFont font, XSolidBrush brush, double x, double y, XStringFormat format);

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
}
