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
        private const string NumberFormat = "G5";

        private class Element
        {
            public string name;
            public string content;
            public Dictionary<string, string> attributes = new Dictionary<string, string>();
            public List<Element> children = new List<Element>();

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

            public bool Has(string name) { return attributes.ContainsKey(name); }
            public string Get(string name) { return attributes[name]; }
            public void Set(string name, string value) { attributes[name] = value; }
            public void Set(string name, float value) { attributes[name] = value.ToString(NumberFormat, CultureInfo.InvariantCulture); }
            public void Set(string name, Color color) {
                if (color.IsEmpty || color.A == 0)
                    return; // Inherits "None" from root
                else if (color.A < 255)
                    attributes[name] = $"rgba({color.R},{color.G},{color.B},{color.A / 255f:G5})";
                else
                    attributes[name] = $"rgb({color.R},{color.G},{color.B})";
            }

            public void Apply(AbstractPen pen)
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
                                string.Join(" ", pen.CustomDashPattern.Select(w => (w * pen.Width).ToString(NumberFormat))));
                            break;
                    }
                }
            }
            public void Apply(AbstractBrush brush)
            {
                Set("fill", brush?.Color ?? Color.Empty);
            }
            public void Apply(AbstractPen pen, AbstractBrush brush)
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

            public override string ToString() { return b.ToString().Trim(); }

            public PathBuilder() { }

            public void MoveTo(float x, float y)
            {
                if (!used)
                {
                    b.Append($"M{x:G5},{y:G5}");
                    used = true;
                }
                else
                {
                    b.Append($"m{x - lastX:G5},{y - lastY:G5}");
                }
                lastX = x;
                lastY = y;
            }
            public void LineTo(float x, float y)
            {
                if (!used)
                {
                    b.Append($"L{x:G5},{y:G5}");
                    used = true;
                }
                else if (x == lastX)
                {
                    b.Append($"v{y - lastY:G5}");
                }
                else if (y == lastY)
                {
                    b.Append($"h{x - lastX:G5}");
                }
                else
                {
                    b.Append($"l{x - lastX:G5},{y - lastY:G5}");
                }
                lastX = x;
                lastY = y;
            }
            public void ArcTo(float rx, float ry, float phi, int arcFlag, int sweepFlag, float x, float y)
            {
                if (!used)
                {
                    b.Append($"A{rx:G5},{ry:G5},{phi:G5},{arcFlag},{sweepFlag},{x:G5},{y:G5}");
                    used = true;
                }
                else
                {
                    b.Append($"a{rx:G5},{ry:G5},{phi:G5},{arcFlag},{sweepFlag},{x - lastX:G5},{y - lastY:G5}");
                }
                lastX = x;
                lastY = y;
            }
            public void CurveTo(float x1, float y1, float x2, float y2, float x, float y)
            {
                if (!used)
                {
                    b.Append($"C,{x1:G5},{y1:G5},{x2:G5},{y2:G5},{x:G5},{y:G5}");
                    used = true;
                }
                else
                {
                    b.Append($"c{x1 - lastX:G5},{y1 - lastY:G5},{x2 - lastX:G5},{y2 - lastY:G5},{x - lastX:G5},{y - lastY:G5}");
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
            foreach (var child in e.children)
                Optimize(child);

            // Remove <g>s with no children
            e.children.RemoveAll(child => child.name == ElementNames.G && child.children.Count == 0);

            // Flatten <g> with no properties
            List<Element> c = new List<Element>();
            foreach (var child in e.children)
            {
                if (child.name == ElementNames.G && child.attributes.Count == 0)
                    c.AddRange(child.children);
                else
                    c.Add(child);
            }
            e.children = c;
            
            // If a <g> has only a single child, merge
            if (e.name == ElementNames.G && e.children.Count == 1)
            {
                var child = e.children.First();
                // TODO: Other exclusive elements?
                if (!(e.Has("clip-path") && child.Has("clip-path")))
                {
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
                $"width=\"{width}\" height=\"{height}\">");
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

        private int def_id = 0;
        private Element AddDefinition(Element element)
        {
            element.Set("id", "did" + (++def_id).ToString(CultureInfo.InvariantCulture));
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
        private Element Append(Element element)
        {
            return Current.Append(element);
        }

        public SVGGraphics(float width, float height)
        {
            this.width = width;
            this.height = height;

            root.Set("fill", "None");
            root.Set("stroke", "None");
            stack.Push(root);
        }

        System.Drawing.Graphics AbstractGraphics.Graphics => null;
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

        public void DrawPath(AbstractPen pen, AbstractBrush brush, AbstractPath path)
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

        public void DrawClosedCurve(AbstractPen pen, AbstractBrush brush, PointF[] points, float tension)
        {
            var e = Append(new Element(ElementNames.PATH));
            e.Set("d", ToSVG(points, tension, true));
            e.Apply(pen, brush);
        }

        public void DrawRectangle(AbstractPen pen, AbstractBrush brush, float x, float y, float width, float height)
        {
            var e = Append(new Element(ElementNames.RECT));
            e.Set("x", x);
            e.Set("y", y);
            e.Set("width", width);
            e.Set("height", height);
            e.Apply(pen, brush);
        }

        public void DrawEllipse(AbstractPen pen, AbstractBrush brush, float x, float y, float width, float height)
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
        public void DrawImage(AbstractImage image, float x, float y, float width, float height)
        {
            var e = Append(new Element(ElementNames.IMAGE));
            e.Set("x", x);
            e.Set("y", y);
            e.Set("width", width);
            e.Set("height", height);
            e.Set("xlink:href", image.Url);
        }

        public void DrawImageAlpha(float alpha, AbstractImage image, RectangleF targetRect)
        {
            var e = Append(new Element(ElementNames.IMAGE));
            e.Set("x", targetRect.X);
            e.Set("y", targetRect.Y);
            e.Set("width", targetRect.Width);
            e.Set("height", targetRect.Height);
            e.Set("opacity", alpha);
            e.Set("xlink:href", image.Url);
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
            e.Set("clip-path", $"url(#{clipPath.Get("id")})");
        }

        public void IntersectClip(AbstractPath path)
        {
            var clipPath = AddDefinition(new Element(ElementNames.CLIPPATH));
            var p = clipPath.Append(new Element(ElementNames.PATH));
            p.Set("d", ToSVG(path));

            var e = Open(new Element(ElementNames.G));
            e.Set("clip-path", $"url(#{clipPath.Get("id")})");
        }
        #endregion

        #region Text
        private System.Drawing.Graphics scratch;
        public SizeF MeasureString(string text, Font font)
        {
            if (scratch == null) scratch = System.Drawing.Graphics.FromImage(new Bitmap(1, 1));
            return scratch.MeasureString(text, font);
        }

        public void DrawString(string s, Font font, AbstractBrush brush, float x, float y, StringAlignment alignment)
        {
            var e = Append(new Element(ElementNames.TEXT));
            e.content = s;

            e.Set("font-family", font.Name);
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
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"scale({scaleX:G5} {scaleY:G5})");
        }
        public void TranslateTransform(float dx, float dy)
        {
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"translate({dx:G5},{dy:G5})");
        }
        public void RotateTransform(float angle)
        {
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"rotate({angle:G5})");
        }
        public void MultiplyTransform(AbstractMatrix m)
        {
            var e = Open(new Element(ElementNames.G));
            e.Set("transform", $"matrix({m.M11:G5},{m.M12:G5},{m.M21:G5},{m.M22:G5},{m.OffsetX:G5},{m.OffsetY:G5})");
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

            Func<int, PointF> deriv = (int i) =>
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
            };
               
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
        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                scratch?.Dispose();
            }
        }
        #endregion
    }
}
