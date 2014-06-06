//#define SHOW_TIMING

using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

namespace Maps.Rendering
{
    /// <summary>
    /// Summary description for Render.
    /// </summary>
    public static class Render
    {
        private class MapLabel
        {
            public MapLabel(string text, float x, float y) { this.text = text; this.position = new PointF(x, y); }
            public readonly string text;
            public readonly PointF position;
        }

        // TODO: Move this to data file
        private static readonly MapLabel[] labels = 
        {
            new MapLabel("Human Client States", -160, -50),
            new MapLabel("Aslan Client States", -60, 155),
            new MapLabel("Aslan Colonies", -115, -5),
            new MapLabel("Mixed Client States", 110, 5),
            new MapLabel("Scattered\nClient States", 85, 65),
            new MapLabel("Vargr Enclaves", 95, -135),
            new MapLabel("Hive Young Worlds", 100, 128)
        };

        private static readonly string[] borderFiles = {
            @"~/res/Vectors/Imperium.xml", 
            @"~/res/Vectors/Aslan.xml", 
            @"~/res/Vectors/Kkree.xml", 
            @"~/res/Vectors/Vargr.xml", 
            @"~/res/Vectors/Zhodani.xml", 
            @"~/res/Vectors/Solomani.xml", 
            @"~/res/Vectors/Hive.xml", 
            @"~/res/Vectors/SpinwardClient.xml", 
            @"~/res/Vectors/RimwardClient.xml", 
            @"~/res/Vectors/TrailingClient.xml"
        };

        private static readonly string[] riftFiles = { 
            @"~/res/Vectors/GreatRift.xml", 
            @"~/res/Vectors/LesserRift.xml", 
            @"~/res/Vectors/WindhornRift.xml", 
            @"~/res/Vectors/DelphiRift.xml", 
            @"~/res/Vectors/ZhdantRift.xml" 
        };

        private static readonly string[] routeFiles = {
            @"~/res/Vectors/J5Route.xml",
            @"~/res/Vectors/J4Route.xml",
            @"~/res/Vectors/CoreRoute.xml" 
        };

        // TODO: Consider not caching these across sessions
        private static XImage s_sillyImageColor;
        private static XImage s_sillyImageGray;

        private static XImage s_backgroundImage;

        // These are loaded as GDI+ Images since we need to derive alpha-variants of them;
        // the results are cached as PDFSharp Images (XImage)
        private static Image s_galaxyImage;
        private static Image s_riftImage;
        private static Dictionary<string, XImage> s_worldImages;

        public class RenderContext
        {
            public ResourceManager resourceManager = null;
            public Selector selector = null;
            public XGraphics graphics = null;
            public RectangleF tileRect;
            public double scale = double.NaN;
            public MapOptions options = 0;
            public Stylesheet styles = null;
            public Size tileSize;
            public XGraphicsPath clipPath = null;
            public bool border = false;
            public bool silly = false;
            public bool clipOutsectorBorders = false;

            public XMatrix ImageSpaceToWorldSpace
            {
                get
                {
                    XMatrix m = new XMatrix();
                    m.TranslatePrepend((float)(-this.tileRect.Left * this.scale * Astrometrics.ParsecScaleX), (float)(-this.tileRect.Top * this.scale * Astrometrics.ParsecScaleY));
                    m.ScalePrepend((float)this.scale * Astrometrics.ParsecScaleX, (float)this.scale * Astrometrics.ParsecScaleY);
                    return m;
                }
            }
        }

        /// <summary>
        /// Performance timer record
        /// </summary>
        private class Timer
        {
            public DateTime dt;
            public string label;
            public Timer(string label)
            {
                this.dt = DateTime.Now;
                this.label = label;
            }
        }

