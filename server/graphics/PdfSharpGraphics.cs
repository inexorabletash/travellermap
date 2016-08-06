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
        private XSolidBrush brush;
        private XPen pen;

        public PdfSharpGraphics(XGraphics g) { this.g = g;
            this.brush = new XSolidBrush();
            this.pen = new XPen(Color.Empty);
        }

        private void Apply(AbstractBrush brush) { this.brush.Color = brush.Color; }
        private void Apply(AbstractPen pen) { this.pen.Color = pen.Color; this.pen.Width = pen.Width; this.pen.DashStyle = pen.DashStyle; }
        private void Apply(AbstractPen pen, AbstractBrush brush) { Apply(pen); Apply(brush); }

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

        public void DrawLine(AbstractPen pen, double x1, double y1, double x2, double y2) { Apply(pen); g.DrawLine(this.pen, x1, y1, x2, y2); }
        public void DrawLine(AbstractPen pen, PointF pt1, PointF pt2) { Apply(pen); g.DrawLine(this.pen, pt1, pt2); }
        public void DrawLines(AbstractPen pen, PointF[] points) { Apply(pen); g.DrawLines(this.pen, points); }
        public void DrawPath(AbstractPen pen, XGraphicsPath path) { Apply(pen);  g.DrawPath(this.pen, path); }
        public void DrawPath(AbstractBrush brush, XGraphicsPath path) { Apply(brush); g.DrawPath(this.brush, path); }
        public void DrawCurve(AbstractPen pen, PointF[] points, double tension) { Apply(pen); g.DrawCurve(this.pen, points, tension); }
        public void DrawClosedCurve(AbstractPen pen, PointF[] points, double tension) { Apply(pen); g.DrawClosedCurve(this.pen, points, tension); }
        public void DrawClosedCurve(AbstractBrush brush, PointF[] points, double tension) { Apply(brush); g.DrawClosedCurve(this.brush, points, XFillMode.Alternate, tension); }
        public void DrawRectangle(AbstractPen pen, double x, double y, double width, double height) { Apply(pen); g.DrawRectangle(this.pen, x, y, width, height); }
        public void DrawRectangle(AbstractBrush brush, double x, double y, double width, double height) { Apply(brush); g.DrawRectangle(this.brush, x, y, width, height); }
        public void DrawRectangle(AbstractBrush brush, RectangleF rect) { Apply(brush); g.DrawRectangle(this.brush, rect); }
        public void DrawEllipse(AbstractPen pen, double x, double y, double width, double height) { Apply(pen); g.DrawEllipse(this.pen, x, y, width, height); }
        public void DrawEllipse(AbstractBrush brush, double x, double y, double width, double height) { Apply(brush); g.DrawEllipse(this.brush, x, y, width, height); }
        public void DrawEllipse(AbstractPen pen, AbstractBrush brush, double x, double y, double width, double height) { Apply(pen, brush); g.DrawEllipse(this.pen, this.brush, x, y, width, height); }
        public void DrawArc(AbstractPen pen, double x, double y, double width, double height, double startAngle, double sweepAngle) { Apply(pen); g.DrawArc(this.pen, x, y, width, height, startAngle, sweepAngle); }
        public void DrawImage(AbstractImage image, double x, double y, double width, double height) { g.DrawImage(image.XImage, x, y, width, height); }

        public void DrawImageAlpha(float alpha, AbstractImage mimage, RectangleF targetRect)
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
        public void DrawString(string s, XFont font, AbstractBrush brush, double x, double y, XStringFormat format) { Apply(brush); g.DrawString(s, font, this.brush, x, y, format); }

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
