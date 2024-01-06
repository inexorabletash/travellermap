﻿#nullable enable
using Maps.Utilities;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;

namespace Maps.Graphics
{
    internal class PdfSharpGraphics : AbstractGraphics
    {
        private XGraphics g;
        private XSolidBrush brush;
        private XPen pen;

        public PdfSharpGraphics(XGraphics g)
        {
            this.g = g;
            brush = new XSolidBrush();
            pen = new XPen(Color.Empty);
        }

        private void Apply(AbstractBrush brush)
        {
            this.brush.Color = brush.Color;
        }
        private void Apply(AbstractPen pen)
        {
            this.pen.Color = pen.Color;
            this.pen.Width = pen.Width;
            switch (pen.DashStyle)
            {
                case DashStyle.Solid: this.pen.DashStyle = XDashStyle.Solid; break;
                case DashStyle.Dot: this.pen.DashStyle = XDashStyle.Dot; break;
                case DashStyle.Dash: this.pen.DashStyle = XDashStyle.Dash; break;
                case DashStyle.DashDot: this.pen.DashStyle = XDashStyle.DashDot; break;
                case DashStyle.DashDotDot: this.pen.DashStyle = XDashStyle.DashDotDot; break;
                case DashStyle.Custom: this.pen.DashStyle = XDashStyle.Custom; break;
            }
            if (pen.CustomDashPattern != null)
                this.pen.DashPattern = pen.CustomDashPattern.Select(f => (double)f).ToArray();
        }
        private void Apply(AbstractPen pen, AbstractBrush brush) { Apply(pen); Apply(brush); }

        private Dictionary<Font, XFont> fontMap = new Dictionary<Font, XFont>();
        private XFont Convert(Font font)
        {
            if (fontMap.ContainsKey(font))
                return fontMap[font];
            XFont xfont = new XFont(font, new XPdfFontOptions(PdfFontEncoding.Unicode));
            fontMap.Add(font, xfont);
            return xfont;
        }

        public bool SupportsWingdings => true;
        public SmoothingMode SmoothingMode { get => (SmoothingMode)g.SmoothingMode; set => g.SmoothingMode = (XSmoothingMode)value; }
        public System.Drawing.Graphics? Graphics => g.Graphics;
        public void ScaleTransform(float scaleXY) { g.ScaleTransform(scaleXY); }
        public void ScaleTransform(float scaleX, float scaleY) { g.ScaleTransform(scaleX, scaleY); }
        public void TranslateTransform(float dx, float dy) { g.TranslateTransform(dx, dy); }
        public void RotateTransform(float angle) { g.RotateTransform(angle); }
        public void MultiplyTransform(AbstractMatrix m) { g.MultiplyTransform(m.XMatrix); }

        public void IntersectClip(AbstractPath path) { g.IntersectClip(new XGraphicsPath(path.Points, path.Types, XFillMode.Winding)); }
        public void IntersectClip(RectangleF rect) { g.IntersectClip(rect); }

        public void DrawLine(AbstractPen pen, float x1, float y1, float x2, float y2) { Apply(pen); g.DrawLine(this.pen, x1, y1, x2, y2); }
        public void DrawLine(AbstractPen pen, PointF pt1, PointF pt2) { Apply(pen); g.DrawLine(this.pen, pt1, pt2); }
        public void DrawLines(AbstractPen pen, PointF[] points) { Apply(pen); g.DrawLines(this.pen, points); }
        public void DrawPath(AbstractPen pen, AbstractPath path) { Apply(pen); g.DrawPath(this.pen, new XGraphicsPath(path.Points, path.Types, XFillMode.Winding)); }
        public void DrawPath(AbstractBrush brush, AbstractPath path) { Apply(brush); g.DrawPath(this.brush, new XGraphicsPath(path.Points, path.Types, XFillMode.Winding)); }
        public void DrawCurve(AbstractPen pen, PointF[] points, float tension) { Apply(pen); g.DrawCurve(this.pen, points, tension); }
        public void DrawClosedCurve(AbstractPen pen, PointF[] points, float tension) { Apply(pen); g.DrawClosedCurve(this.pen, points, tension); }
        public void DrawClosedCurve(AbstractBrush brush, PointF[] points, float tension) { Apply(brush); g.DrawClosedCurve(this.brush, points, XFillMode.Alternate, tension); }
        public void DrawRectangle(AbstractPen pen, float x, float y, float width, float height) { Apply(pen); g.DrawRectangle(this.pen, x, y, width, height); }
        public void DrawRectangle(AbstractPen pen, RectangleF rect) { Apply(pen); g.DrawRectangle(this.pen, rect); }
        public void DrawRectangle(AbstractBrush brush, float x, float y, float width, float height) { Apply(brush); g.DrawRectangle(this.brush, x, y, width, height); }
        public void DrawRectangle(AbstractBrush brush, RectangleF rect) { Apply(brush); g.DrawRectangle(this.brush, rect); }
        public void DrawEllipse(AbstractPen pen, float x, float y, float width, float height) { Apply(pen); g.DrawEllipse(this.pen, x, y, width, height); }
        public void DrawEllipse(AbstractBrush brush, float x, float y, float width, float height) { Apply(brush); g.DrawEllipse(this.brush, x, y, width, height); }
        public void DrawEllipse(AbstractPen pen, AbstractBrush brush, float x, float y, float width, float height) { Apply(pen, brush); g.DrawEllipse(this.pen, this.brush, x, y, width, height); }
        public void DrawArc(AbstractPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) { Apply(pen); g.DrawArc(this.pen, x, y, width, height, startAngle, sweepAngle); }

