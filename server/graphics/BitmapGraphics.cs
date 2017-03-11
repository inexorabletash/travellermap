using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Maps.Rendering
{
    internal class BitmapGraphics : AbstractGraphics
    {
        private Graphics g;
        private SolidBrush brush;
        private Pen pen;

        public BitmapGraphics(Graphics g)
        {
            this.g = g;
            this.brush = new SolidBrush(Color.Empty);
            this.pen = new Pen(Color.Empty);
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
                case DashStyle.Solid: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Solid; break;
                case DashStyle.Dot: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot; break;
                case DashStyle.Dash: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash; break;
                case DashStyle.DashDot: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot; break;
                case DashStyle.DashDotDot: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.DashDotDot; break;
                case DashStyle.Custom: this.pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom; break;
            }
            if (pen.CustomDashPattern != null)
                this.pen.DashPattern = pen.CustomDashPattern;
        }
        private void Apply(AbstractPen pen, AbstractBrush brush) { Apply(pen); Apply(brush); }

        public bool SupportsWingdings => true;
        public SmoothingMode SmoothingMode { get => g.SmoothingMode; set => g.SmoothingMode = value; }
        public Graphics Graphics => g;
        public void ScaleTransform(float scaleXY) { g.ScaleTransform(scaleXY, scaleXY); }
        public void ScaleTransform(float scaleX, float scaleY) { g.ScaleTransform(scaleX, scaleY); }
        public void TranslateTransform(float dx, float dy) { g.TranslateTransform(dx, dy); }
        public void RotateTransform(float angle) { g.RotateTransform(angle); }
        public void MultiplyTransform(AbstractMatrix m) { g.MultiplyTransform(m.Matrix); }

        public void IntersectClip(AbstractPath path) { g.IntersectClip(new Region(new GraphicsPath(path.Points, path.Types, FillMode.Winding))); }
        public void IntersectClip(RectangleF rect) { g.IntersectClip(rect); }

        public void DrawLine(AbstractPen pen, float x1, float y1, float x2, float y2) { Apply(pen); g.DrawLine(this.pen, x1, y1, x2, y2); }
        public void DrawLine(AbstractPen pen, PointF pt1, PointF pt2) { Apply(pen); g.DrawLine(this.pen, pt1, pt2); }
        public void DrawLines(AbstractPen pen, PointF[] points) { Apply(pen); g.DrawLines(this.pen, points); }
        public void DrawPath(AbstractPen pen, AbstractPath path) { Apply(pen); g.DrawPath(this.pen, new GraphicsPath(path.Points, path.Types, FillMode.Winding)); }
        public void DrawPath(AbstractBrush brush, AbstractPath path) { Apply(brush); g.FillPath(this.brush, new GraphicsPath(path.Points, path.Types, FillMode.Winding)); }
        public void DrawCurve(AbstractPen pen, PointF[] points, float tension) { Apply(pen); g.DrawCurve(this.pen, points,tension); }
        public void DrawClosedCurve(AbstractPen pen, PointF[] points, float tension) { Apply(pen); g.DrawClosedCurve(this.pen, points, tension, FillMode.Winding); }
        public void DrawClosedCurve(AbstractBrush brush, PointF[] points, float tension) { Apply(brush); g.FillClosedCurve(this.brush, points, FillMode.Winding, tension); }
        public void DrawRectangle(AbstractPen pen, float x, float y, float width, float height) { Apply(pen); g.DrawRectangle(this.pen, x, y, width, height); }
        public void DrawRectangle(AbstractPen pen, RectangleF rect) { Apply(pen); g.DrawRectangle(this.pen, rect.X, rect.Y, rect.Width, rect.Height); }
        public void DrawRectangle(AbstractBrush brush, float x, float y, float width, float height) { Apply(brush); g.FillRectangle(this.brush, x, y, width, height); }
        public void DrawRectangle(AbstractBrush brush, RectangleF rect) { Apply(brush); g.FillRectangle(this.brush, rect); }
        public void DrawEllipse(AbstractPen pen, float x, float y, float width, float height) { Apply(pen); g.DrawEllipse(this.pen, x, y, width, height); }
        public void DrawEllipse(AbstractBrush brush, float x, float y, float width, float height) { Apply(brush); g.FillEllipse(this.brush, x, y, width, height); }
        public void DrawEllipse(AbstractPen pen, AbstractBrush brush, float x, float y, float width, float height) { Apply(pen, brush); g.FillEllipse(this.brush, x, y, width, height); g.DrawEllipse(this.pen, x, y, width, height); }
        public void DrawArc(AbstractPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle) { Apply(pen); g.DrawArc(this.pen, x, y, width, height, startAngle, sweepAngle); }

        public void DrawImage(AbstractImage image, float x, float y, float width, float height) { g.DrawImage(image.Image, x, y, width, height); }
        public void DrawImageAlpha(float alpha, AbstractImage image, RectangleF targetRect)
        {
            if (alpha <= 0)
                return;
            if (alpha >= 1)
            {
                DrawImage(image, targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height);
                return;
            }

            Image gdiImage = image.Image;
            lock(gdiImage)
            {
                ColorMatrix matrix = new ColorMatrix()
                {
                    Matrix00 = 1,
                    Matrix11 = 1,
                    Matrix22 = 1,
                    Matrix33 = alpha
                };
                ImageAttributes attr = new ImageAttributes();
                attr.SetColorMatrix(matrix);

                PointF[] dest = new PointF[]
                {
                    new PointF(targetRect.Left, targetRect.Top),
                    new PointF(targetRect.Right, targetRect.Top),
                    new PointF(targetRect.Left, targetRect.Bottom)
                };

                g.DrawImage(gdiImage, dest, new RectangleF(0, 0, gdiImage.Width, gdiImage.Height), GraphicsUnit.Pixel, attr);
            }
        }

        public SizeF MeasureString(string text, Font font)
        {
            return g.MeasureString(text, font);
        }

        public void DrawString(string s, Font font, AbstractBrush brush, float x, float y, StringAlignment format)
        {
            Apply(brush);
            if (format == StringAlignment.Baseline)
            {
                float fontUnitsToWorldUnits = font.Size / font.FontFamily.GetEmHeight(font.Style);
                float ascent = font.FontFamily.GetCellAscent(font.Style) * fontUnitsToWorldUnits;
                g.DrawString(s, font, this.brush, x, y - ascent);
            }
            else
            {
                g.DrawString(s, font, this.brush, x, y, Format(format));
            }
        }

        public AbstractGraphicsState Save() { return new State(this, g.Save()); }
        public void Restore(AbstractGraphicsState state) { g.Restore(((State)state).state); }

        #region StringFormats
        private static StringFormat CreateStringFormat(System.Drawing.StringAlignment alignment, System.Drawing.StringAlignment lineAlignment)
        {
            StringFormat format = new StringFormat()
            {
                Alignment = alignment,
                LineAlignment = lineAlignment
            };
            return format;
        }

        private StringFormat Format(StringAlignment alignment)
        {
            switch (alignment)
            {
                case StringAlignment.Centered: return centeredFormat;
                case StringAlignment.TopLeft: return topLeftFormat;
                case StringAlignment.TopCenter: return topCenterFormat;
                case StringAlignment.TopRight: return topRightFormat;
                case StringAlignment.CenterLeft: return centerLeftFormat;
                case StringAlignment.Baseline: return defaultFormat;
                default: throw new ApplicationException("Unhandled string alignment");
            }
        }

        private readonly StringFormat defaultFormat = StringFormat.GenericDefault;//CreateStringFormat(System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Far);
        private readonly StringFormat centeredFormat = CreateStringFormat(System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Center);
        private readonly StringFormat topLeftFormat = CreateStringFormat(System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Near);
        private readonly StringFormat topCenterFormat = CreateStringFormat(System.Drawing.StringAlignment.Center, System.Drawing.StringAlignment.Near);
        private readonly StringFormat topRightFormat = CreateStringFormat(System.Drawing.StringAlignment.Far, System.Drawing.StringAlignment.Near);
        private readonly StringFormat centerLeftFormat = CreateStringFormat(System.Drawing.StringAlignment.Near, System.Drawing.StringAlignment.Center);
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (disposedValue)
                return;
            if (disposing)
            {
                g?.Dispose();
                brush?.Dispose();
                pen?.Dispose();
            }
            g = null;
            brush = null;
            pen = null;
            disposedValue = true;
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
        }
        #endregion

        private class State : AbstractGraphicsState
        {
            public GraphicsState state;
            public State(AbstractGraphics g, GraphicsState state) : base(g) { this.state = state; }
        }
    }
}
