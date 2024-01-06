﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Maps.Graphics
{
    internal class SVGGraphics : AbstractGraphics
    {
        // G6 precision is needed for rendering far away from Charted Space, e.g. Legend
        private const string NumberFormat = "G6";
        private static string F(float f) => f.ToString(NumberFormat);

        private class Element
        {
#pragma warning disable IDE1006 // Naming Styles
            public string name { get; set; }
            public string? content;
            public Dictionary<string, string> attributes = new Dictionary<string, string>();
            public List<Element> children = new List<Element>();
#pragma warning restore IDE1006 // Naming Styles

            public Element(string name) { this.name = name; }

            public Element Append(Element child) { children.Add(child); return child; }

            public void Serialize(TextWriter b)
            {
                b.Write("<");
                b.Write(name);
                foreach (KeyValuePair<string, string> entry in attributes)
                {
                    b.Write(" ");
                    b.Write(entry.Key);
                    b.Write("=\"");
                    b.Write(System.Security.SecurityElement.Escape(entry.Value));
                    b.Write("\"");
                }
                if (children.Count == 0 && string.IsNullOrWhiteSpace(content))
                {
                    b.Write("/>");
                    return;
                }
                b.Write(">");

                foreach (var child in children)
                    child.Serialize(b);

                if (!string.IsNullOrWhiteSpace(content))
                    b.Write(System.Security.SecurityElement.Escape(content));

                b.Write("</");
                b.Write(name);
                b.Write(">");
            }

            public bool Has(string name) => attributes.ContainsKey(name);
            public string Get(string name) => attributes[name];
            public void Set(string name, string value) { attributes[name] = value; }
            public void Set(string name, float value) { attributes[name] = F(value); }
            public void Set(string name, Color color)
            {
                if (color.IsEmpty || color.A == 0)
                {
                    attributes[name] = "none";
                }
                else if (color.A < 255)
                {
                    attributes[name] = ColorTranslator.ToHtml(color);
                    attributes[name + "-opacity"] = (color.A / 255f).ToString("G2");
                }
                else
                {
                    attributes[name] = ColorTranslator.ToHtml(color);
                    attributes.Remove(name + "-opacity");
                }
            }

            // Helpers for common attributes
            public string Id { get => Get("id"); set => Set("id", value); }

            public void Apply(AbstractPen? pen)
            {
                if (pen == null)
                {
                    Set("stroke", Color.Empty);
                }
                else
                {
                    Set("stroke", pen.Color);
                    Set("stroke-width", pen.Width);

                    switch (pen.DashStyle)
                    {
                        case DashStyle.Solid:
                            // "solid" is SVG default
                            break;
                        case DashStyle.Dot:
                            Set("stroke-dasharray", $"{pen.Width:G2} {pen.Width:G2}");
                            break;
                        case DashStyle.Dash:
                            Set("stroke-dasharray", $"{pen.Width * 2:G2} {pen.Width:G2}");
                            break;
                        case DashStyle.DashDot:
                            Set("stroke-dasharray", $"{pen.Width * 2:G2} {pen.Width:G2} {pen.Width:G2} {pen.Width:G2}");
                            break;
                        case DashStyle.DashDotDot:
                            Set("stroke-dasharray", $"{pen.Width * 2:G2} {pen.Width:G2} {pen.Width:G2} {pen.Width:G2} {pen.Width:G2} {pen.Width:G2}");
                            break;
                        case DashStyle.Custom:
                            if (pen.CustomDashPattern == null)
                                throw new ApplicationException("Custom dash style specified but no pattern set");
                            Set("stroke-dasharray",
                                string.Join(" ", pen.CustomDashPattern.Select(w => F(w * pen.Width))));
                            break;
                    }
                }
            }
            public void Apply(AbstractBrush? brush)
            {
                Set("fill", brush?.Color ?? Color.Empty);
            }
            public void Apply(AbstractPen? pen, AbstractBrush? brush)
            {
                Apply(pen);
                Apply(brush);
            }

            public int NodeCount => 1 + children.Sum(c => c.NodeCount);
        }

        private class ElementNames
        {
            private ElementNames() { }

            public const string DEFS = "defs";
            public const string USE = "use";
            public const string CLIPPATH = "clipPath";

            public const string G = "g";
            public const string PATH = "path";
            public const string LINE = "line";
            public const string POLYLINE = "polyline";
            public const string RECT = "rect";
            public const string ELLIPSE = "ellipse";
            public const string CIRCLE = "circle";
            public const string TEXT = "text";
            public const string IMAGE = "image";
        }

        // Builds paths, using relative coordinates to reduce space.
        private class PathBuilder
        {
            private StringBuilder b = new StringBuilder();
            private float lastX = 0;
            private float lastY = 0;
            private bool used = false;

            public override string ToString() => b.ToString().Trim();

            public PathBuilder() { }

            public void MoveTo(float x, float y)
            {
                if (!used)
                {
                    b.Append($"M{F(x)},{F(y)}");
                    used = true;
                }
                else
                {
                    b.Append($"m{F(x - lastX)},{F(y - lastY)}");
                }
                lastX = x;
                lastY = y;
            }
            public void LineTo(float x, float y)
            {
                if (!used)
                {
                    b.Append($"L{F(x)},{F(y)}");
                    used = true;
                }
                else if (x == lastX)
                {
                    b.Append($"v{F(y - lastY)}");
                }
                else if (y == lastY)
                {
                    b.Append($"h{F(x - lastX)}");
                }
                else
                {
                    b.Append($"l{F(x - lastX)},{F(y - lastY)}");
                }
                lastX = x;
                lastY = y;
            }
            public void ArcTo(float rx, float ry, float phi, int arcFlag, int sweepFlag, float x, float y)
            {
                if (!used)
                {
                    b.Append($"A{F(rx)},{F(ry)},{F(phi)},{arcFlag},{sweepFlag},{F(x)},{F(y)}");
                    used = true;
                }
                else
                {
                    b.Append($"a{F(rx)},{F(ry)},{F(phi)},{arcFlag},{sweepFlag},{F(x - lastX)},{F(y - lastY)}");
                }
                lastX = x;
                lastY = y;
            }
            public void CurveTo(float x1, float y1, float x2, float y2, float x, float y)
            {
                if (!used)
                {
                    b.Append($"C,{F(x1)},{F(y1)},{F(x2)},{F(y2)},{F(x)},{F(y)}");
                    used = true;
                }
                else
                {
                    b.Append($"c{F(x1 - lastX)},{F(y1 - lastY)},{F(x2 - lastX)},{F(y2 - lastY)},{F(x - lastX)},{F(y - lastY)}");
                }
                lastX = x;
                lastY = y;
            }

            public void Close()
            {
                b.Append("Z");
                used = false; // Since initial point isn't remembered.
            }
        }

        private void Optimize(Element e)
        {
            // Simplify subtrees first
            foreach (var ch in e.children)
                Optimize(ch);

            // Remove <g>s with no children
            e.children.RemoveAll(ch => ch.name == ElementNames.G && ch.children.Count == 0);

            // Flatten <g> with no properties
            List<Element> c = new List<Element>();
            foreach (var ch in e.children)
            {
                if (ch.name == ElementNames.G && ch.attributes.Count == 0)
                    c.AddRange(ch.children);
                else
                    c.Add(ch);
            }
            e.children = c;

            // If a <g> has only a single child, merge
            if (e.name != ElementNames.G || e.children.Count != 1)
                return;

            var child = e.children.First();

            // Can't merge clip-paths, and clip needs to come before child transform.
            // TODO: Other exclusive elements?
            if (e.Has("clip-path") && (child.Has("clip-path") || child.Has("transform")))
                return;

            e.name = child.name;
            e.children = child.children;
            e.content = child.content;
            foreach (var entry in child.attributes)
            {
                if (e.attributes.ContainsKey(entry.Key))
                {
                    if (entry.Key != "transform")
                        throw new ApplicationException("Only know how to combine 'transform' attributes");
                    e.attributes[entry.Key] += " " + entry.Value;
                }
                else
                {
                    e.attributes[entry.Key] = entry.Value;
                }
            }
        }

        public void Serialize(TextWriter writer)
        {
            Optimize(root);

            writer.WriteLine("<?xml version = \"1.0\" encoding=\"utf-8\"?>");
            writer.Write(
                "<svg version=\"1.1\" baseProfile=\"full\" " +
                "xmlns=\"http://www.w3.org/2000/svg\" " +
                "xmlns:xlink=\"http://www.w3.org/1999/xlink\" " +
                $"width=\"{width}\" height=\"{height}\" " +
                $"viewBox=\"0 0 {width} {height}\" " +
                $"clip=\"0 {width} 0 {height}\">");
            if (defs.children.Count > 0)
                defs.Serialize(writer);
            root.Serialize(writer);
            writer.Write("</svg>");
            writer.Flush();
        }

        private float width;
        private float height;
        private Element root = new Element(ElementNames.G);
        private Element defs = new Element(ElementNames.DEFS);
        private Dictionary<AbstractImage, string> images = new Dictionary<AbstractImage, string>();

        private int def_id = 0;
        private Element AddDefinition(Element element)
        {
            element.Id = "did" + (++def_id).ToString(CultureInfo.InvariantCulture);
            defs.Append(element);
            return element;
        }

        private Stack<Element> stack = new Stack<Element>();

        private Element Current => stack.Peek();
        private Element Open(Element element)
        {
            stack.Push(Current.Append(element));
            return element;
        }
        private Element Append(Element element) => Current.Append(element);

        public SVGGraphics(float width, float height)
        {
            this.width = width;
            this.height = height;

            root.Set("fill", "None");
            root.Set("stroke", "None");
            stack.Push(root);
        }

        System.Drawing.Graphics? AbstractGraphics.Graphics => null;
        SmoothingMode AbstractGraphics.SmoothingMode { get; set; }
        public bool SupportsWingdings => false;
        #region Drawing

        public void DrawLine(AbstractPen pen, float x1, float y1, float x2, float y2)
        {
            var e = Append(new Element(ElementNames.LINE));
            e.Set("x1", x1);
            e.Set("y1", y1);
            e.Set("x2", x2);
            e.Set("y2", y2);
            e.Apply(pen);
        }

        public void DrawLines(AbstractPen pen, PointF[] points)
        {
            var e = Append(new Element(ElementNames.PATH));
            var path = new PathBuilder();
            path.MoveTo(points[0].X, points[0].Y);
            for (var i = 0; i < points.Length; ++i)
                path.LineTo(points[i].X, points[i].Y);
            e.Set("d", path.ToString());
            e.Apply(pen, null);
        }

        public void DrawArc(AbstractPen pen, float x, float y, float width, float height, float startAngle, float sweepAngle)
        {
            // Convert from center to endpoint parameterization
            // https://www.w3.org/TR/SVG/implnote.html#ArcImplementationNotes

            float rx = width / 2;
            float ry = height / 2;
            float cx = x + rx;
            float cy = y + ry;

            // GDI+ uses angles in degrees, clockwise from x axis
            startAngle = -startAngle * (float)Math.PI / 180;
            sweepAngle = -sweepAngle * (float)Math.PI / 180;

            // Since phi is always 0, conversion is simplified
            const float phi = 0;

            float x1 = rx * (float)Math.Cos(startAngle) + cx;
            float y1 = -ry * (float)Math.Sin(startAngle) + cy;
            float x2 = rx * (float)Math.Cos(startAngle + sweepAngle) + cx;
            float y2 = -ry * (float)Math.Sin(startAngle + sweepAngle) + cy;

            int fA = Math.Abs(sweepAngle) > Math.PI ? 1 : 0;
            int fS = sweepAngle < 0 ? 1 : 0;

            var e = Append(new Element(ElementNames.PATH));
            var path = new PathBuilder();
            path.MoveTo(x1, y1);
            path.ArcTo(rx, ry, phi, fA, fS, x2, y2);
            e.Set("d", path.ToString());
            e.Apply(pen, null);
        }

        public void DrawPath(AbstractPen? pen, AbstractBrush? brush, AbstractPath path)
        {
            var e = Append(new Element(ElementNames.PATH));
            e.Set("d", ToSVG(path));
            e.Apply(pen, brush);
        }

        public void DrawCurve(AbstractPen pen, PointF[] points, float tension)
        {
            var e = Append(new Element(ElementNames.PATH));
            e.Set("d", ToSVG(points, tension, false));
            e.Apply(pen, null);
        }

        public void DrawClosedCurve(AbstractPen? pen, AbstractBrush? brush, PointF[] points, float tension)
        {
            var e = Append(new Element(ElementNames.PATH));
            e.Set("d", ToSVG(points, tension, true));
            e.Apply(pen, brush);
        }

        public void DrawRectangle(AbstractPen? pen, AbstractBrush? brush, float x, float y, float width, float height)
        {
            var e = Append(new Element(ElementNames.RECT));
            e.Set("x", x);
            e.Set("y", y);
            e.Set("width", width);
            e.Set("height", height);
            e.Apply(pen, brush);
        }

        public void DrawEllipse(AbstractPen? pen, AbstractBrush? brush, float x, float y, float width, float height)
        {
            Element e;
            if (width == height)
            {
                e = Append(new Element(ElementNames.CIRCLE));
                e.Set("r", width / 2);
            }
            else
            {
                e = Append(new Element(ElementNames.ELLIPSE));
                e.Set("rx", width / 2);
                e.Set("ry", height / 2);
            }
            e.Set("cx", x + width / 2);
            e.Set("cy", y + height / 2);
            e.Apply(pen, brush);
        }
        #endregion

        #region Images
        private string UseImage(AbstractImage image)
        {
            if (images.ContainsKey(image))
                return images[image];
            var e = AddDefinition(new Element(ElementNames.IMAGE));
            e.Set("xlink:href", image.DataUrl);
            e.Set("width", 1);
            e.Set("height", 1);
            e.Set("preserveAspectRatio", "none");
            images[image] = e.Id;
            return e.Id;
        }

        public void DrawImage(AbstractImage image, float x, float y, float width, float height)
        {
            var e = Append(new Element(ElementNames.USE));
            e.Set("transform", $"translate({F(x)} {F(y)}) scale({F(width)} {F(height)})");
            e.Set("xlink:href", "#" + UseImage(image));
        }

        public void DrawImageAlpha(float alpha, AbstractImage image, RectangleF targetRect)
        {
            var e = Append(new Element(ElementNames.USE));
            e.Set("transform", $"translate({F(targetRect.X)} {F(targetRect.Y)}) scale({F(targetRect.Width)} {F(targetRect.Height)})");
            e.Set("opacity", alpha);
            e.Set("xlink:href", "#" + UseImage(image));
        }
        #endregion

        #region Clipping
        public void IntersectClip(RectangleF rect)
        {
            var clipPath = AddDefinition(new Element(ElementNames.CLIPPATH));
            var r = clipPath.Append(new Element(ElementNames.RECT));
            r.Set("x", rect.X);
            r.Set("y", rect.Y);
            r.Set("width", rect.Width);
            r.Set("height", rect.Height);

            var e = Open(new Element(ElementNames.G));
            e.Set("clip-path", $"url(#{clipPath.Id})");
        }

        public void IntersectClip(AbstractPath path)
        {
            var clipPath = AddDefinition(new Element(ElementNames.CLIPPATH));
            var p = clipPath.Append(new Element(ElementNames.PATH));
            p.Set("d", ToSVG(path));

            var e = Open(new Element(ElementNames.G));
            e.Set("clip-path", $"url(#{clipPath.Id})");
        }
        #endregion

        #region Text
        private System.Drawing.Graphics? scratch;
        public SizeF MeasureString(string text, AbstractFont font)
        {
            scratch ??= System.Drawing.Graphics.FromImage(new Bitmap(1, 1));
            return scratch.MeasureString(text, font.Font);
        }

        public void DrawString(string s, AbstractFont font, AbstractBrush brush, float x, float y, StringAlignment alignment)
        {
            var e = Append(new Element(ElementNames.TEXT));
            e.content = s;

            e.Set("font-family", font.Families);
            e.Set("font-size", font.Size);
            if (font.Italic)
                e.Set("font-style", "italic");
            if (font.Bold)
                e.Set("font-weight", "bold");
            if (font.Underline)
                e.Set("text-decoration", "underline");
            else if (font.Strikeout)
                e.Set("text-decoration", "line-through");

            switch (alignment)
            {
                case StringAlignment.Centered: y += (font.Size * 0.85f) / 2; e.Set("text-anchor", "middle"); break;
                case StringAlignment.TopLeft: y += font.Size * 0.85f; break;
                case StringAlignment.TopCenter: y += font.Size * 0.85f; e.Set("text-anchor", "middle"); break;
                case StringAlignment.TopRight: y += font.Size * 0.85f; e.Set("text-anchor", "end"); break;
                case StringAlignment.CenterLeft: y += (font.Size * 0.85f) / 2; break;
                case StringAlignment.Baseline: break;
                default: throw new ApplicationException("Unhandled string alignment");
            }

            e.Set("x", x);
            e.Set("y", y);
            e.Apply(brush);
        }
        #endregion

        #region Transforms
        public void ScaleTransform(float scaleX, float scaleY)
        {
            if (scaleX == 1 && scaleY == 1)
                return;
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"scale({F(scaleX)} {F(scaleY)})");
        }
        public void TranslateTransform(float dx, float dy)
        {
            if (dx == 0 && dy == 0)
                return;
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"translate({F(dx)},{F(dy)})");
        }
        public void RotateTransform(float angle)
        {
            if (angle == 0)
                return;
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"rotate({F(angle)})");
        }
        public void MultiplyTransform(AbstractMatrix m)
        {
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"matrix({F(m.M11)},{F(m.M12)},{F(m.M21)},{F(m.M22)},{F(m.OffsetX)},{F(m.OffsetY)})");
        }
        #endregion

        #region State
        public AbstractGraphicsState Save()
        {
            var state = new State(this, new Element(ElementNames.G));
            Open(state.element);
            return state;
        }

        public void Restore(AbstractGraphicsState state)
        {
            while (stack.Peek() != ((State)state).element)
                stack.Pop();
            stack.Pop();
        }

        private class State : AbstractGraphicsState
        {
            public Element element;
            public State(AbstractGraphics g, Element e) : base(g) { element = e; }
        }
        #endregion

        #region Relay Methods
        public void DrawLine(AbstractPen pen, PointF pt1, PointF pt2)
        {
            DrawLine(pen, pt1.X, pt1.Y, pt2.X, pt2.Y);
        }
        public void DrawPath(AbstractBrush brush, AbstractPath path)
        {
            DrawPath(null, brush, path);
        }
        public void DrawPath(AbstractPen pen, AbstractPath path)
        {
            DrawPath(pen, null, path);
        }
        public void DrawRectangle(AbstractBrush brush, RectangleF rect)
        {
            DrawRectangle(null, brush, rect.X, rect.Y, rect.Width, rect.Height);
        }
        public void DrawRectangle(AbstractBrush brush, float x, float y, float width, float height)
        {
            DrawRectangle(null, brush, x, y, width, height);
        }
        public void DrawRectangle(AbstractPen pen, float x, float y, float width, float height)
        {
            DrawRectangle(pen, null, x, y, width, height);
        }
        public void DrawRectangle(AbstractPen pen, RectangleF rect)
        {
            DrawRectangle(pen, null, rect.X, rect.Y, rect.Width, rect.Height);
        }
        public void DrawEllipse(AbstractBrush brush, float x, float y, float width, float height)
        {
            DrawEllipse(null, brush, x, y, width, height);
        }
        public void DrawEllipse(AbstractPen pen, float x, float y, float width, float height)
        {
            DrawEllipse(pen, null, x, y, width, height);
        }
        public void DrawClosedCurve(AbstractBrush brush, PointF[] points, float tension)
        {
            DrawClosedCurve(null, brush, points, tension);
        }
        public void DrawClosedCurve(AbstractPen pen, PointF[] points, float tension)
        {
            DrawClosedCurve(pen, null, points, tension);
        }

        public void ScaleTransform(float scaleXY)
        {
            ScaleTransform(scaleXY, scaleXY);
        }
        #endregion

        #region Utilities
        private static string ToSVG(AbstractPath ap)
        {
            PathBuilder path = new PathBuilder();

            for (int i = 0; i < ap.Points.Length; ++i)
            {
                byte type = ap.Types[i];
                PointF point = ap.Points[i];
                switch (type & 0x7)
                {
                    case 0: path.MoveTo(point.X, point.Y); break;
                    case 1: path.LineTo(point.X, point.Y); break;
                    case 3: throw new ApplicationException("Unsupported path point type: " + type);
                }

                if ((type & 0x20) != 0)
                    throw new ApplicationException("Unsupported path flag type: " + type);

                if ((type & 0x80) != 0)
                    path.Close();
            }

            return path.ToString();
        }

        private static string ToSVG(PointF[] points, float tension, bool closed)
        {
            PathBuilder path = new PathBuilder();

            float a = tension + 1;
            PointF last = PointF.Empty;
            PointF lastd = PointF.Empty;

            PointF deriv(int i)
            {
                if (closed)
                {
                    int j = (i + 1) % points.Length;
                    int k = (i > 0) ? i - 1 : points.Length - 1;
                    return new PointF((points[j].X - points[k].X) / a, (points[j].Y - points[k].Y) / a);
                }

                if (i == 0)
                    return new PointF((points[1].X - points[0].X) / a, (points[1].Y - points[0].Y) / a);
                else if (i == points.Length - 1)
                    return new PointF((points[i].X - points[i - 1].X) / a, (points[i].Y - points[i - 1].Y) / a);
                else
                    return new PointF((points[i + 1].X - points[i - 1].X) / a, (points[i + 1].Y - points[i - 1].Y) / a);
            }

            for (int i = 0; i < points.Length; ++i)
            {
                PointF point = points[i];
                PointF pointd = deriv(i);

                if (i == 0)
                {
                    path.MoveTo(point.X, point.Y);
                }
                else
                {
                    path.CurveTo(
                        last.X + lastd.X / 3, last.Y + lastd.Y / 3,
                        point.X - pointd.X / 3, point.Y - pointd.Y / 3,
                        point.X, point.Y);
                }

                last = point;
                lastd = pointd;
            }

            if (closed)
            {
                PointF point = points[0];
                PointF pointd = deriv(0);

                path.CurveTo(
                    last.X + lastd.X / 3, last.Y + lastd.Y / 3,
                    point.X - pointd.X / 3, point.Y - pointd.Y / 3,
                    point.X, point.Y);
                path.Close();
            }

            return path.ToString();
        }
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
                scratch?.Dispose();
                scratch = null;
            }
            disposed = true;
        }
        #endregion
    }
}
