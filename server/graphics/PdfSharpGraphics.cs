using PdfSharp.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;

namespace Maps.Rendering
{
    internal class PdfSharpGraphics : AbstractGraphics
    {
        private XGraphics g;

        public PdfSharpGraphics(XGraphics g) { this.g = g; }
        public bool SupportsWingdings { get { return true; } }

        public XSmoothingMode SmoothingMode { get { return g.SmoothingMode; } set { g.SmoothingMode = value; } }
        public Graphics Graphics { get { return g.Graphics; } }

        public void ScaleTransform(double scaleXY) { g.ScaleTransform(scaleXY); }
        public void ScaleTransform(double scaleX, double scaleY) { g.ScaleTransform(scaleX, scaleY); }
        public void TranslateTransform(double dx, double dy) { g.TranslateTransform(dx, dy); }
        public void RotateTransform(double angle) { g.RotateTransform(angle); }
        public void MultiplyTransform(XMatrix m) { g.MultiplyTransform(m); }

        public void IntersectClip(XGraphicsPath path) { g.IntersectClip(path); }
        public void IntersectClip(RectangleF rect) { g.IntersectClip(rect); }

        public void DrawLine(XPen pen, double x1, double y1, double x2, double y2) { g.DrawLine(pen, x1, y1, x2, y2); }
        public void DrawLine(XPen pen, PointF pt1, PointF pt2) { g.DrawLine(pen, pt1, pt2); }
        public void DrawLines(XPen pen, XPoint[] points) { g.DrawLines(pen, points); }
        public void DrawPath(XPen pen, XGraphicsPath path) { g.DrawPath(pen, path); }
        public void DrawPath(XSolidBrush brush, XGraphicsPath path) { g.DrawPath(brush, path); }
        public void DrawCurve(XPen pen, PointF[] points, double tension) { g.DrawCurve(pen, points, tension); }
        public void DrawClosedCurve(XPen pen, PointF[] points, double tension) { g.DrawClosedCurve(pen, points, tension); }
        public void DrawClosedCurve(XSolidBrush brush, PointF[] points, double tension) { g.DrawClosedCurve(brush, points, XFillMode.Alternate, tension); }
        public void DrawRectangle(XPen pen, double x, double y, double width, double height) { g.DrawRectangle(pen, x, y, width, height); }
        public void DrawRectangle(XSolidBrush brush, double x, double y, double width, double height) { g.DrawRectangle(brush, x, y, width, height); }
        public void DrawRectangle(XSolidBrush brush, RectangleF rect) { g.DrawRectangle(brush, rect); }
        public void DrawEllipse(XPen pen, double x, double y, double width, double height) { g.DrawEllipse(pen, x, y, width, height); }
        public void DrawEllipse(XSolidBrush brush, double x, double y, double width, double height) { g.DrawEllipse(brush, x, y, width, height); }
        public void DrawEllipse(XPen pen, XSolidBrush brush, double x, double y, double width, double height) { g.DrawEllipse(pen, brush, x, y, width, height); }
        public void DrawArc(XPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle) { g.DrawArc(pen, x, y, width, height, startAngle, sweepAngle); }
        public void DrawImage(AbstractImage image, double x, double y, double width, double height) { g.DrawImage(image.XImage, x, y, width, height); }

        public void DrawImageAlpha(float alpha, AbstractImage mimage, Rectangle targetRect)
        {
            // Clamp and Quantize
            alpha = Util.Clamp(alpha, 0f, 1f);
            alpha = (float)Math.Round(alpha * 16f) / 16f;
            if (alpha <= 0f)
                return;
            if (alpha >= 1f)
            {
                g.DrawImage(mimage.XImage, targetRect);
                return;
            }

            int key = (int)Math.Round(alpha * 16);

            Image image = mimage.Image;
            XImage ximage;
            int w, h;

            lock (image)
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
                    dict[key] = ximage;
                }
            }

            lock (ximage)
            {
                g.DrawImage(ximage, targetRect);
            }
        }

        public XSize MeasureString(string text, XFont font) { return g.MeasureString(text, font); }
        public void DrawString(string s, XFont font, XSolidBrush brush, double x, double y, XStringFormat format) { g.DrawString(s, font, brush, x, y, format); }

        public AbstractGraphicsState Save() { return new MXGraphicsState(g.Save()); }
        public void Restore(AbstractGraphicsState state) { g.Restore(((MXGraphicsState)state).state); }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    g.Dispose();
                    g = null;
                }
                disposedValue = true;
            }
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
        #endregion

        private class MXGraphicsState : AbstractGraphicsState
        {
            public XGraphicsState state;
            public MXGraphicsState(XGraphicsState state) { this.state = state; }
        }

    }
}