        public static void RenderTile(RenderContext ctx)
        {
            DateTime dtStart = DateTime.Now;
            List<Timer> timers = new List<Timer>();

            if (ctx.resourceManager == null)
                throw new ArgumentNullException("resourceManager");

            if (ctx.graphics == null)
                throw new ArgumentNullException("graphics");

            if (ctx.selector == null)
                throw new ArgumentNullException("selector");

            XSolidBrush solidBrush = new XSolidBrush();
            XPen pen = new XPen(XColor.Empty);

            using (var fonts = new FontCache(ctx.styles))
            {
                #region resources
                lock (ctx.resourceManager.GetType())
                {
                    if (ctx.styles.useBackgroundImage && s_backgroundImage == null)
                        s_backgroundImage = XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Nebula.png"));
 
                    if (ctx.styles.showRifts && s_riftImage == null)
                        s_riftImage = Image.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Rifts.png"));

                    if (ctx.styles.useGalaxyImage && s_galaxyImage == null)
                        s_galaxyImage = Image.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Galaxy.png"));
                    
                    if (ctx.styles.useWorldImages && s_worldImages == null)
                    {
                        s_worldImages = new Dictionary<string, XImage> {
                            { "Hyd0", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd0.png")) },
                            { "Hyd1", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd1.png")) },
                            { "Hyd2", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd2.png")) },
                            { "Hyd3", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd3.png")) },
                            { "Hyd4", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd4.png")) },
                            { "Hyd5", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd5.png")) },
                            { "Hyd6", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd6.png")) },
                            { "Hyd7", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd7.png")) },
                            { "Hyd8", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd8.png")) },
                            { "Hyd9", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Hyd9.png")) },
                            { "HydA", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/HydA.png")) },
                            { "Belt", XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/Candy/Belt.png")) }
                        };
                    }

                    if (ctx.silly && s_sillyImageColor == null)
                    {
                        // Happy face c/o http://bighappyfaces.com/
                        s_sillyImageColor = XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/AprilFools/Starburst.png"));
                        s_sillyImageGray = XImage.FromFile(ctx.resourceManager.Server.MapPath(@"~/res/AprilFools/Starburst_Gray.png"));
                    }
                }
                #endregion

                timers.Add(new Timer("preload"));
                //////////////////////////////////////////////////////////////
                //
                // Image-Space Rendering
                //
                //////////////////////////////////////////////////////////////

                using (Maps.Rendering.RenderUtil.SaveState(ctx.graphics))
                {
                    if (ctx.clipPath != null)
                    {
                        XMatrix m = ctx.ImageSpaceToWorldSpace;
                        ctx.graphics.MultiplyTransform(m);
                        ctx.graphics.IntersectClip(ctx.clipPath);
                        m.Invert();
                        ctx.graphics.MultiplyTransform(m);
                    }

                    // Fill
                    ctx.graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                    solidBrush.Color = ctx.styles.backgroundColor;
                    ctx.graphics.DrawRectangle(solidBrush, 0, 0, ctx.tileSize.Width, ctx.tileSize.Height);


                    //// Draw tile #
                    //using( var font = new Font( FontFamily.GenericSansSerif, 10 ) )
                    //{
                    //  graphics.DrawString( String.Format( "({0},{1})", x, y ), font, foregroundBrush, 0, 0 );
                    //  graphics.DrawString( String.Format( "{0},{1}-{2}x{3}", tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height ), font, foregroundBrush, 0, 20 );
                    //}

                    // Frame it
                    //graphics.DrawRectangle( Pens.Green, 0, 0, tileSize.Width-1, tileSize.Height-1 );
                }

                timers.Add(new Timer("imagespace"));
                //////////////////////////////////////////////////////////////
                //
                // World-Space Rendering
                //
                //////////////////////////////////////////////////////////////

                // Transform from image-space to world-space. Set up a reverse transform as well.
                XMatrix imageSpaceToWorldSpace = ctx.ImageSpaceToWorldSpace;

                XMatrix worldSpaceToImageSpace = imageSpaceToWorldSpace;
                worldSpaceToImageSpace.Invert();

                ctx.graphics.MultiplyTransform(imageSpaceToWorldSpace);

                using (Maps.Rendering.RenderUtil.SaveState(ctx.graphics))
                {

                    //------------------------------------------------------------
                    // Explicit Clipping
                    //------------------------------------------------------------

                    if (ctx.clipPath != null)
                        ctx.graphics.IntersectClip(ctx.clipPath);

                    //ctx.styles.showPseudoRandomStars = true;
                    //------------------------------------------------------------
                    // Backgrounds
                    //------------------------------------------------------------

                    RectangleF galacticBounds = new RectangleF(-14598.67f, -23084.26f, 29234.1133f, 25662.4746f); // TODO: Don't hardcode
                    Rectangle galaxyImageRect = new Rectangle(-18257, -26234, 36551, 32462); // Chosen to match T5 pp.416 

                    // This transforms the Linehan galactic structure to the Mikesh galactic structure
                    // See http://travellermap.blogspot.com/2009/03/galaxy-scale-mismatch.html
                    Matrix xformLinehanToMikesh = new Matrix(0.9181034f, 0.0f, 0.0f, 0.855192542f, 120.672432f, 86.34569f);
                    timers.Add(new Timer("prep"));

                    //------------------------------------------------------------
                    // Local background (Nebula)
                    //------------------------------------------------------------
                    #region local-background

                    // NOTE: Since alpha texture brushes aren't supported without
                    // creating a new image (slow!) we render the local background
                    // first, then overlay the deep background over it, for
                    // basically the same effect since the alphas sum to 1.

                    if (ctx.styles.useBackgroundImage && galacticBounds.IntersectsWith(ctx.tileRect))
                    {
                        // Image-space rendering, so save current context
                        using (RenderUtil.SaveState(ctx.graphics))
                        {
                            // Never fill outside the galaxy
                            ctx.graphics.IntersectClip(galacticBounds);

                            // Map back to image space so it scales/tiles nicely
                            ctx.graphics.MultiplyTransform(worldSpaceToImageSpace);

                            const float backgroundImageScale = 2.0f;

                            lock (s_backgroundImage)
                            {
                                // Scaled size of the background
                                double w = s_backgroundImage.PixelWidth * backgroundImageScale;
                                double h = s_backgroundImage.PixelHeight * backgroundImageScale;

                                // Offset of the background, relative to the canvas
                                double ox = (float)(-ctx.tileRect.Left * ctx.scale * Astrometrics.ParsecScaleX) % w;
                                double oy = (float)(-ctx.tileRect.Top * ctx.scale * Astrometrics.ParsecScaleY) % h;
                                if (ox > 0) ox -= w;
                                if (oy > 0) oy -= h;

                                // Number of copies needed to cover the canvas
                                int nx = 1 + (int)Math.Floor(ctx.tileSize.Width / w);
                                int ny = 1 + (int)Math.Floor(ctx.tileSize.Height / h);
                                if (ox + nx * w < ctx.tileSize.Width) nx += 1;
                                if (oy + ny * h < ctx.tileSize.Height) ny += 1;

                                for (int x = 0; x < nx; ++x)
                                {
                                    for (int y = 0; y < ny; ++y)
                                    {
                                        ctx.graphics.DrawImage(s_backgroundImage, ox + x * w, oy + y * h, w + 1, h + 1);
                                        //ctx.graphics.DrawRectangle( XPens.Orange, ox + x * w, oy + y * h, w, h );
                                    }
                                }
                            }
                        }
                    }
                    #endregion
                    timers.Add(new Timer("nebula"));

                    //------------------------------------------------------------
                    // Deep background (Galaxy)
                    //------------------------------------------------------------
                    #region galaxy-background
                    if (ctx.styles.useGalaxyImage && ctx.styles.deepBackgroundOpacity > 0f)
                    {
                        using (RenderUtil.SaveState(ctx.graphics))
                        {
                            ctx.graphics.MultiplyTransform(xformLinehanToMikesh);
                            lock (s_galaxyImage)
                            {
                                RenderUtil.DrawImageAlpha(ctx.graphics, ctx.styles.deepBackgroundOpacity, s_galaxyImage, galaxyImageRect);
                            }
                        }
                    }
                    #endregion
                    timers.Add(new Timer("galaxy"));

                    //------------------------------------------------------------
                    // Pseudo-Random Stars
                    //------------------------------------------------------------
                    #region pseudorandom-stars

                    if (ctx.styles.pseudoRandomStars.visible)
                    {
                        // Render pseudorandom stars based on the tile # and 
                        // scale factor. Note that these are positioned in
                        // screen space, not world space.

                        //const int nStars = 75;
                        int nMinStars = ctx.tileSize.Width * ctx.tileSize.Height / 300;
                        int nStars = ctx.scale >= 1 ? nMinStars : (int)(nMinStars / ctx.scale);

                        // NOTE: For performance's sake, three different cases are considered:
                        // (1) Tile is entirely within charted space (most common) - just render
                        //     the pseudorandom stars into the tile
                        // (2) Tile intersects the galaxy bounds - render pseudorandom stars
                        //     into a texture, then fill the galaxy vector with it
                        // (3) Tile is entire outside the galaxy - don't render stars

                        using (RenderUtil.SaveState(ctx.graphics))
                        {
                            ctx.graphics.SmoothingMode = XSmoothingMode.HighQuality;
                            solidBrush.Color = ctx.styles.pseudoRandomStars.fillColor;

                            Random rand = new Random((((int)ctx.tileRect.Left) << 8) ^ (int)ctx.tileRect.Top);
                            for (int i = 0; i < nStars; i++)
                            {
                                float starX = (float)rand.NextDouble() * ctx.tileRect.Width + ctx.tileRect.X;
                                float starY = (float)rand.NextDouble() * ctx.tileRect.Height + ctx.tileRect.Y;
                                float d = (float)rand.NextDouble() * 2;

                                //ctx.graphics.DrawRectangle( fonts.foregroundBrush, starX, starY, (float)( d / ctx.scale * Astrometrics.ParsecScaleX ), (float)( d / ctx.scale * Astrometrics.ParsecScaleY ) );
                                ctx.graphics.DrawEllipse(solidBrush, starX, starY, (float)(d / ctx.scale * Astrometrics.ParsecScaleX), (float)(d / ctx.scale * Astrometrics.ParsecScaleY));
                            }
                        }
                    }
                    #endregion
                    timers.Add(new Timer("pseudorandom"));

                    //------------------------------------------------------------
                    // Rifts in Charted Space
                    //------------------------------------------------------------
                    #region riftFiles

                    if (ctx.styles.showRifts && ctx.styles.riftOpacity > 0f)
                    {
                        Rectangle riftImageRect;
                        riftImageRect = new Rectangle(-1374, -827, 2769, 1754); // Correct
                        lock (s_riftImage)
                        {
                            RenderUtil.DrawImageAlpha(ctx.graphics, ctx.styles.riftOpacity, s_riftImage, riftImageRect);
                        }
                    }
                    #endregion
                    timers.Add(new Timer("riftFiles"));

                    //------------------------------------------------------------
                    // April Fool's Day
                    //------------------------------------------------------------
                    #region april-fools

                    if (ctx.silly)
                    {
                        using (RenderUtil.SaveState(ctx.graphics))
                        {
                            // Render in image-space
                            ctx.graphics.MultiplyTransform(worldSpaceToImageSpace);

                            XImage sillyImage = ctx.styles.grayscale ? s_sillyImageGray : s_sillyImageColor;

                            lock (sillyImage)
                            {
                                ctx.graphics.DrawImage(sillyImage, 0, 0, ctx.tileSize.Width, ctx.tileSize.Height);
                            }
                        }
                        timers.Add(new Timer("silly"));
                    }

                    #endregion

                    //------------------------------------------------------------
                    // Macro: Borders object
                    //------------------------------------------------------------
                    #region macro-borders
                    if (ctx.styles.macroBorders.visible)
                    {
                        ctx.styles.macroBorders.pen.Apply(ref pen);
                        ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                        foreach (var vec in borderFiles
                            .Select(file => ctx.resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & ctx.options & MapOptions.BordersMask) != 0))
                        {
                            vec.Draw(ctx.graphics, ctx.tileRect, ctx.options, pen);
                        }

                    }
                    #endregion
                    timers.Add(new Timer("macroborder"));

                    //------------------------------------------------------------
                    // Macro: Route object
                    //------------------------------------------------------------
                    #region macro-routes

                    if (ctx.styles.macroRoutes.visible)
                    {
                        ctx.styles.macroRoutes.pen.Apply(ref pen);
                        ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                        foreach (var vec in routeFiles
                            .Select(file => ctx.resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & ctx.options & MapOptions.BordersMask) != 0))
                        {
                            vec.Draw(ctx.graphics, ctx.tileRect, ctx.options, pen);
                        }
                    }
                    #endregion
                    timers.Add(new Timer("macroroute"));

                    //------------------------------------------------------------
                    // Sector Grid
                    //------------------------------------------------------------
                    #region sector-grid

                    ctx.graphics.SmoothingMode = XSmoothingMode.HighSpeed;

                    if (ctx.styles.sectorGrid.visible)
                    {
                        const int gridSlop = 10;
                        ctx.styles.sectorGrid.pen.Apply(ref pen);

                        for (float h = ((float)(Math.Floor((ctx.tileRect.Left) / Astrometrics.SectorWidth) - 1) - Astrometrics.ReferenceSector.X) * Astrometrics.SectorWidth - Astrometrics.ReferenceHex.X; h <= ctx.tileRect.Right + Astrometrics.SectorWidth; h += Astrometrics.SectorWidth)
                            ctx.graphics.DrawLine(pen, h, ctx.tileRect.Top - gridSlop, h, ctx.tileRect.Bottom + gridSlop);
                        
                        for (float v = ((float)(Math.Floor((ctx.tileRect.Top) / Astrometrics.SectorHeight) - 1) - Astrometrics.ReferenceSector.Y) * Astrometrics.SectorHeight - Astrometrics.ReferenceHex.Y; v <= ctx.tileRect.Bottom + Astrometrics.SectorHeight; v += Astrometrics.SectorHeight)
                            ctx.graphics.DrawLine(pen, ctx.tileRect.Left - gridSlop, v, ctx.tileRect.Right + gridSlop, v);
                    }

                    #endregion
                    timers.Add(new Timer("sector grid"));

                    //------------------------------------------------------------
                    // Subsector Grid
                    //------------------------------------------------------------
                    #region subsector-grid
                    ctx.graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                    if (ctx.styles.subsectorGrid.visible)
                    {
                        const int gridSlop = 10;
                        ctx.styles.subsectorGrid.pen.Apply(ref pen);

                        int hmin = (int)Math.Floor(ctx.tileRect.Left / Astrometrics.SubsectorWidth) - 1 - Astrometrics.ReferenceSector.X,
                            hmax = (int)Math.Ceiling((ctx.tileRect.Right + Astrometrics.SubsectorWidth + Astrometrics.ReferenceHex.X) / Astrometrics.SubsectorWidth);
                        for (int hi = hmin; hi <= hmax; ++hi)
                        {
                            if (hi % 4 == 0) continue;
                            float h = hi * Astrometrics.SubsectorWidth - Astrometrics.ReferenceHex.X;
                            ctx.graphics.DrawLine(pen, h, ctx.tileRect.Top - gridSlop, h, ctx.tileRect.Bottom + gridSlop);
                        }

                        int vmin = (int)Math.Floor(ctx.tileRect.Top / Astrometrics.SubsectorHeight) - 1 - Astrometrics.ReferenceSector.Y,
                            vmax = (int)Math.Ceiling((ctx.tileRect.Bottom + Astrometrics.SubsectorHeight + Astrometrics.ReferenceHex.Y) / Astrometrics.SubsectorHeight);
                        for (int vi = vmin; vi <= vmax; ++vi)
                        {
                            if (vi % 4 == 0) continue;
                            float v = vi * Astrometrics.SubsectorHeight - Astrometrics.ReferenceHex.Y;
                            ctx.graphics.DrawLine(pen, ctx.tileRect.Left - gridSlop, v, ctx.tileRect.Right + gridSlop, v);
                        }
                    }
                    #endregion
                    timers.Add(new Timer("subsector grid"));

                    //------------------------------------------------------------
                    // Parsec Grid
                    //------------------------------------------------------------
                    #region parsec-grid
                    // TODO: Optimize - timers indicate this is slow
                    ctx.graphics.SmoothingMode = XSmoothingMode.HighQuality;
                    if (ctx.styles.parsecGrid.visible)
                    {
                        const int parsecSlop = 1;

                        int hx = (int)Math.Floor(ctx.tileRect.Left);
                        int hw = (int)Math.Ceiling(ctx.tileRect.Width);
                        int hy = (int)Math.Floor(ctx.tileRect.Top);
                        int hh = (int)Math.Ceiling(ctx.tileRect.Height);

                        ctx.styles.parsecGrid.pen.Apply(ref pen);

                        switch (ctx.styles.microBorderStyle)
                        {
                            case MicroBorderStyle.Square:
                                for (int px = hx - parsecSlop; px < hx + hw + parsecSlop; px++)
                                {
                                    float yOffset = ((px % 2) != 0) ? 0.0f : 0.5f;
                                    for (int py = hy - parsecSlop; py < hy + hh + parsecSlop; py++)
                                    {
                                        // TODO: use RenderUtil.(Square|Hex)Edges(X|Y) arrays
                                        const float inset = 0.1f;
                                        ctx.graphics.DrawRectangle(pen, px + inset, py + inset + yOffset, 1 - inset * 2, 1 - inset * 2);
                                    }
                                }
                                break;

                            case MicroBorderStyle.Hex:
                                // TODO: use RenderUtil.(Square|Hex)Edges(X|Y) arrays
                                const double hexEdge = 0.18f; // TODO: Need to compute this (should be cos(60), inverse-scaled)
                                XPoint[] points = new XPoint[4];
                                for (int px = hx - parsecSlop; px < hx + hw + parsecSlop; px++)
                                {
                                    double yOffset = ((px % 2) != 0) ? 0.0 : 0.5;
                                    for (int py = hy - parsecSlop; py < hy + hh + parsecSlop; py++)
                                    {
                                        points[0] = new XPoint(px + -hexEdge, py + 0.5 + yOffset);
                                        points[1] = new XPoint(px + hexEdge, py + 1.0 + yOffset);
                                        points[2] = new XPoint(px + 1.0 - hexEdge, py + 1.0 + yOffset);
                                        points[3] = new XPoint(px + 1.0 + hexEdge, py + 0.5 + yOffset);
                                        ctx.graphics.DrawLines(pen, points);
                                    }
                                }
                                break;
                            case MicroBorderStyle.Curve:
                                // none
                                break;
                        }
                    }
                    #endregion
                    timers.Add(new Timer("parsec grids"));

                    //------------------------------------------------------------
                    // Subsector Names
                    //------------------------------------------------------------
                    #region subsector-names

                    if (ctx.styles.subsectorNames.visible)
                    {
                        solidBrush.Color = ctx.styles.subsectorNames.textColor;
                        foreach (Sector sector in ctx.selector.Sectors)
                        {
                            for (int i = 0; i < 16; i++)
                            {
                                int ssx = i % 4;
                                int ssy = i / 4;

                                Subsector ss = sector[i];
                                if (ss == null || String.IsNullOrEmpty(ss.Name))
                                    continue;

                                Point center = sector.SubsectorCenter(i);
                                RenderUtil.DrawLabel(ctx.graphics, ss.Name, center, ctx.styles.subsectorNames.Font, solidBrush, ctx.styles.subsectorNames.textStyle);
                            }
                        }
                    }

                    #endregion

                    //------------------------------------------------------------
                    // Micro: Borders
                    //------------------------------------------------------------
                    #region micro-borders
                    if (ctx.styles.microBorders.visible)
                    {
                        if (ctx.styles.fillMicroBorders)
                            DrawMicroBorders(ctx, fonts, BorderLayer.Fill);
                        DrawMicroBorders(ctx, fonts, BorderLayer.Stroke);
                    }
                    #endregion
                    timers.Add(new Timer("microborders"));

                    //------------------------------------------------------------
                    // Micro: Routes
                    //------------------------------------------------------------
                    #region routes

                    if (ctx.styles.microRoutes.visible)
                        DrawRoutes(ctx, fonts);
                    
                    #endregion
                    timers.Add(new Timer("routes"));

                    //------------------------------------------------------------
                    // Sector Names
                    //------------------------------------------------------------
                    #region sector-names

                    if (ctx.styles.showSomeSectorNames || ctx.styles.showAllSectorNames)
                    {
                        foreach (Sector sector in ctx.selector.Sectors
                            .Where(sector => ctx.styles.showAllSectorNames || (ctx.styles.showSomeSectorNames && sector.Selected))
                            .Where(sector => sector.Names.Any()))
                        {
                            solidBrush.Color = ctx.styles.sectorName.textColor;
                            string name = sector.Names[0].Text;

                            RenderUtil.DrawLabel(ctx.graphics, name, sector.Center, ctx.styles.sectorName.Font, solidBrush, ctx.styles.sectorName.textStyle);
                        }
                    }

                    #endregion
                    timers.Add(new Timer("sector names"));

                    //------------------------------------------------------------
                    // Macro: Government / Rift / Route Names
                    //------------------------------------------------------------
                    #region government-rift-names
                    if (ctx.styles.macroNames.visible)
                    {
                        foreach (var vec in borderFiles
                            .Select(file => ctx.resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & ctx.options & MapOptions.NamesMask) != 0))
                        {
                            bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                            LabelStyle labelStyle = new LabelStyle();
                            labelStyle.Uppercase = major;
                            XFont font = major ? ctx.styles.macroNames.Font : ctx.styles.macroNames.SmallFont;
                            solidBrush.Color = major ? ctx.styles.macroNames.textColor : ctx.styles.macroNames.textHighlightColor;
                            vec.DrawName(ctx.graphics, ctx.tileRect, ctx.options, font, solidBrush, labelStyle);
                        }

                        foreach (var vec in riftFiles
                            .Select(file => ctx.resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & ctx.options & MapOptions.NamesMask) != 0))
                        {
                            bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                            LabelStyle labelStyle = new LabelStyle();
                            labelStyle.Rotation = 35;
                            labelStyle.Uppercase = major;
                            XFont font = major ? ctx.styles.macroNames.Font : ctx.styles.macroNames.SmallFont;
                            solidBrush.Color = major ? ctx.styles.macroNames.textColor : ctx.styles.macroNames.textHighlightColor;
                            vec.DrawName(ctx.graphics, ctx.tileRect, ctx.options, font, solidBrush, labelStyle);
                        }

                        if (ctx.styles.macroRoutes.visible)
                        {
                            foreach (var vec in routeFiles
                                .Select(file => ctx.resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                                .OfType<VectorObject>()
                                .Where(vec => (vec.MapOptions & ctx.options & MapOptions.NamesMask) != 0))
                            {
                                bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                                LabelStyle labelStyle = new LabelStyle();
                                labelStyle.Uppercase = major;
                                XFont font = major ? ctx.styles.macroNames.Font : ctx.styles.macroNames.SmallFont;
                                solidBrush.Color = major ? ctx.styles.macroRoutes.textColor : ctx.styles.macroRoutes.textHighlightColor;
                                vec.DrawName(ctx.graphics, ctx.tileRect, ctx.options, font, solidBrush, labelStyle);
                            }
                        }

                        if (ctx.options.HasFlag(MapOptions.NamesMinor))
                        {
                            XFont font = ctx.styles.macroNames.MediumFont;
                            solidBrush.Color = ctx.styles.macroRoutes.textHighlightColor;
                            foreach (var label in labels)
                            {
                                using (RenderUtil.SaveState(ctx.graphics))
                                {
                                    XMatrix matrix = new XMatrix();
                                    matrix.ScalePrepend(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                                    matrix.TranslatePrepend(label.position.X, label.position.Y);
                                    ctx.graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                                    XSize size = ctx.graphics.MeasureString(label.text, font);
                                    ctx.graphics.TranslateTransform(-size.Width / 2, -size.Height / 2); // Center the text
                                    RectangleF textBounds = new RectangleF(0, 0, (float)size.Width, (float)size.Height * 2); // *2 or it gets cut off at high sizes
                                    XTextFormatter formatter = new XTextFormatter(ctx.graphics);
                                    formatter.Alignment = XParagraphAlignment.Center;
                                    formatter.DrawString(label.text, font, solidBrush, textBounds);
                                }
                            }

                        }
                    }

                    #endregion
                    timers.Add(new Timer("macro names"));

                    //------------------------------------------------------------
                    // Macro: Capitals & Home Worlds
                    //------------------------------------------------------------
                    #region capitals-homeworlds

                    if (ctx.styles.capitals.visible && (ctx.options & MapOptions.WorldsMask) != 0)
                    {
                        WorldObjectCollection worlds = ctx.resourceManager.GetXmlFileObject(@"~/res/Worlds.xml", typeof(WorldObjectCollection)) as WorldObjectCollection;
                        if (worlds != null)
                        {
                            solidBrush.Color = ctx.styles.capitals.textColor;
                            foreach (WorldObject world in worlds.Worlds.Where(world => (world.MapOptions & ctx.options) != 0))
                            {
                                world.Paint(ctx.graphics, ctx.tileRect, ctx.options, ctx.styles.capitals.fillColor,
                                    solidBrush, ctx.styles.macroNames.SmallFont);
                            }
                        }
                    }

                    #endregion
                    timers.Add(new Timer("macro worlds"));

                    //------------------------------------------------------------
                    // Micro: Government Names
                    //------------------------------------------------------------
                    #region government-names

                    if (ctx.styles.showMicroNames)
                        DrawLabels(ctx, fonts);
                    
                    #endregion
                    timers.Add(new Timer("microborder labels"));
                }

                // End of clipping, so world names are not clipped in jumpmaps.

                //------------------------------------------------------------
                // Worlds
                //------------------------------------------------------------
                #region worlds
                if (ctx.styles.worlds.visible)
                {
                    // TODO: selector may be expensive
                    foreach (World world in ctx.selector.Worlds) { DrawWorld(ctx, fonts, world, WorldLayer.Background); }
                    foreach (World world in ctx.selector.Worlds) { DrawWorld(ctx, fonts, world, WorldLayer.Foreground); }
                }
                #endregion
                timers.Add(new Timer("worlds"));


                //------------------------------------------------------------
                // Unofficial
                //------------------------------------------------------------
                #region unofficial

                if (ctx.styles.dimUnofficialSectors)
                {
                    solidBrush.Color = Color.FromArgb(128, ctx.styles.backgroundColor);
                    foreach (Sector sector in ctx.selector.Sectors.Where(sector => !sector.Tags.Contains("Official")))
                        ctx.graphics.DrawRectangle(solidBrush, sector.Bounds);
                }

                #endregion

#if SHOW_TIMING
                using( RenderUtil.SaveState( ctx.graphics ) )
                {
                    XFont font = new XFont( FontFamily.GenericSansSerif, 12, XFontStyle.Regular, new XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode) );
                    ctx.graphics.MultiplyTransform( worldSpaceToImageSpace );
                    double cursorX = 20.0, cursorY = 20.0;
                    DateTime last = dtStart;
                    foreach( Timer s in timers )
                    {
                        TimeSpan ts = s.dt - last;
                        last = s.dt;
                        for( int dx = -1; dx <= 1; ++dx )
                        {
                            for( int dy = -1; dy <= 1; ++dy )
                            {

                                ctx.graphics.DrawString( String.Format( "{0} {1}", Math.Round( ts.TotalMilliseconds ), s.label ), font, XBrushes.Black, cursorX + dx, cursorY + dy );
                            }
                        }
                        ctx.graphics.DrawString( String.Format("{0} {1}", Math.Round(ts.TotalMilliseconds), s.label), font, XBrushes.Yellow, cursorX, cursorY );
                        cursorY += 14;
                    }
                }
#endif

            }
        }

        private enum WorldLayer { Background, Foreground };
        private static void DrawWorld(RenderContext ctx, FontCache styleRes, World world, WorldLayer layer)
        {
            bool isPlaceholder = world.IsPlaceholder;
            bool isCapital = world.IsCapital;
            bool isHiPop = world.IsHi;
            bool renderName = ctx.styles.worldDetails.HasFlag(WorldDetails.AllNames) ||
                (ctx.styles.worldDetails.HasFlag(WorldDetails.KeyNames) && (isCapital || isHiPop));
            bool renderUWP = ctx.styles.worldDetails.HasFlag(WorldDetails.Uwp);

            using (RenderUtil.SaveState(ctx.graphics))
            {
                XPen pen = new XPen(XColor.Empty);
                XSolidBrush solidBrush = new XSolidBrush();

                ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                // Center on the parsec
                PointF center = Astrometrics.HexToCenter(world.Coordinates);

                XMatrix matrix = new XMatrix();
                matrix.TranslatePrepend(center.X, center.Y);
                matrix.ScalePrepend(ctx.styles.hexContentScale / Astrometrics.ParsecScaleX, ctx.styles.hexContentScale / Astrometrics.ParsecScaleY);
                ctx.graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                if (!ctx.styles.useWorldImages)
                {
                    if (layer == WorldLayer.Background)
                    {
                        #region Zone
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Zone))
                        {
                            if (world.IsAmber || world.IsRed || world.IsBlue)
                            {
                                PenInfo pi =
                                    world.IsAmber ? ctx.styles.amberZone.pen :
                                    world.IsRed ? ctx.styles.redZone.pen : ctx.styles.blueZone.pen;
                                pi.Apply(ref pen);

                                if (renderName && ctx.styles.fillMicroBorders)
                                {
                                    using (RenderUtil.SaveState(ctx.graphics))
                                    {
                                        ctx.graphics.IntersectClip(new RectangleF(-.5f, -.5f, 1f, renderUWP ? 0.65f : 0.75f));
                                        ctx.graphics.DrawEllipse(pen, -0.4f, -0.4f, 0.8f, 0.8f);
                                    }
                                }
                                else
                                {
                                    ctx.graphics.DrawEllipse(pen, -0.4f, -0.4f, 0.8f, 0.8f);
                                }
                            }
                        }
                        #endregion

                        #region Hex
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Hex))
                        {
                            string hex;
                            switch (ctx.styles.hexCoordinateStyle)
                            {
                                default:
                                case Stylesheet.HexCoordinateStyle.Sector: hex = world.Hex; break;
                                case Stylesheet.HexCoordinateStyle.Subsector: hex = world.SubsectorHex; break;
                            }
                            XSize size = ctx.graphics.MeasureString(hex, ctx.styles.hexNumber.Font);
                            solidBrush.Color = ctx.styles.hexNumber.textColor;
                            ctx.graphics.DrawString(hex, ctx.styles.hexNumber.Font, solidBrush, 0.0f, -0.5f, RenderUtil.StringFormatTopCenter);
                        }
                        #endregion
                    }

                    if (layer == WorldLayer.Foreground)
                    {
                        #region Disc
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Type))
                        {
                            if (isPlaceholder)
                            {
                                DrawWorldLabel(ctx, ctx.styles.placeholder.textBackgroundStyle, solidBrush, ctx.styles.placeholder.textColor, ctx.styles.placeholder.position, ctx.styles.placeholder.Font, ctx.styles.placeholder.content);
                            }
                            else
                            {
                                // Blank out world area, so routes are shown correctly
                                if (!ctx.styles.fillMicroBorders)
                                {
                                    solidBrush.Color = ctx.styles.backgroundColor;
                                    ctx.graphics.DrawEllipse(solidBrush, -0.15f, -0.15f, 0.3f, 0.3f);
                                }

                                if (world.Size <= 0)
                                {
                                    #region Asteroid-Belt
                                    if (ctx.styles.worldDetails.HasFlag(WorldDetails.Asteroids))
                                    {
                                        // Basic pattern, with probability varying per position:
                                        //   o o o
                                        //  o o o o
                                        //   o o o

                                        int[] lpx = { -2, 0, 2, -3, -1, 1, 3, -2, 0, 2 };
                                        int[] lpy = { -2, -2, -2, 0, 0, 0, 0, 2, 2, 2 };
                                        float[] lpr = { 0.5f, 0.9f, 0.5f, 0.6f, 0.9f, 0.9f, 0.6f, 0.5f, 0.9f, 0.5f };

                                        solidBrush.Color = ctx.styles.worlds.textColor;

                                        // Random generator is seeded with world location so it is always the same
                                        Random rand = new Random(world.Coordinates.X ^ world.Coordinates.Y);
                                        for (int i = 0; i < lpx.Length; ++i)
                                        {
                                            if (rand.NextDouble() < lpr[i])
                                            {
                                                float px = lpx[i] * 0.035f;
                                                float py = lpy[i] * 0.035f;

                                                float w = 0.04f + (float)rand.NextDouble() * 0.03f;
                                                float h = 0.04f + (float)rand.NextDouble() * 0.03f;

                                                // If necessary, add jitter here
                                                float dx = 0, dy = 0;

                                                ctx.graphics.DrawEllipse(solidBrush,
                                                    px + dx - w / 2, py + dy - h / 2, w, h);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Just a glyph
                                        solidBrush.Color = ctx.styles.worlds.textColor;
                                        RenderUtil.DrawGlyph(ctx.graphics, Glyph.DiamondX, styleRes, solidBrush, 0.0f, 0.0f);
                                    }
                                    #endregion
                                }
                                else
                                {
                                    XColor penColor, brushColor;
                                    ctx.styles.WorldColors(world, out penColor, out brushColor);

                                    if (!brushColor.IsEmpty)
                                    {
                                        solidBrush.Color = brushColor;
                                        ctx.graphics.DrawEllipse(solidBrush, -0.1f, -0.1f, 0.2f, 0.2f);
                                    }

                                    if (!penColor.IsEmpty)
                                    {
                                        ctx.styles.worldWater.pen.Apply(ref pen);
                                        pen.Color = penColor;
                                        ctx.graphics.DrawEllipse(pen, -0.1f, -0.1f, 0.2f, 0.2f);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Dotmap
                            solidBrush.Color = ctx.styles.worlds.textColor;
                            ctx.graphics.DrawEllipse(solidBrush, -0.2f, -0.2f, 0.4f, 0.4f);
                        }
                        #endregion

                        #region Name
                        if (renderName)
                        {
                            string name = world.Name;
                            if (isHiPop)
                                name = name.ToUpperInvariant();

                            Color textColor = isCapital ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;

                            if (ctx.styles.worlds.textStyle.Uppercase)
                                name = name.ToUpper();

                            DrawWorldLabel(ctx, ctx.styles.worlds.textBackgroundStyle, solidBrush, textColor, ctx.styles.worlds.textStyle.Translation, ctx.styles.worlds.Font, name);
                        }
                        #endregion

                        #region Allegiance
                        // TODO: Mask off background for allegiance
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Allegiance))
                        {
                            string alleg = world.Allegiance;
                            if (!SecondSurvey.IsDefaultAllegiance(alleg))
                            {
                                if (!ctx.styles.t5AllegianceCodes && alleg.Length > 2)
                                    alleg = SecondSurvey.T5AllegianceCodeToLegacyCode(alleg);

                                solidBrush.Color = ctx.styles.worlds.textColor;

                                if (ctx.styles.lowerCaseAllegiance)
                                    alleg = alleg.ToLowerInvariant();

                                ctx.graphics.DrawString(alleg, ctx.styles.worlds.SmallFont, solidBrush, ctx.styles.AllegiancePosition.X, ctx.styles.AllegiancePosition.Y, RenderUtil.StringFormatCentered);
                            }
                        }
                        #endregion

                        if (isPlaceholder)
                            return;

                        #region GasGiant
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.GasGiant))
                        {
                            if (world.GasGiants > 0)
                            {
                                solidBrush.Color = ctx.styles.worlds.textColor;
                                RenderUtil.DrawGlyph(ctx.graphics, Glyph.Circle, styleRes, solidBrush, ctx.styles.GasGiantPosition.X, ctx.styles.GasGiantPosition.Y);
                            }
                        }
                        #endregion

                        #region Starport
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Starport))
                        {
                            string starport = world.Starport.ToString();
                            DrawWorldLabel(ctx, ctx.styles.worlds.textBackgroundStyle, solidBrush, ctx.styles.worlds.textColor, ctx.styles.StarportPosition, styleRes.StarportFont, starport);
                        }
                        #endregion

                        #region UWP
                        if (renderUWP)
                        {
                            string uwp = world.UWP;
                            solidBrush.Color = ctx.styles.worlds.textColor;

                            ctx.graphics.DrawString(uwp, ctx.styles.hexNumber.Font, solidBrush, ctx.styles.StarportPosition.X, -ctx.styles.StarportPosition.Y, RenderUtil.StringFormatCentered);
                        }
                        #endregion

                        #region Bases
                        // TODO: Mask off background for glyphs
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Bases))
                        {
                            string bases = world.Bases;

                            // Special case: Show Zho Naval+Military as diamond
                            if (world.BaseAllegiance == "Zh" && bases == "KM")
                                bases = "Z";

                            // Base 1
                            bool bottomUsed = false;
                            if (bases.Length > 0)
                            {
                                Glyph glyph = Glyph.FromBaseCode(world.BaseAllegiance, bases[0]);
                                if (glyph.Printable)
                                {
                                    PointF pt = ctx.styles.BaseTopPosition;
                                    if (glyph.Bias == Glyph.GlyphBias.Bottom)
                                    {
                                        pt = ctx.styles.BaseBottomPosition;
                                        bottomUsed = true;
                                    }

                                    solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, pt.X, pt.Y);
                                }
                            }

                            // Base 2
                            if (bases.Length > 1)
                            {
                                Glyph glyph = Glyph.FromBaseCode(world.LegacyAllegiance, bases[1]);
                                if (glyph.Printable)
                                {
                                    PointF pt = bottomUsed ? ctx.styles.BaseTopPosition : ctx.styles.BaseBottomPosition;
                                    solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, pt.X, pt.Y);
                                }
                            }

                            // Research Stations
                            string rs;
                            if ((rs = world.ResearchStation) != null)
                            {
                                Glyph glyph = Glyph.FromResearchCode(rs);
                                solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, ctx.styles.BaseMiddlePosition.X, ctx.styles.BaseMiddlePosition.Y);
                            }
                            else if (world.IsReserve)
                            {
                                Glyph glyph = Glyph.Reserve;
                                solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, ctx.styles.BaseMiddlePosition.X, 0);
                            }
                            else if (world.IsPenalColony)
                            {
                                Glyph glyph = Glyph.Prison;
                                solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, ctx.styles.BaseMiddlePosition.X, 0);
                            }
                            else if (world.IsPrisonExileCamp)
                            {
                                Glyph glyph = Glyph.ExileCamp;
                                solidBrush.Color = glyph.IsHighlighted ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;
                                RenderUtil.DrawGlyph(ctx.graphics, glyph, styleRes, solidBrush, ctx.styles.BaseMiddlePosition.X, 0);
                            }
                        }
                        #endregion
                    }
                }
                else
                {
                    float imageRadius = ((world.Size <= 0) ? 0.6f : (0.3f * (world.Size / 5.0f + 0.2f))) / 2;
                    float decorationRadius = imageRadius;

                    if (layer == WorldLayer.Background)
                    {
                        #region Disc
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Type))
                        {
                            if (isPlaceholder)
                            {
                                DrawWorldLabel(ctx, ctx.styles.placeholder.textBackgroundStyle, solidBrush, ctx.styles.placeholder.textColor, ctx.styles.placeholder.position, ctx.styles.placeholder.Font, ctx.styles.placeholder.content);
                            } 
                            else if (world.Size <= 0)
                            {
                                const float scaleX = 1.5f;
                                const float scaleY = 1.0f;
                                XImage img = s_worldImages["Belt"];

                                lock (img)
                                {
                                    ctx.graphics.DrawImage(img, -imageRadius * scaleX, -imageRadius * scaleY, imageRadius * 2 * scaleX, imageRadius * 2 * scaleY);
                                }
                            }
                            else
                            {
                                XImage img;
                                switch (world.Hydrographics)
                                {
                                    default:
                                    case 0x0: img = s_worldImages["Hyd0"]; break;
                                    case 0x1: img = s_worldImages["Hyd1"]; break;
                                    case 0x2: img = s_worldImages["Hyd2"]; break;
                                    case 0x3: img = s_worldImages["Hyd3"]; break;
                                    case 0x4: img = s_worldImages["Hyd4"]; break;
                                    case 0x5: img = s_worldImages["Hyd5"]; break;
                                    case 0x6: img = s_worldImages["Hyd6"]; break;
                                    case 0x7: img = s_worldImages["Hyd7"]; break;
                                    case 0x8: img = s_worldImages["Hyd8"]; break;
                                    case 0x9: img = s_worldImages["Hyd9"]; break;
                                    case 0xA: img = s_worldImages["HydA"]; break;
                                }
                                if (img != null)
                                {
                                    lock (img)
                                    {
                                        ctx.graphics.DrawImage(img, -imageRadius, -imageRadius, imageRadius * 2, imageRadius * 2);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Dotmap
                            solidBrush.Color = ctx.styles.worlds.textColor;
                            ctx.graphics.DrawEllipse(solidBrush, -0.2f, -0.2f, 0.4f, 0.4f);
                        }
                        #endregion
                    }

                    if (isPlaceholder)
                        return;

                    if (layer == WorldLayer.Foreground)
                    {
                        #region Zone
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.Zone))
                        {
                            if (world.IsAmber || world.IsRed || world.IsBlue)
                            {
                                PenInfo pi =
                                    world.IsAmber ? ctx.styles.amberZone.pen :
                                    world.IsRed ? ctx.styles.redZone.pen : ctx.styles.blueZone.pen;
                                pi.Apply(ref pen);

                                // TODO: Try and accomplish this using dash pattern
                                decorationRadius += 0.1f;
                                ctx.graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 5, 80);
                                ctx.graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 95, 80);
                                ctx.graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 185, 80);
                                ctx.graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 275, 80);
                            }
                        }
                        #endregion

                        #region GasGiant
                        if (ctx.styles.worldDetails.HasFlag(WorldDetails.GasGiant))
                        {
                            if (world.GasGiants > 0)
                            {
                                decorationRadius += 0.1f;
                                const float symbolRadius = 0.05f;
                                solidBrush.Color = ctx.styles.worlds.textHighlightColor; ;
                                ctx.graphics.DrawEllipse(solidBrush, decorationRadius - symbolRadius, 0.0f - symbolRadius, symbolRadius * 2, symbolRadius * 2);
                            }
                        }
                        #endregion

                        #region UWP
                        if (renderUWP)
                        {
                            string uwp = world.UWP;
                            solidBrush.Color = ctx.styles.worlds.textColor;

                            using (RenderUtil.SaveState(ctx.graphics))
                            {
                                XMatrix uwpMatrix = new XMatrix();
                                uwpMatrix.TranslatePrepend(decorationRadius, 0.0f);
                                uwpMatrix.ScalePrepend(ctx.styles.worlds.textStyle.Scale.Width, ctx.styles.worlds.textStyle.Scale.Height);
                                uwpMatrix.Multiply(uwpMatrix, XMatrixOrder.Prepend);
                                ctx.graphics.DrawString(uwp, ctx.styles.hexNumber.Font, solidBrush, ctx.styles.StarportPosition.X, -ctx.styles.StarportPosition.Y, RenderUtil.StringFormatCenterLeft);
                            }
                        }
                        #endregion

                        #region Name
                        if (renderName)
                        {
                            string name = world.Name;
                            if (isHiPop)
                                name = name.ToUpperInvariant();

                            using (RenderUtil.SaveState(ctx.graphics))
                            {
                                Color textColor = isCapital ? ctx.styles.worlds.textHighlightColor : ctx.styles.worlds.textColor;

                                if (ctx.styles.worlds.textStyle.Uppercase)
                                    name = name.ToUpper();

                                decorationRadius += 0.1f;
                                XMatrix imageMatrix = new XMatrix();
                                imageMatrix.TranslatePrepend(decorationRadius, 0.0f);
                                imageMatrix.ScalePrepend(ctx.styles.worlds.textStyle.Scale.Width, ctx.styles.worlds.textStyle.Scale.Height);
                                imageMatrix.TranslatePrepend(ctx.graphics.MeasureString(name, ctx.styles.worlds.Font).Width / 2, 0.0f); // Left align
                                ctx.graphics.MultiplyTransform(imageMatrix, XMatrixOrder.Prepend);

                                DrawWorldLabel(ctx, ctx.styles.worlds.textBackgroundStyle, solidBrush, textColor, ctx.styles.worlds.textStyle.Translation, ctx.styles.worlds.Font, name);
                            }
                        }
                        #endregion
                    }
                }
            }
        }

        private static void DrawWorldLabel(RenderContext ctx, TextBackgroundStyle backgroundStyle, XSolidBrush brush, Color color, PointF position, XFont font, string text)
        {
            XSize size = ctx.graphics.MeasureString(text, font);

            switch (backgroundStyle)
            {
                case TextBackgroundStyle.None:
                    break;

                default:
                case TextBackgroundStyle.Rectangle:
                    if (!ctx.styles.fillMicroBorders)
                    {
                        // TODO: Implement this with a clipping region instead
                        brush.Color = ctx.styles.backgroundColor;
                        ctx.graphics.DrawRectangle(brush, position.X - size.Width / 2, position.Y - size.Height / 2, size.Width, size.Height);
                    }
                    break;
             
                case TextBackgroundStyle.Outline:
                case TextBackgroundStyle.Shadow:
                    {
                        // TODO: These scaling factors are constant for a render; compute once

                        // Invert the current scaling transforms
                        float sx = 1.0f / ctx.styles.hexContentScale;
                        float sy = 1.0f / ctx.styles.hexContentScale;
                        sx *= Astrometrics.ParsecScaleX;
                        sy *= Astrometrics.ParsecScaleY;
                        sx /= (float)ctx.scale * Astrometrics.ParsecScaleX;
                        sy /= (float)ctx.scale * Astrometrics.ParsecScaleY;

                        const int outlineSize = 2;
                        const int outlineSkip = 1;

                        int outlineStart = backgroundStyle == TextBackgroundStyle.Outline
                            ? -outlineSize
                            : 0;

                        brush.Color = ctx.styles.backgroundColor;

                        for (int dx = outlineStart; dx <= outlineSize; dx += outlineSkip)
                        {
                            for (int dy = outlineStart; dy <= outlineSize; dy += outlineSkip)
                            {
                                ctx.graphics.DrawString(text, font, brush, position.X + sx * dx, position.Y + sy * dy, RenderUtil.StringFormatCentered);
                            }
                        }
                        break;
                    }
            }

            brush.Color = color;
            ctx.graphics.DrawString(text, font, brush, position.X, position.Y, RenderUtil.StringFormatCentered);
        }

        private static void DrawLabels(RenderContext ctx, FontCache styleRes)
        {
            using (RenderUtil.SaveState(ctx.graphics))
            {
                XSolidBrush solidBrush = new XSolidBrush();

                ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                
                foreach (Sector sector in ctx.selector.Sectors)
                {
                    solidBrush.Color = ctx.styles.microBorders.textColor;
                    foreach (Border border in sector.Borders.Where(border => border.ShowLabel))
                    {
                        Allegiance alleg = sector.GetAllegianceFromCode(border.Allegiance);
                        if (alleg != null)
                        {
                            string text = border.Label ?? alleg.Name;
                            Point labelHex = border.LabelPosition;
                            PointF labelPos = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, labelHex)));
                            // TODO: Replace these with, well, positions!
                            //labelPos.X -= 0.5f;
                            //labelPos.Y -= 0.5f;

                            System.Text.RegularExpressions.Regex r = new System.Text.RegularExpressions.Regex(@"\s+(?![a-z])");
                            if (border.WrapLabel)
                                text = r.Replace(text, "\n");
                                //text = text.Replace(' ', '\n');
                            
                            RenderUtil.DrawLabel(ctx.graphics, text, labelPos, ctx.styles.microBorders.Font, solidBrush, ctx.styles.microBorders.textStyle);
                        }
                    }

                    foreach (Label label in sector.Labels)
                    {
                        string text = label.Text;
                        Point labelHex = new Point(label.Hex / 100, label.Hex % 100);
                        PointF labelPos = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, labelHex)));
                        // TODO: Adopt some of the tweaks from .MSEC
                        labelPos.Y -= label.OffsetY * 0.7f;

                        XFont font;
                        switch (label.Size)
                        {
                            case "small": font = ctx.styles.microBorders.SmallFont; break;
                            case "large": font = ctx.styles.microBorders.LargeFont; break;
                            default: font = ctx.styles.microBorders.Font; break;
                        }

                        if (!ctx.styles.grayscale &&
                            ColorUtil.NoticeableDifference(label.Color, ctx.styles.backgroundColor) &&
                            (label.Color != Label.DefaultColor))
                            solidBrush.Color = label.Color;
                        else
                            solidBrush.Color = ctx.styles.microBorders.textColor;
                        RenderUtil.DrawLabel(ctx.graphics, text, labelPos, font, solidBrush, ctx.styles.microBorders.textStyle);
                    }
                }
            }
        }

        private static void DrawRoutes(RenderContext ctx, FontCache styleRes)
        {
            using (RenderUtil.SaveState(ctx.graphics))
            {
                ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                XPen pen = new XPen(XColor.Empty);
                ctx.styles.microRoutes.pen.Apply(ref pen);
                float baseWidth = ctx.styles.microRoutes.pen.width;

                foreach (Sector sector in ctx.selector.Sectors)
                {
                    foreach (Route route in sector.Routes)
                    {
                        // Compute source/target sectors (may be offset)
                        Point startSector = sector.Location, endSector = sector.Location;
                        startSector.Offset(route.StartOffset);
                        endSector.Offset(route.EndOffset);

                        Location startLocation = new Location(startSector, route.Start);
                        Location endLocation = new Location(endSector, route.End);

                        PointF startPoint = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(startLocation));
                        PointF endPoint = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(endLocation));

                        // If drawing dashed lines twice and the start/end are swapped the 
                        // dashes don't overlap correctly. So "sort" the points.
                        if ((startPoint.X > endPoint.X) ||
                            (startPoint.X == endPoint.X) && (startPoint.Y > endPoint.Y))
                        {
                            PointF tmp = startPoint;
                            startPoint = endPoint;
                            endPoint = tmp;
                        }

                        float? routeWidth = route.Width;
                        Color? routeColor = route.Color;
                        Route.RouteStyle? routeStyle = route.Style;

                        //
                        SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("route", route.Allegiance ?? route.Type ?? "Im");
                        routeStyle = routeStyle ?? ssr.GetEnum<Route.RouteStyle>("style");
                        routeColor = routeColor ?? ssr.GetColor("color");
                        routeWidth = routeWidth ?? (float?)ssr.GetNumber("width") ?? 1.0f;

                        // In grayscale, convert default color and style to non-default style
                        if (ctx.styles.grayscale && !routeColor.HasValue && !routeStyle.HasValue)
                            routeStyle = Route.RouteStyle.Dashed;

                        routeColor = routeColor ?? ctx.styles.microRoutes.pen.color;
                        routeStyle = routeStyle ?? Route.RouteStyle.Solid;

                        // Ensure color is visible
                        if (ctx.styles.grayscale || !ColorUtil.NoticeableDifference(routeColor.Value, ctx.styles.backgroundColor))
                            routeColor = ctx.styles.microRoutes.pen.color; // default

                        pen.Color = routeColor.Value;
                        pen.Width = routeWidth.Value * baseWidth;
                        pen.DashStyle = RouteStyleToDashStyle(routeStyle.Value);

                        ctx.graphics.DrawLine(pen, startPoint, endPoint);
                    }
                }
            }
        }

        private static XDashStyle RouteStyleToDashStyle(Route.RouteStyle routeStyle)
        {
            switch (routeStyle)
            {
                default:
                case Route.RouteStyle.Solid: return XDashStyle.Solid;
                case Route.RouteStyle.Dashed: return XDashStyle.Dash;
                case Route.RouteStyle.Dotted: return XDashStyle.Dot;
            }
        }

        private enum BorderLayer { Fill, Stroke };
        private static void DrawMicroBorders(RenderContext ctx, FontCache styleRes, BorderLayer layer)
        {
            const byte FILL_ALPHA = 64;

            float[] edgex, edgey;
            PathUtil.PathType borderPathType = ctx.styles.microBorderStyle == MicroBorderStyle.Square ?
                PathUtil.PathType.Square : PathUtil.PathType.Hex;
            RenderUtil.HexEdges(borderPathType, out edgex, out edgey);

            XSolidBrush solidBrush = new XSolidBrush();
            XPen pen = new XPen(XColor.Empty);
            ctx.styles.microBorders.pen.Apply(ref pen);

            foreach (Sector sector in ctx.selector.Sectors)
            {
                XGraphicsPath sectorClipPath = null;

                using (RenderUtil.SaveState(ctx.graphics))
                {
                    // This looks craptacular for Candy style borders :(
                    if (ctx.clipOutsectorBorders &&
                        (layer == BorderLayer.Fill || ctx.styles.microBorderStyle != MicroBorderStyle.Curve))
                    {
                        Sector.ClipPath clip = sector.ComputeClipPath(borderPathType);
                        if (!ctx.tileRect.IntersectsWith(clip.bounds))
                            continue;

                        sectorClipPath = new XGraphicsPath(clip.clipPathPoints, clip.clipPathPointTypes, XFillMode.Alternate);
                        if (sectorClipPath != null)
                            ctx.graphics.IntersectClip(sectorClipPath);
                    }

                    ctx.graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                    foreach (Border border in sector.Borders)
                    {
                        BorderPath borderPath = border.ComputeGraphicsPath(sector, borderPathType);

                        XGraphicsPath drawPath = borderPath.borderPathPoints.Length > 0 ? new XGraphicsPath(borderPath.borderPathPoints, borderPath.borderPathTypes, XFillMode.Alternate) : null;
                        XGraphicsPath clipPath = new XGraphicsPath(borderPath.clipPathPoints, borderPath.clipPathTypes, XFillMode.Alternate);

                        Color? borderColor = border.Color;
                        SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("border", border.Allegiance);
                        borderColor = borderColor ?? ssr.GetColor("color") ?? ctx.styles.microBorders.pen.color;
                        
                        if (ctx.styles.grayscale ||
                            !ColorUtil.NoticeableDifference(borderColor.Value, ctx.styles.backgroundColor))
                        {
                            borderColor = ctx.styles.microBorders.pen.color; // default
                        }
                        pen.Color = borderColor.Value;

                        if (ctx.styles.microBorderStyle != MicroBorderStyle.Curve)
                        {
                            // Clip to the path itself - this means adjacent borders don't clash
                            using (RenderUtil.SaveState(ctx.graphics))
                            {
                                ctx.graphics.IntersectClip(clipPath);
                                if (layer == BorderLayer.Fill)
                                {
                                    solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                    ctx.graphics.DrawPath(solidBrush, clipPath);
                                }

                                if (layer == BorderLayer.Stroke && drawPath != null)
                                    ctx.graphics.DrawPath(pen, drawPath);
                            }
                        }
                        else
                        {
                            if (layer == BorderLayer.Fill)
                            {
                                solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                ctx.graphics.DrawClosedCurve(solidBrush, borderPath.clipPathPoints);
                            }

                            if (layer == BorderLayer.Stroke)
                            {
                                foreach (PointF[] curve in borderPath.curvePoints)
                                {
                                    // TODO: Investigate DrawClosedCurve to handle endings
                                    // Would need to have path computer tell whether
                                    // or not the path was actually a closed loop
                                    // Can do it by clipping borders to sector, but that loses 
                                    // bottom/right overlaps
                                    ctx.graphics.DrawCurve(pen, curve, 0.6f);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