        public void DrawImage(AbstractImage image, float x, float y, float width, float height)
        {
            XImage ximage = image.XImage;
            lock (ximage)
            {
                g.DrawImage(ximage, x, y, width, height);
            }
        }

        const int ALPHA_STEPS = 16;

        public void DrawImageAlpha(float alpha, AbstractImage mimage, RectangleF targetRect)
        {
            XImage ximage;

            // Clamp and Quantize
            alpha = alpha.Clamp(0f, 1f);
            alpha = (float)Math.Round(alpha * ALPHA_STEPS) / ALPHA_STEPS;
            if (alpha <= 0f)
                return;

            ximage = (alpha >= 1f) ? mimage.XImage : GetAlphaVariant(alpha, mimage);
            lock (ximage)
            {
                g.DrawImage(ximage, targetRect);
            }
        }

        private static XImage GetAlphaVariant(float alpha, AbstractImage mimage)
        {
            Image image = mimage.Image!;
            lock (image)
            {
                XImage ximage;

                int w = image.Width;
                int h = image.Height;
                int key = (int)Math.Round(alpha * ALPHA_STEPS);


                if (!(image.Tag is Dictionary<int, XImage> dict))
                    image.Tag = dict = new Dictionary<int, XImage>();

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
                    using (var scratchGraphics = System.Drawing.Graphics.FromImage(scratchBitmap))
                    {
                        ImageAttributes attr = new ImageAttributes();
                        attr.SetColorMatrix(new ColorMatrix()
                        {
                            Matrix00 = 1,
                            Matrix11 = 1,
                            Matrix22 = 1,
                            Matrix33 = alpha
                        });

                        scratchGraphics.DrawImage(image, new Rectangle(0, 0, w, h), 0, 0, w, h, GraphicsUnit.Pixel, attr);
                    }

                    ximage = XImage.FromGdiPlusImage(scratchBitmap);
                    dict[key] = ximage;
                }
                return ximage;
            }
        }

        public SizeF MeasureString(string text, AbstractFont font) => g.MeasureString(text, font.Font).ToSizeF();

        public void DrawString(string s, AbstractFont font, AbstractBrush brush, float x, float y, StringAlignment format)
        {
            Apply(brush);
            g.DrawString(s, Convert(font.Font), this.brush, x, y, Format(format));
        }

        public AbstractGraphicsState Save() => new State(this, g.Save());
        public void Restore(AbstractGraphicsState state) { g.Restore(((State)state).state); }

        #region StringFormats
        private static XStringFormat CreateStringFormat(XStringAlignment alignment, XLineAlignment lineAlignment)
        {
            XStringFormat format = new XStringFormat()
            {
                Alignment = alignment,
                LineAlignment = lineAlignment
            };
            return format;
        }

        private XStringFormat Format(StringAlignment alignment) =>
            alignment switch
            {
                StringAlignment.Centered => centeredFormat,
                StringAlignment.TopLeft => topLeftFormat,
                StringAlignment.TopCenter => topCenterFormat,
                StringAlignment.TopRight => topRightFormat,
                StringAlignment.CenterLeft => centerLeftFormat,
                StringAlignment.Baseline => defaultFormat,
                _ => throw new ApplicationException("Unhandled string alignment"),
            };

        private readonly XStringFormat defaultFormat = CreateStringFormat(XStringAlignment.Near, XLineAlignment.BaseLine);
        private readonly XStringFormat centeredFormat = CreateStringFormat(XStringAlignment.Center, XLineAlignment.Center);
        private readonly XStringFormat topLeftFormat = CreateStringFormat(XStringAlignment.Near, XLineAlignment.Near);
        private readonly XStringFormat topCenterFormat = CreateStringFormat(XStringAlignment.Center, XLineAlignment.Near);
        private readonly XStringFormat topRightFormat = CreateStringFormat(XStringAlignment.Far, XLineAlignment.Near);
        private readonly XStringFormat centerLeftFormat = CreateStringFormat(XStringAlignment.Near, XLineAlignment.Center);
        #endregion

        #region IDisposable Support
        private bool disposed = false;

        void IDisposable.Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
            if (disposing)
            {
                g.Dispose();
            }
            disposed = true;
        }
        #endregion

        private class State : AbstractGraphicsState
        {
            public XGraphicsState state;
            public State(AbstractGraphics g, XGraphicsState state) : base(g) { this.state = state; }
        }
    }
}
