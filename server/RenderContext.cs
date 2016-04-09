//#define SHOW_TIMING

using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;

namespace Maps.Rendering
{
    internal class RenderContext
    {
        public RenderContext(ResourceManager resourceManager, Selector selector, RectangleF tileRect,
            double scale, MapOptions options, Stylesheet styles, Size tileSize)
        {
            this.resourceManager = resourceManager;
            this.selector = selector;
            this.tileRect = tileRect;
            this.scale = scale;
            this.options = options;
            this.styles = styles;
            this.tileSize = tileSize;

            XMatrix m = new XMatrix();
            m.TranslatePrepend((float)(-tileRect.Left * scale * Astrometrics.ParsecScaleX), (float)(-tileRect.Top * scale * Astrometrics.ParsecScaleY));
            m.ScalePrepend((float)scale * Astrometrics.ParsecScaleX, (float)scale * Astrometrics.ParsecScaleY);
            imageSpaceToWorldSpace = m;
            m.Invert();
            worldSpaceToImageSpace = m;
        }

        // Required
        private readonly ResourceManager resourceManager;
        private readonly Selector selector;
        private readonly RectangleF tileRect;
        private readonly double scale;
        private readonly MapOptions options;
        private readonly Stylesheet styles;
        private readonly Size tileSize;

        // Options
        public XGraphicsPath ClipPath { get; set; }
        public bool DrawBorder { get; set; }
        public bool Silly { get; set; }
        public bool ClipOutsectorBorders { get; set; }

        // Assigned during Render()
        private XGraphics graphics = null;
        private XSolidBrush solidBrush;
        private XPen pen;

        public Stylesheet Styles { get { return styles; } }

        private readonly XMatrix imageSpaceToWorldSpace;
        private readonly XMatrix worldSpaceToImageSpace;
        public XMatrix ImageSpaceToWorldSpace { get { return imageSpaceToWorldSpace;  } }

        private static readonly RectangleF galacticBounds = new RectangleF(-14598.67f, -23084.26f, 29234.1133f, 25662.4746f); // TODO: Don't hardcode
        private static readonly Rectangle galaxyImageRect = new Rectangle(-18257, -26234, 36551, 32462); // Chosen to match T5 pp.416

        // This transforms the Linehan galactic structure to the Mikesh galactic structure
        // See https://travellermap.blogspot.com/2009/03/galaxy-scale-mismatch.html
        private static readonly Matrix xformLinehanToMikesh = new Matrix(0.9181034f, 0.0f, 0.0f, 0.855192542f, 120.672432f, 86.34569f);

        private static readonly Rectangle riftImageRect = new Rectangle(-1374, -827, 2769, 1754);

        #region labels
        private class MapLabel
        {
            public MapLabel(string text, float x, float y, bool minor = false) { this.text = text; position = new PointF(x, y); this.minor = minor; }
            public readonly string text;
            public readonly PointF position;
            public readonly bool minor;
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

        // TODO: Move this to data file
        private static readonly MapLabel[] megaLabels =
        {
            new MapLabel("Charted Space", 0, 400, true),
            new MapLabel("Zhodani Core Expeditions", 0, -2000, true),
            new MapLabel("Core Sophonts", 0, -10000),
            new MapLabel("Abyssals", -12000, -8500),
            new MapLabel("Denizens", -7000, -7000),
            new MapLabel("Essaray", 6000, -12000),
            new MapLabel("Dushis Khurisi", 0, -19000, true),
            new MapLabel("The Barren Arm", 8000, -4500, true),
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
        #endregion

        #region Static Caches
        private static object s_imageInitLock = new object();

        // TODO: Consider not caching these across sessions
        private static XImage s_sillyImageColor;
        private static XImage s_sillyImageGray;

        private static XImage s_nebulaImage;

        // These are loaded as GDI+ Images since we need to derive alpha-variants of them;
        // the results are cached as PDFSharp Images (XImage)
        private static ImageHolder s_galaxyImage;
        private static ImageHolder s_galaxyImageGray;
        private static ImageHolder s_riftImage;
        private static Dictionary<string, XImage> s_worldImages;
        #endregion

        /// <summary>
        /// Performance timer record
        /// </summary>
        private class Timer
        {
#if SHOW_TIMING
            public DateTime dt;
            public string label;
            public Timer(string label)
            {
                this.dt = DateTime.Now;
                this.label = label;
            }
#else
            public Timer(string label) { }
#endif
        }

        public void Render(XGraphics graphics)
        {
            this.graphics = graphics;
            solidBrush = new XSolidBrush();
            pen = new XPen(XColor.Empty);

            List<Timer> timers = new List<Timer>();

            using (var fonts = new FontCache(styles))
            {
                #region resources
                lock (s_imageInitLock)
                {
                    if (styles.showNebulaBackground && s_nebulaImage == null)
                        s_nebulaImage = XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Nebula.png"));

                    if (styles.showRiftOverlay && s_riftImage == null)
                        s_riftImage = new ImageHolder(Image.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Rifts.png")));

                    if (styles.showGalaxyBackground && s_galaxyImage == null) {
                        // TODO: Don't load both unless necessary
                        s_galaxyImage = new ImageHolder(Image.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Galaxy.png")));
                        s_galaxyImageGray = new ImageHolder(Image.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Galaxy_Gray.png")));
                    }

                    if (styles.useWorldImages && s_worldImages == null)
                    {
                        s_worldImages = new Dictionary<string, XImage> {
                            { "Hyd0", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd0.png")) },
                            { "Hyd1", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd1.png")) },
                            { "Hyd2", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd2.png")) },
                            { "Hyd3", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd3.png")) },
                            { "Hyd4", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd4.png")) },
                            { "Hyd5", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd5.png")) },
                            { "Hyd6", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd6.png")) },
                            { "Hyd7", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd7.png")) },
                            { "Hyd8", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd8.png")) },
                            { "Hyd9", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Hyd9.png")) },
                            { "HydA", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/HydA.png")) },
                            { "Belt", XImage.FromFile(resourceManager.Server.MapPath(@"~/res/Candy/Belt.png")) }
                        };
                    }

                    if (Silly && s_sillyImageColor == null)
                    {
                        // Happy face c/o http://bighappyfaces.com/
                        s_sillyImageColor = XImage.FromFile(resourceManager.Server.MapPath(@"~/res/AprilFools/Starburst.png"));
                        s_sillyImageGray = XImage.FromFile(resourceManager.Server.MapPath(@"~/res/AprilFools/Starburst_Gray.png"));
                    }
                }
                #endregion

                timers.Add(new Timer("preload"));
                //////////////////////////////////////////////////////////////
                //
                // Image-Space Rendering
                //
                //////////////////////////////////////////////////////////////

                using (RenderUtil.SaveState(graphics))
                {
                    if (ClipPath != null)
                    {
                        graphics.MultiplyTransform(imageSpaceToWorldSpace);
                        graphics.IntersectClip(ClipPath);
                        graphics.MultiplyTransform(worldSpaceToImageSpace);
                    }

                    // Fill
                    graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                    solidBrush.Color = styles.backgroundColor;
                    graphics.DrawRectangle(solidBrush, 0, 0, tileSize.Width, tileSize.Height);
                }

                timers.Add(new Timer("imagespace"));

                //////////////////////////////////////////////////////////////
                //
                // World-Space Rendering
                //
                //////////////////////////////////////////////////////////////

                graphics.MultiplyTransform(imageSpaceToWorldSpace);

                using (RenderUtil.SaveState(graphics))
                {
                    //------------------------------------------------------------
                    // Explicit Clipping
                    //------------------------------------------------------------

                    if (ClipPath != null)
                        graphics.IntersectClip(ClipPath);

                    //------------------------------------------------------------
                    // Background
                    //------------------------------------------------------------

                    timers.Add(new Timer("prep"));

                    #region nebula-background
                    //------------------------------------------------------------
                    // Local background (Nebula)
                    //------------------------------------------------------------
                    // NOTE: Since alpha texture brushes aren't supported without
                    // creating a new image (slow!) we render the local background
                    // first, then overlay the deep background over it, for
                    // basically the same effect since the alphas sum to 1.

                    if (styles.showNebulaBackground)
                        DrawNebulaBackground();
                    timers.Add(new Timer("background (nebula)"));
                    #endregion

                    #region galaxy-background
                    //------------------------------------------------------------
                    // Deep background (Galaxy)
                    //------------------------------------------------------------
                    if (styles.showGalaxyBackground && styles.deepBackgroundOpacity > 0f && galacticBounds.IntersectsWith(tileRect))
                    {
                        using (RenderUtil.SaveState(graphics))
                        {
                            graphics.MultiplyTransform(xformLinehanToMikesh);
                            ImageHolder galaxyImage = styles.lightBackground ? s_galaxyImageGray : s_galaxyImage;
                            RenderUtil.DrawImageAlpha(graphics, styles.deepBackgroundOpacity, galaxyImage, galaxyImageRect);
                        }
                    }
                    timers.Add(new Timer("background (galaxy)"));
                    #endregion

                    #region pseudorandom-stars
                    //------------------------------------------------------------
                    // Pseudo-Random Stars
                    //------------------------------------------------------------
                    if (styles.pseudoRandomStars.visible)
                        DrawPseudoRandomStars();
                    timers.Add(new Timer("pseudorandom"));
                    #endregion

                    #region rifts
                    //------------------------------------------------------------
                    // Rifts in Charted Space
                    //------------------------------------------------------------
                    if (styles.showRiftOverlay && styles.riftOpacity > 0f)
                        RenderUtil.DrawImageAlpha(graphics, styles.riftOpacity, s_riftImage, riftImageRect);
                    timers.Add(new Timer("rifts"));
                    #endregion

                    #region april-fools
                    //------------------------------------------------------------
                    // April Fool's Day
                    //------------------------------------------------------------
                    if (Silly)
                    {
                        using (RenderUtil.SaveState(graphics))
                        {
                            // Render in image-space
                            graphics.MultiplyTransform(worldSpaceToImageSpace);

                            XImage sillyImage = styles.grayscale ? s_sillyImageGray : s_sillyImageColor;

                            lock (sillyImage)
                            {
                                graphics.DrawImage(sillyImage, 0, 0, tileSize.Width, tileSize.Height);
                            }
                        }
                        timers.Add(new Timer("silly"));
                    }
                    #endregion

                    //------------------------------------------------------------
                    // Foreground
                    //------------------------------------------------------------

                    #region macro-borders
                    //------------------------------------------------------------
                    // Macro: Borders object
                    //------------------------------------------------------------
                    if (styles.macroBorders.visible)
                    {
                        styles.macroBorders.pen.Apply(ref pen);
                        graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                        foreach (var vec in borderFiles
                            .Select(file => resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & options & MapOptions.BordersMask) != 0))
                        {
                            vec.Draw(graphics, tileRect, pen);
                        }
                    }
                    timers.Add(new Timer("macro-borders"));
                    #endregion

                    #region macro-routes
                    //------------------------------------------------------------
                    // Macro: Route object
                    //------------------------------------------------------------
                    if (styles.macroRoutes.visible)
                    {
                        styles.macroRoutes.pen.Apply(ref pen);
                        graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                        foreach (var vec in routeFiles
                            .Select(file => resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                            .OfType<VectorObject>()
                            .Where(vec => (vec.MapOptions & options & MapOptions.BordersMask) != 0))
                        {
                            vec.Draw(graphics, tileRect, pen);
                        }
                    }
                    timers.Add(new Timer("macro-routes"));
                    #endregion

                    #region sector-grid
                    //------------------------------------------------------------
                    // Sector Grid
                    //------------------------------------------------------------
                    graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                    if (styles.sectorGrid.visible)
                    {
                        const int gridSlop = 10;
                        styles.sectorGrid.pen.Apply(ref pen);

                        for (float h = ((float)(Math.Floor((tileRect.Left) / Astrometrics.SectorWidth) - 1) - Astrometrics.ReferenceSector.X) * Astrometrics.SectorWidth - Astrometrics.ReferenceHex.X; h <= tileRect.Right + Astrometrics.SectorWidth; h += Astrometrics.SectorWidth)
                            graphics.DrawLine(pen, h, tileRect.Top - gridSlop, h, tileRect.Bottom + gridSlop);

                        for (float v = ((float)(Math.Floor((tileRect.Top) / Astrometrics.SectorHeight) - 1) - Astrometrics.ReferenceSector.Y) * Astrometrics.SectorHeight - Astrometrics.ReferenceHex.Y; v <= tileRect.Bottom + Astrometrics.SectorHeight; v += Astrometrics.SectorHeight)
                            graphics.DrawLine(pen, tileRect.Left - gridSlop, v, tileRect.Right + gridSlop, v);
                    }
                    timers.Add(new Timer("sector grid"));
                    #endregion

                    #region subsector-grid
                    //------------------------------------------------------------
                    // Subsector Grid
                    //------------------------------------------------------------
                    graphics.SmoothingMode = XSmoothingMode.HighSpeed;
                    if (styles.subsectorGrid.visible)
                    {
                        const int gridSlop = 10;
                        styles.subsectorGrid.pen.Apply(ref pen);

                        int hmin = (int)Math.Floor(tileRect.Left / Astrometrics.SubsectorWidth) - 1 - Astrometrics.ReferenceSector.X,
                            hmax = (int)Math.Ceiling((tileRect.Right + Astrometrics.SubsectorWidth + Astrometrics.ReferenceHex.X) / Astrometrics.SubsectorWidth);
                        for (int hi = hmin; hi <= hmax; ++hi)
                        {
                            if (hi % 4 == 0) continue;
                            float h = hi * Astrometrics.SubsectorWidth - Astrometrics.ReferenceHex.X;
                            graphics.DrawLine(pen, h, tileRect.Top - gridSlop, h, tileRect.Bottom + gridSlop);
                        }

                        int vmin = (int)Math.Floor(tileRect.Top / Astrometrics.SubsectorHeight) - 1 - Astrometrics.ReferenceSector.Y,
                            vmax = (int)Math.Ceiling((tileRect.Bottom + Astrometrics.SubsectorHeight + Astrometrics.ReferenceHex.Y) / Astrometrics.SubsectorHeight);
                        for (int vi = vmin; vi <= vmax; ++vi)
                        {
                            if (vi % 4 == 0) continue;
                            float v = vi * Astrometrics.SubsectorHeight - Astrometrics.ReferenceHex.Y;
                            graphics.DrawLine(pen, tileRect.Left - gridSlop, v, tileRect.Right + gridSlop, v);
                        }
                    }
                    timers.Add(new Timer("subsector grid"));
                    #endregion

                    #region parsec-grid
                    //------------------------------------------------------------
                    // Parsec Grid
                    //------------------------------------------------------------
                    // TODO: Optimize - timers indicate this is slow
                    graphics.SmoothingMode = XSmoothingMode.HighQuality;
                    if (styles.parsecGrid.visible)
                        DrawParsecGrid();
                    timers.Add(new Timer("parsec grid"));
                    #endregion

                    #region subsector-names
                    //------------------------------------------------------------
                    // Subsector Names
                    //------------------------------------------------------------
                    if (styles.subsectorNames.visible)
                    {
                        solidBrush.Color = styles.subsectorNames.textColor;
                        foreach (Sector sector in selector.Sectors)
                        {
                            for (int i = 0; i < 16; i++)
                            {
                                Subsector ss = sector.Subsector(i);
                                if (ss == null || string.IsNullOrEmpty(ss.Name))
                                    continue;

                                Point center = sector.SubsectorCenter(i);
                                RenderUtil.DrawLabel(graphics, ss.Name, center, styles.subsectorNames.Font, solidBrush, styles.subsectorNames.textStyle);
                            }
                        }
                    }
                    timers.Add(new Timer("subsector names"));
                    #endregion

                    #region micro-borders
                    //------------------------------------------------------------
                    // Micro: Borders
                    //------------------------------------------------------------
                    if (styles.microBorders.visible)
                    {
                        if (styles.fillMicroBorders)
                        {
                            DrawMicroBorders(BorderLayer.Fill);
                            DrawRegions(BorderLayer.Fill);
                        }

                        DrawMicroBorders(BorderLayer.Stroke);
                        DrawRegions(BorderLayer.Stroke);
                    }
                    timers.Add(new Timer("micro-borders"));
                    #endregion

                    #region micro-routes
                    //------------------------------------------------------------
                    // Micro: Routes
                    //------------------------------------------------------------
                    if (styles.microRoutes.visible)
                        DrawRoutes();
                    timers.Add(new Timer("micro-routes"));
                    #endregion

                    #region micro-border-labels
                    //------------------------------------------------------------
                    // Micro: Border Labels & Explicit Labels
                    //------------------------------------------------------------
                    if (styles.showMicroNames)
                        DrawLabels();
                    timers.Add(new Timer("micro-border labels"));
                    #endregion

                    #region sector-names
                    //------------------------------------------------------------
                    // Sector Names
                    //------------------------------------------------------------
                    if (styles.showSomeSectorNames || styles.showAllSectorNames)
                    {
                        foreach (Sector sector in selector.Sectors
                            .Where(sector => styles.showAllSectorNames || (styles.showSomeSectorNames && sector.Selected))
                            .Where(sector => sector.Names.Any() || sector.Label != null))
                        {
                            solidBrush.Color = styles.sectorName.textColor;
                            string name = sector.Label ?? sector.Names[0].Text;

                            RenderUtil.DrawLabel(graphics, name, sector.Center, styles.sectorName.Font, solidBrush, styles.sectorName.textStyle);
                        }
                    }
                    timers.Add(new Timer("sector names"));
                    #endregion

                    #region government-rift-names
                    //------------------------------------------------------------
                    // Macro: Government / Rift / Route Names
                    //------------------------------------------------------------
                    if (styles.macroNames.visible)
                        DrawMacroNames();
                    timers.Add(new Timer("macro names"));
                    #endregion

                    #region capitals-homeworlds
                    //------------------------------------------------------------
                    // Macro: Capitals & Home Worlds
                    //------------------------------------------------------------
                    if (styles.capitals.visible && (options & MapOptions.WorldsMask) != 0)
                    {
                        WorldObjectCollection worlds = resourceManager.GetXmlFileObject(@"~/res/Worlds.xml", typeof(WorldObjectCollection)) as WorldObjectCollection;
                        if (worlds != null && worlds.Worlds != null)
                        {
                            solidBrush.Color = styles.capitals.textColor;
                            foreach (WorldObject world in worlds.Worlds.Where(world => (world.MapOptions & options) != 0))
                            {
                                world.Paint(graphics, styles.capitals.fillColor, solidBrush, styles.macroNames.SmallFont);
                            }
                        }
                    }
                    timers.Add(new Timer("macro worlds"));
                    #endregion

                    #region mega-names
                    //------------------------------------------------------------
                    // Mega: Galaxy-Scale Labels
                    //------------------------------------------------------------
                    if (styles.megaNames.visible)
                    {
                        solidBrush.Color = styles.megaNames.textColor;
                        foreach (var label in megaLabels)
                        {
                            using (RenderUtil.SaveState(graphics))
                            {
                                XMatrix matrix = new XMatrix();
                                matrix.ScalePrepend(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                                matrix.TranslatePrepend(label.position.X, label.position.Y);
                                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                                XFont font = label.minor ? styles.megaNames.SmallFont : styles.megaNames.Font;
                                XSize size = graphics.MeasureString(label.text, font);
                                graphics.TranslateTransform(-size.Width / 2, -size.Height / 2); // Center the text
                                RectangleF textBounds = new RectangleF(0, 0, (float)size.Width * 1.01f, (float)size.Height * 2); // *2 or it gets cut off at high sizes
                                XTextFormatter formatter = new XTextFormatter(graphics);
                                formatter.Alignment = XParagraphAlignment.Center;
                                formatter.DrawString(label.text, font, solidBrush, textBounds);
                            }
                        }
                    }
                    timers.Add(new Timer("mega names"));
                    #endregion
                }

                // End of clipping, so world names are not clipped in jumpmaps.

                #region worlds
                //------------------------------------------------------------
                // Worlds
                //------------------------------------------------------------
                if (styles.worlds.visible)
                {
                    if (styles.showStellarOverlay)
                    {
                        foreach (World world in selector.Worlds) { DrawStars(world); }
                    }
                    else {
                        foreach (World world in selector.Worlds) { DrawWorld(fonts, world, WorldLayer.Background); }
                        foreach (World world in selector.Worlds) { DrawWorld(fonts, world, WorldLayer.Foreground); }

                        if (styles.HasWorldOverlays)
                        {
                            float slop = selector.SlopFactor;
                            selector.SlopFactor = (float)Math.Max(slop, Math.Log(scale, 2.0) - 4);
                            foreach (World world in selector.Worlds) { DrawWorld(fonts, world, WorldLayer.Overlay); }
                            selector.SlopFactor = slop;
                        }
                    }
                }
                timers.Add(new Timer("worlds"));
#endregion

                //------------------------------------------------------------
                // Overlays
                //------------------------------------------------------------

#region droyne
                //------------------------------------------------------------
                // Droyne/Chirper Worlds
                //------------------------------------------------------------
                if (styles.droyneWorlds.visible)
                {
                    solidBrush.Color = styles.droyneWorlds.textColor;
                    foreach (World world in selector.Worlds)
                    {
                        bool droyne = world.HasCodePrefix("Droy") != null;
                        bool chirpers = world.HasCodePrefix("Chir") != null;
                        if (droyne || chirpers)
                        {
                            string glyph = droyne ? "\u2605" : "\u2606";
                            PointF center = Astrometrics.HexToCenter(world.Coordinates);
                            using (RenderUtil.SaveState(graphics))
                            {
                                XMatrix matrix = new XMatrix();
                                matrix.TranslatePrepend(center.X, center.Y);
                                matrix.ScalePrepend(1 / Astrometrics.ParsecScaleX, 1 / Astrometrics.ParsecScaleY);
                                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);
                                graphics.DrawString(glyph, styles.droyneWorlds.Font, solidBrush, 0, 0, RenderUtil.StringFormatCentered);
                            }
                        }
                    }
                }
                timers.Add(new Timer("droyne"));
                #endregion

                #region minorHomeWorlds
                //------------------------------------------------------------
                // Minor Homeworlds 
                //------------------------------------------------------------
                if (styles.minorHomeWorlds.visible)
                {
                    solidBrush.Color = styles.minorHomeWorlds.textColor;
                    foreach (World world in selector.Worlds)
                    {
                        if (world.HasCodePrefix("(") != null)
                        {
                            string glyph = "\u273B";
                            PointF center = Astrometrics.HexToCenter(world.Coordinates);
                            using (RenderUtil.SaveState(graphics))
                            {
                                XMatrix matrix = new XMatrix();
                                matrix.TranslatePrepend(center.X, center.Y);
                                matrix.ScalePrepend(1 / Astrometrics.ParsecScaleX, 1 / Astrometrics.ParsecScaleY);
                                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);
                                graphics.DrawString(glyph, styles.minorHomeWorlds.Font, solidBrush, 0, 0, RenderUtil.StringFormatCentered);
                            }
                        }
                    }
                }
                timers.Add(new Timer("minor"));
                #endregion

                #region ancients
                //------------------------------------------------------------
                // Ancients Worlds
                //------------------------------------------------------------
                if (styles.ancientsWorlds.visible)
                {
                    solidBrush.Color = styles.ancientsWorlds.textColor;
                    foreach (World world in selector.Worlds)
                    {
                        string glyph = "\u2600";

                        if (world.HasCode("An"))
                        {
                            PointF center = Astrometrics.HexToCenter(world.Coordinates);
                            using (RenderUtil.SaveState(graphics))
                            {
                                XMatrix matrix = new XMatrix();
                                matrix.TranslatePrepend(center.X, center.Y);
                                matrix.ScalePrepend(1 / Astrometrics.ParsecScaleX, 1 / Astrometrics.ParsecScaleY);
                                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);
                                graphics.DrawString(glyph, styles.ancientsWorlds.Font, solidBrush, 0, 0, RenderUtil.StringFormatCentered);
                            }
                        }
                    }
                }
                timers.Add(new Timer("ancients"));
                #endregion

                #region unofficial
                //------------------------------------------------------------
                // Unofficial
                //------------------------------------------------------------
                if (styles.dimUnofficialSectors && styles.worlds.visible)
                {
                    solidBrush.Color = Color.FromArgb(128, styles.backgroundColor);
                    foreach (Sector sector in selector.Sectors
                        .Where(sector => !sector.Tags.Contains("Official") && !sector.Tags.Contains("Preserve") && !sector.Tags.Contains("InReview")))
                        graphics.DrawRectangle(solidBrush, sector.Bounds);
                }
                timers.Add(new Timer("unofficial"));
#endregion

#region timing
#if SHOW_TIMING
                using( RenderUtil.SaveState( graphics ) )
                {
                    XFont font = new XFont( FontFamily.GenericSansSerif, 12, XFontStyle.Regular, new XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode) );
                    graphics.MultiplyTransform( worldSpaceToImageSpace );
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

                                graphics.DrawString( String.Format( "{0} {1}", Math.Round( ts.TotalMilliseconds ), s.label ), font, XBrushes.Black, cursorX + dx, cursorY + dy );
                            }
                        }
                        graphics.DrawString( String.Format("{0} {1}", Math.Round(ts.TotalMilliseconds), s.label), font, XBrushes.Yellow, cursorX, cursorY );
                        cursorY += 14;
                    }
                }
#endif
#endregion
            }
        }

        private void DrawMacroNames()
        {
            foreach (var vec in borderFiles
                .Select(file => resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                .OfType<VectorObject>()
                .Where(vec => (vec.MapOptions & options & MapOptions.NamesMask) != 0))
            {
                bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                LabelStyle labelStyle = new LabelStyle();
                labelStyle.Uppercase = major;
                XFont font = major ? styles.macroNames.Font : styles.macroNames.SmallFont;
                solidBrush.Color = major ? styles.macroNames.textColor : styles.macroNames.textHighlightColor;
                vec.DrawName(graphics, tileRect, font, solidBrush, labelStyle);
            }

            foreach (var vec in riftFiles
                .Select(file => resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                .OfType<VectorObject>()
                .Where(vec => (vec.MapOptions & options & MapOptions.NamesMask) != 0))
            {
                bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                LabelStyle labelStyle = new LabelStyle();
                labelStyle.Rotation = 35;
                labelStyle.Uppercase = major;
                XFont font = major ? styles.macroNames.Font : styles.macroNames.SmallFont;
                solidBrush.Color = major ? styles.macroNames.textColor : styles.macroNames.textHighlightColor;
                vec.DrawName(graphics, tileRect, font, solidBrush, labelStyle);
            }

            if (styles.macroRoutes.visible)
            {
                foreach (var vec in routeFiles
                    .Select(file => resourceManager.GetXmlFileObject(file, typeof(VectorObject)))
                    .OfType<VectorObject>()
                    .Where(vec => (vec.MapOptions & options & MapOptions.NamesMask) != 0))
                {
                    bool major = vec.MapOptions.HasFlag(MapOptions.NamesMajor);
                    LabelStyle labelStyle = new LabelStyle();
                    labelStyle.Uppercase = major;
                    XFont font = major ? styles.macroNames.Font : styles.macroNames.SmallFont;
                    solidBrush.Color = major ? styles.macroRoutes.textColor : styles.macroRoutes.textHighlightColor;
                    vec.DrawName(graphics, tileRect, font, solidBrush, labelStyle);
                }
            }

            if (options.HasFlag(MapOptions.NamesMinor))
            {
                XFont font = styles.macroNames.MediumFont;
                solidBrush.Color = styles.macroRoutes.textHighlightColor;
                foreach (var label in labels)
                {
                    using (RenderUtil.SaveState(graphics))
                    {
                        XMatrix matrix = new XMatrix();
                        matrix.ScalePrepend(1.0f / Astrometrics.ParsecScaleX, 1.0f / Astrometrics.ParsecScaleY);
                        matrix.TranslatePrepend(label.position.X, label.position.Y);
                        graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                        XSize size = graphics.MeasureString(label.text, font);
                        graphics.TranslateTransform(-size.Width / 2, -size.Height / 2); // Center the text
                        RectangleF textBounds = new RectangleF(0, 0, (float)size.Width, (float)size.Height * 2); // *2 or it gets cut off at high sizes
                        XTextFormatter formatter = new XTextFormatter(graphics);
                        formatter.Alignment = XParagraphAlignment.Center;
                        formatter.DrawString(label.text, font, solidBrush, textBounds);
                    }
                }

            }
        }
        
        private void DrawParsecGrid()
        {
            const int parsecSlop = 1;

            int hx = (int)Math.Floor(tileRect.Left);
            int hw = (int)Math.Ceiling(tileRect.Width);
            int hy = (int)Math.Floor(tileRect.Top);
            int hh = (int)Math.Ceiling(tileRect.Height);

            styles.parsecGrid.pen.Apply(ref pen);

            switch (styles.hexStyle)
            {
                case HexStyle.Square:
                    for (int px = hx - parsecSlop; px < hx + hw + parsecSlop; px++)
                    {
                        float yOffset = ((px % 2) != 0) ? 0.0f : 0.5f;
                        for (int py = hy - parsecSlop; py < hy + hh + parsecSlop; py++)
                        {
                            // TODO: use RenderUtil.(Square|Hex)Edges(X|Y) arrays
                            const float inset = 0.1f;
                            graphics.DrawRectangle(pen, px + inset, py + inset + yOffset, 1 - inset * 2, 1 - inset * 2);
                        }
                    }
                    break;

                case HexStyle.Hex:
                    XPoint[] points = new XPoint[4];
                    for (int px = hx - parsecSlop; px < hx + hw + parsecSlop; px++)
                    {
                        double yOffset = ((px % 2) != 0) ? 0.0 : 0.5;
                        for (int py = hy - parsecSlop; py < hy + hh + parsecSlop; py++)
                        {
                            points[0] = new XPoint(px + -RenderUtil.HEX_EDGE, py + 0.5 + yOffset);
                            points[1] = new XPoint(px + RenderUtil.HEX_EDGE, py + 1.0 + yOffset);
                            points[2] = new XPoint(px + 1.0 - RenderUtil.HEX_EDGE, py + 1.0 + yOffset);
                            points[3] = new XPoint(px + 1.0 + RenderUtil.HEX_EDGE, py + 0.5 + yOffset);
                            graphics.DrawLines(pen, points);
                        }
                    }
                    break;
                case HexStyle.None:
                    // none
                    break;
            }

            if (styles.numberAllHexes &&
                styles.worldDetails.HasFlag(WorldDetails.Hex))
            {
                solidBrush.Color = styles.hexNumber.textColor;
                for (int px = hx - parsecSlop; px < hx + hw + parsecSlop; px++)
                {
                    double yOffset = ((px % 2) != 0) ? 0.0 : 0.5;
                    for (int py = hy - parsecSlop; py < hy + hh + parsecSlop; py++)
                    {
                        Location loc = Astrometrics.CoordinatesToLocation(px + 1, py + 1);
                        string hex;
                        switch (styles.hexCoordinateStyle)
                        {
                            default:
                            case Stylesheet.HexCoordinateStyle.Sector: hex = loc.HexString; break;
                            case Stylesheet.HexCoordinateStyle.Subsector: hex = loc.SubsectorHexString; break;
                        }
                        using (RenderUtil.SaveState(graphics))
                        {
                            XMatrix matrix = new XMatrix();
                            matrix.TranslatePrepend(px + 0.5f, py + yOffset);
                            matrix.ScalePrepend(styles.hexContentScale / Astrometrics.ParsecScaleX, styles.hexContentScale / Astrometrics.ParsecScaleY);
                            graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);
                            graphics.DrawString(hex, styles.hexNumber.Font, solidBrush, 0, 0, RenderUtil.StringFormatTopCenter);
                        }
                    }
                }
            }
        }

        private void DrawPseudoRandomStars()
        {
            // Render pseudorandom stars based on the tile # and
            // scale factor. Note that these are positioned in
            // screen space, not world space.

            //const int nStars = 75;
            int nMinStars = tileSize.Width * tileSize.Height / 300;
            int nStars = scale >= 1 ? nMinStars : (int)(nMinStars / scale);

            // NOTE: For performance's sake, three different cases are considered:
            // (1) Tile is entirely within charted space (most common) - just render
            //     the pseudorandom stars into the tile
            // (2) Tile intersects the galaxy bounds - render pseudorandom stars
            //     into a texture, then fill the galaxy vector with it
            // (3) Tile is entire outside the galaxy - don't render stars

            using (RenderUtil.SaveState(graphics))
            {
                graphics.SmoothingMode = XSmoothingMode.HighQuality;
                solidBrush.Color = styles.pseudoRandomStars.fillColor;

                Random rand = new Random((((int)tileRect.Left) << 8) ^ (int)tileRect.Top);
                for (int i = 0; i < nStars; i++)
                {
                    float starX = (float)rand.NextDouble() * tileRect.Width + tileRect.X;
                    float starY = (float)rand.NextDouble() * tileRect.Height + tileRect.Y;
                    float d = (float)rand.NextDouble() * 2;

                    //graphics.DrawRectangle( fonts.foregroundBrush, starX, starY, (float)( d / scale * Astrometrics.ParsecScaleX ), (float)( d / scale * Astrometrics.ParsecScaleY ) );
                    graphics.DrawEllipse(solidBrush, starX, starY, (float)(d / scale * Astrometrics.ParsecScaleX), (float)(d / scale * Astrometrics.ParsecScaleY));
                }
            }
        }

        private void DrawNebulaBackground()
        {
            // Render in image-space so it scales/tiles nicely
            using (RenderUtil.SaveState(graphics))
            {
                graphics.MultiplyTransform(worldSpaceToImageSpace);

                lock (s_nebulaImage)
                {
                    const float backgroundImageScale = 2.0f;

                    // Scaled size of the background
                    double w = s_nebulaImage.PixelWidth * backgroundImageScale;
                    double h = s_nebulaImage.PixelHeight * backgroundImageScale;

                    // Offset of the background, relative to the canvas
                    double ox = (float)(-tileRect.Left * scale * Astrometrics.ParsecScaleX) % w;
                    double oy = (float)(-tileRect.Top * scale * Astrometrics.ParsecScaleY) % h;
                    if (ox > 0) ox -= w;
                    if (oy > 0) oy -= h;

                    // Number of copies needed to cover the canvas
                    int nx = 1 + (int)Math.Floor(tileSize.Width / w);
                    int ny = 1 + (int)Math.Floor(tileSize.Height / h);
                    if (ox + nx * w < tileSize.Width) nx += 1;
                    if (oy + ny * h < tileSize.Height) ny += 1;

                    for (int x = 0; x < nx; ++x)
                    {
                        for (int y = 0; y < ny; ++y)
                        {
                            graphics.DrawImage(s_nebulaImage, ox + x * w, oy + y * h, w + 1, h + 1);
                        }
                    }
                }
            }
        }

        private enum WorldLayer { Background, Foreground, Overlay };
        private void DrawWorld(FontCache styleRes, World world, WorldLayer layer)
        {
            bool isPlaceholder = world.IsPlaceholder;
            bool isCapital = world.IsCapital;
            bool isHiPop = world.IsHi;
            bool renderName = styles.worldDetails.HasFlag(WorldDetails.AllNames) ||
                (styles.worldDetails.HasFlag(WorldDetails.KeyNames) && (isCapital || isHiPop));
            bool renderUWP = styles.worldDetails.HasFlag(WorldDetails.Uwp);

            using (RenderUtil.SaveState(graphics))
            {
                XPen pen = new XPen(XColor.Empty);
                XSolidBrush solidBrush = new XSolidBrush();

                graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                // Center on the parsec
                PointF center = Astrometrics.HexToCenter(world.Coordinates);

                XMatrix matrix = new XMatrix();
                matrix.TranslatePrepend(center.X, center.Y);
                matrix.ScalePrepend(styles.hexContentScale / Astrometrics.ParsecScaleX, styles.hexContentScale / Astrometrics.ParsecScaleY);
                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                if (layer == WorldLayer.Overlay)
                {
#region Population Overlay 
                    if (styles.showPopulationOverlay && world.Population > 0)
                    {
                        // TODO: Don't hardcode the color
                        solidBrush.Color = XColor.FromArgb(0x80ffff00);
                        float r = (float)Math.Sqrt(world.Population / Math.PI) * 0.00002f;
                        graphics.DrawEllipse(solidBrush, -r, -r, r * 2, r * 2);
                    }
#endregion

#region Importance Overlay
                    if (styles.showImportanceOverlay)
                    {
                        int im = SecondSurvey.Importance(world);
                        if (im > 0)
                        {
                            // TODO: Don't hardcode the color
                            solidBrush.Color = XColor.FromArgb(0x2080ff00);
                            float r = (im - 0.5f) * Astrometrics.ParsecScaleX;
                            graphics.DrawEllipse(solidBrush, -r, -r, r * 2, r * 2);
                        }
                    }
#endregion
                }

                if (!styles.useWorldImages)
                {
                    if (layer == WorldLayer.Background)
                    {
#region Zone
                        if (styles.worldDetails.HasFlag(WorldDetails.Zone))
                        {
                            Stylesheet.StyleElement? maybeElem = ZoneStyle(world);
                            if (maybeElem.HasValue)
                            {
                                Stylesheet.StyleElement elem = maybeElem.Value;
                                if (!elem.fillColor.IsEmpty)
                                {
                                    solidBrush.Color = elem.fillColor;
                                    graphics.DrawEllipse(solidBrush, -0.4f, -0.4f, 0.8f, 0.8f);
                                }

                                PenInfo pi = elem.pen;
                                if (!pi.color.IsEmpty)
                                {
                                    pi.Apply(ref pen);

                                    if (renderName && styles.fillMicroBorders)
                                    {
                                        using (RenderUtil.SaveState(graphics))
                                        {
                                            graphics.IntersectClip(new RectangleF(-.5f, -.5f, 1f, renderUWP ? 0.65f : 0.75f));
                                            graphics.DrawEllipse(pen, -0.4f, -0.4f, 0.8f, 0.8f);
                                        }
                                    }
                                    else
                                    {
                                        graphics.DrawEllipse(pen, -0.4f, -0.4f, 0.8f, 0.8f);
                                    }
                                }
                            }
                        }
#endregion

#region Hex
                        if (!styles.numberAllHexes &&
                            styles.worldDetails.HasFlag(WorldDetails.Hex))
                        {
                            string hex;
                            switch (styles.hexCoordinateStyle)
                            {
                                default:
                                case Stylesheet.HexCoordinateStyle.Sector: hex = world.Hex; break;
                                case Stylesheet.HexCoordinateStyle.Subsector: hex = world.SubsectorHex; break;
                            }
                            solidBrush.Color = styles.hexNumber.textColor;
                            graphics.DrawString(hex, styles.hexNumber.Font, solidBrush, 0.0f, -0.5f, RenderUtil.StringFormatTopCenter);
                        }
#endregion
                    }

                    if (layer == WorldLayer.Foreground)
                    {
                        Stylesheet.StyleElement? elem = ZoneStyle(world);
                        TextBackgroundStyle worldTextBackgroundStyle = (elem.HasValue && !elem.Value.fillColor.IsEmpty)
                            ? TextBackgroundStyle.None : styles.worlds.textBackgroundStyle;

#region Name
                        if (renderName)
                        {
                            string name = world.Name;
                            if ((isHiPop && styles.worldDetails.HasFlag(WorldDetails.Highlight)) || styles.worlds.textStyle.Uppercase)
                                name = name.ToUpperInvariant();

                            Color textColor = (isCapital && styles.worldDetails.HasFlag(WorldDetails.Highlight))
                                ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                            XFont font = ((isHiPop || isCapital) && styles.worldDetails.HasFlag(WorldDetails.Highlight))
                                ? styles.worlds.LargeFont : styles.worlds.Font;

                            DrawWorldLabel(worldTextBackgroundStyle, solidBrush, textColor, styles.worlds.textStyle.Translation, font, name);
                        }
#endregion

#region Allegiance
                        // TODO: Mask off background for allegiance
                        if (styles.worldDetails.HasFlag(WorldDetails.Allegiance))
                        {
                            string alleg = world.Allegiance;
                            if (!SecondSurvey.IsDefaultAllegiance(alleg))
                            {
                                if (!styles.t5AllegianceCodes && alleg.Length > 2)
                                    alleg = SecondSurvey.T5AllegianceCodeToLegacyCode(alleg);

                                solidBrush.Color = styles.worlds.textColor;

                                if (styles.lowerCaseAllegiance)
                                    alleg = alleg.ToLowerInvariant();

                                graphics.DrawString(alleg, styles.worlds.SmallFont, solidBrush, styles.AllegiancePosition.X, styles.AllegiancePosition.Y, RenderUtil.StringFormatCentered);
                            }
                        }
#endregion

                        if (!isPlaceholder)
                        {
#region GasGiant
                            if (styles.worldDetails.HasFlag(WorldDetails.GasGiant))
                            {
                                if (world.GasGiants > 0)
                                {
                                    solidBrush.Color = styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(graphics, Glyph.Circle, styleRes, solidBrush, styles.GasGiantPosition.X, styles.GasGiantPosition.Y);
                                }
                            }
#endregion

#region Starport
                            if (styles.worldDetails.HasFlag(WorldDetails.Starport))
                            {
                                string starport = world.Starport.ToString();
                                DrawWorldLabel(worldTextBackgroundStyle, solidBrush, styles.worlds.textColor, styles.StarportPosition, styleRes.StarportFont, starport);
                            }
#endregion

#region UWP
                            if (renderUWP)
                            {
                                string uwp = world.UWP;
                                solidBrush.Color = styles.worlds.textColor;

                                graphics.DrawString(uwp, styles.hexNumber.Font, solidBrush, styles.StarportPosition.X, -styles.StarportPosition.Y, RenderUtil.StringFormatCentered);
                            }
#endregion

#region Bases
                            // TODO: Mask off background for glyphs
                            if (styles.worldDetails.HasFlag(WorldDetails.Bases))
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
                                        PointF pt = styles.BaseTopPosition;
                                        if (glyph.Bias == Glyph.GlyphBias.Bottom)
                                        {
                                            pt = styles.BaseBottomPosition;
                                            bottomUsed = true;
                                        }

                                        solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                        RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, pt.X, pt.Y);
                                    }
                                }

                                // Base 2
                                if (bases.Length > 1)
                                {
                                    Glyph glyph = Glyph.FromBaseCode(world.LegacyAllegiance, bases[1]);
                                    if (glyph.Printable)
                                    {
                                        PointF pt = bottomUsed ? styles.BaseTopPosition : styles.BaseBottomPosition;
                                        solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                        RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, pt.X, pt.Y);
                                    }
                                }

                                // Research Stations
                                string rs;
                                if ((rs = world.ResearchStation) != null)
                                {
                                    Glyph glyph = Glyph.FromResearchCode(rs);
                                    solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, styles.BaseMiddlePosition.X, styles.BaseMiddlePosition.Y);
                                }
                                else if (world.IsReserve)
                                {
                                    Glyph glyph = Glyph.Reserve;
                                    solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, styles.BaseMiddlePosition.X, 0);
                                }
                                else if (world.IsPenalColony)
                                {
                                    Glyph glyph = Glyph.Prison;
                                    solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, styles.BaseMiddlePosition.X, 0);
                                }
                                else if (world.IsPrisonExileCamp)
                                {
                                    Glyph glyph = Glyph.ExileCamp;
                                    solidBrush.Color = glyph.IsHighlighted ? styles.worlds.textHighlightColor : styles.worlds.textColor;
                                    RenderUtil.DrawGlyph(graphics, glyph, styleRes, solidBrush, styles.BaseMiddlePosition.X, 0);
                                }
                            }
#endregion
                        }

#region Disc
                        if (styles.worldDetails.HasFlag(WorldDetails.Type))
                        {
                            if (isPlaceholder)
                            {
                                DrawWorldLabel(styles.placeholder.textBackgroundStyle, solidBrush, styles.placeholder.textColor, styles.placeholder.position, styles.placeholder.Font, styles.placeholder.content);
                            }
                            else
                            {
                                if (world.Size <= 0)
                                {
#region Asteroid-Belt
                                    if (styles.worldDetails.HasFlag(WorldDetails.Asteroids))
                                    {
                                        // Basic pattern, with probability varying per position:
                                        //   o o o
                                        //  o o o o
                                        //   o o o

                                        int[] lpx = { -2, 0, 2, -3, -1, 1, 3, -2, 0, 2 };
                                        int[] lpy = { -2, -2, -2, 0, 0, 0, 0, 2, 2, 2 };
                                        float[] lpr = { 0.5f, 0.9f, 0.5f, 0.6f, 0.9f, 0.9f, 0.6f, 0.5f, 0.9f, 0.5f };

                                        solidBrush.Color = styles.worlds.textColor;

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

                                                graphics.DrawEllipse(solidBrush,
                                                    px + dx - w / 2, py + dy - h / 2, w, h);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Just a glyph
                                        solidBrush.Color = styles.worlds.textColor;
                                        RenderUtil.DrawGlyph(graphics, Glyph.DiamondX, styleRes, solidBrush, 0.0f, 0.0f);
                                    }
#endregion
                                }
                                else
                                {
                                    XColor penColor, brushColor;
                                    styles.WorldColors(world, out penColor, out brushColor);

                                    if (!brushColor.IsEmpty)
                                    {
                                        solidBrush.Color = brushColor;
                                        graphics.DrawEllipse(solidBrush, -0.1f, -0.1f, 0.2f, 0.2f);
                                    }

                                    if (!penColor.IsEmpty)
                                    {
                                        styles.worldWater.pen.Apply(ref pen);
                                        pen.Color = penColor;
                                        graphics.DrawEllipse(pen, -0.1f, -0.1f, 0.2f, 0.2f);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Dotmap
                            solidBrush.Color = styles.worlds.textColor;
                            graphics.DrawEllipse(solidBrush, -0.2f, -0.2f, 0.4f, 0.4f);
                        }
#endregion
                    }
                }
                else // styles.useWorldImages
                {
                    float imageRadius = ((world.Size <= 0) ? 0.6f : (0.3f * (world.Size / 5.0f + 0.2f))) / 2;
                    float decorationRadius = imageRadius;

                    if (layer == WorldLayer.Background)
                    {
#region Disc
                        if (styles.worldDetails.HasFlag(WorldDetails.Type))
                        {
                            if (isPlaceholder)
                            {
                                DrawWorldLabel(styles.placeholder.textBackgroundStyle, solidBrush, styles.placeholder.textColor, styles.placeholder.position, styles.placeholder.Font, styles.placeholder.content);
                            }
                            else if (world.Size <= 0)
                            {
                                const float scaleX = 1.5f;
                                const float scaleY = 1.0f;
                                XImage img = s_worldImages["Belt"];

                                lock (img)
                                {
                                    graphics.DrawImage(img, -imageRadius * scaleX, -imageRadius * scaleY, imageRadius * 2 * scaleX, imageRadius * 2 * scaleY);
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
                                        graphics.DrawImage(img, -imageRadius, -imageRadius, imageRadius * 2, imageRadius * 2);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Dotmap
                            solidBrush.Color = styles.worlds.textColor;
                            graphics.DrawEllipse(solidBrush, -0.2f, -0.2f, 0.4f, 0.4f);
                        }
#endregion
                    }

                    if (isPlaceholder)
                        return;

                    if (layer == WorldLayer.Foreground)
                    {
#region Zone
                        if (styles.worldDetails.HasFlag(WorldDetails.Zone))
                        {
                            if (world.IsAmber || world.IsRed || world.IsBlue)
                            {
                                PenInfo pi =
                                    world.IsAmber ? styles.amberZone.pen :
                                    world.IsRed ? styles.redZone.pen : styles.blueZone.pen;
                                pi.Apply(ref pen);

                                // TODO: Try and accomplish this using dash pattern
                                decorationRadius += 0.1f;
                                graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 5, 80);
                                graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 95, 80);
                                graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 185, 80);
                                graphics.DrawArc(pen, -decorationRadius, -decorationRadius, decorationRadius * 2, decorationRadius * 2, 275, 80);
                            }
                        }
#endregion

#region GasGiant
                        if (styles.worldDetails.HasFlag(WorldDetails.GasGiant))
                        {
                            if (world.GasGiants > 0)
                            {
                                decorationRadius += 0.1f;
                                const float symbolRadius = 0.05f;
                                solidBrush.Color = styles.worlds.textHighlightColor; ;
                                graphics.DrawEllipse(solidBrush, decorationRadius - symbolRadius, 0.0f - symbolRadius, symbolRadius * 2, symbolRadius * 2);
                            }
                        }
#endregion

#region UWP
                        if (renderUWP)
                        {
                            string uwp = world.UWP;
                            solidBrush.Color = styles.worlds.textColor;

                            using (RenderUtil.SaveState(graphics))
                            {
                                XMatrix uwpMatrix = new XMatrix();
                                uwpMatrix.TranslatePrepend(decorationRadius, 0.0f);
                                uwpMatrix.ScalePrepend(styles.worlds.textStyle.Scale.Width, styles.worlds.textStyle.Scale.Height);
                                uwpMatrix.Multiply(uwpMatrix, XMatrixOrder.Prepend);
                                graphics.DrawString(uwp, styles.hexNumber.Font, solidBrush, styles.StarportPosition.X, -styles.StarportPosition.Y, RenderUtil.StringFormatCenterLeft);
                            }
                        }
#endregion

#region Name
                        if (renderName)
                        {
                            string name = world.Name;
                            if (isHiPop)
                                name = name.ToUpperInvariant();

                            using (RenderUtil.SaveState(graphics))
                            {
                                Color textColor = (isCapital && styles.worldDetails.HasFlag(WorldDetails.Highlight))
                                    ? styles.worlds.textHighlightColor : styles.worlds.textColor;

                                if (styles.worlds.textStyle.Uppercase)
                                    name = name.ToUpper();

                                decorationRadius += 0.1f;
                                XMatrix imageMatrix = new XMatrix();
                                imageMatrix.TranslatePrepend(decorationRadius, 0.0f);
                                imageMatrix.ScalePrepend(styles.worlds.textStyle.Scale.Width, styles.worlds.textStyle.Scale.Height);
                                imageMatrix.TranslatePrepend(graphics.MeasureString(name, styles.worlds.Font).Width / 2, 0.0f); // Left align
                                graphics.MultiplyTransform(imageMatrix, XMatrixOrder.Prepend);

                                DrawWorldLabel(styles.worlds.textBackgroundStyle, solidBrush, textColor, styles.worlds.textStyle.Translation, styles.worlds.Font, name);
                            }
                        }
#endregion
                    }
                }
            }
        }

        private static readonly Regex STELLAR_REGEX = new Regex(@"([OBAFGKM][0-9] ?(?:Ia|Ib|II|III|IV|V|VI|VII|D)|D|BD|BH)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private void DrawStars(World world)
        {
            using (RenderUtil.SaveState(graphics))
            {
                graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                PointF center = Astrometrics.HexToCenter(world.Coordinates);

                XMatrix matrix = new XMatrix();
                matrix.TranslatePrepend(center.X, center.Y);
                matrix.ScalePrepend(styles.hexContentScale / Astrometrics.ParsecScaleX, styles.hexContentScale / Astrometrics.ParsecScaleY);
                graphics.MultiplyTransform(matrix, XMatrixOrder.Prepend);

                // TODO: Proper stellar parsing
                List<string> ss = new List<string>();
                foreach (Match m in STELLAR_REGEX.Matches(world.Stellar))
                {
                    ss.Add(m.Value);
                }

                int i = 0;
                foreach (var props in ss.Select(s => StellarRendering.star2props(s)).OrderByDescending(p => p.radius)) {
                    solidBrush.Color = props.color;
                    pen.Color = props.borderColor;
                    pen.DashStyle = XDashStyle.Solid;
                    pen.Width = styles.worlds.pen.width;
                    PointF offset = StellarRendering.Offset(i++);
                    const float offsetScale = 0.3f;
                    float r = 0.15f * props.radius;
                    graphics.DrawEllipse(pen, solidBrush, offset.X * offsetScale - r, offset.Y * offsetScale - r, r*2, r*2);
                }
            }
        }


        private Stylesheet.StyleElement? ZoneStyle(World world)
        {
            if (world.IsAmber || world.IsRed || world.IsBlue)
            {
                Stylesheet.StyleElement elem =
                    world.IsAmber ? styles.amberZone :
                    world.IsRed ? styles.redZone : styles.blueZone;
                return elem;
            }
            return null;
        }

        private void DrawWorldLabel(TextBackgroundStyle backgroundStyle, XSolidBrush brush, Color color, PointF position, XFont font, string text)
        {
            XSize size = graphics.MeasureString(text, font);

            switch (backgroundStyle)
            {
                case TextBackgroundStyle.None:
                    break;

                default:
                case TextBackgroundStyle.Rectangle:
                    if (!styles.fillMicroBorders)
                    {
                        // TODO: Implement this with a clipping region instead
                        brush.Color = styles.backgroundColor;
                        graphics.DrawRectangle(brush, position.X - size.Width / 2, position.Y - size.Height / 2, size.Width, size.Height);
                    }
                    break;

                case TextBackgroundStyle.Outline:
                case TextBackgroundStyle.Shadow:
                    {
                        // TODO: These scaling factors are constant for a render; compute once

                        // Invert the current scaling transforms
                        float sx = 1.0f / styles.hexContentScale;
                        float sy = 1.0f / styles.hexContentScale;
                        sx *= Astrometrics.ParsecScaleX;
                        sy *= Astrometrics.ParsecScaleY;
                        sx /= (float)scale * Astrometrics.ParsecScaleX;
                        sy /= (float)scale * Astrometrics.ParsecScaleY;

                        const int outlineSize = 2;
                        const int outlineSkip = 1;

                        int outlineStart = backgroundStyle == TextBackgroundStyle.Outline
                            ? -outlineSize
                            : 0;

                        brush.Color = styles.backgroundColor;

                        for (int dx = outlineStart; dx <= outlineSize; dx += outlineSkip)
                        {
                            for (int dy = outlineStart; dy <= outlineSize; dy += outlineSkip)
                            {
                                graphics.DrawString(text, font, brush, position.X + sx * dx, position.Y + sy * dy, RenderUtil.StringFormatCentered);
                            }
                        }
                        break;
                    }
            }

            brush.Color = color;
            graphics.DrawString(text, font, brush, position.X, position.Y, RenderUtil.StringFormatCentered);
        }

        private static readonly Regex WRAP_REGEX = new Regex(@"\s+(?![a-z])");

        private void DrawLabels()
        {
            using (RenderUtil.SaveState(graphics))
            {
                XSolidBrush solidBrush = new XSolidBrush();

                graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                foreach (Sector sector in selector.Sectors)
                {
                    solidBrush.Color = styles.microBorders.textColor;
                    foreach (Border border in sector.Borders.Where(border => border.ShowLabel))
                    {
                        string label = border.GetLabel(sector);
                        if (label == null)
                            continue;
                        Hex labelHex = border.LabelPosition;
                        PointF labelPos = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, labelHex)));
                        // TODO: Replace these with, well, positions!
                        //labelPos.X -= 0.5f;
                        //labelPos.Y -= 0.5f;

                        if (border.WrapLabel)
                            label = WRAP_REGEX.Replace(label, "\n");

                        RenderUtil.DrawLabel(graphics, label, labelPos, styles.microBorders.Font, solidBrush, styles.microBorders.textStyle);
                    }

                    foreach (Region region in sector.Regions.Where(region => region.ShowLabel))
                    {
                        string label = region.GetLabel(sector);
                        if (label == null)
                            continue;
                        Hex labelHex = region.LabelPosition;
                        PointF labelPos = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, labelHex)));
                        // TODO: Replace these with, well, positions!
                        //labelPos.X -= 0.5f;
                        //labelPos.Y -= 0.5f;

                        if (region.WrapLabel)
                            label = WRAP_REGEX.Replace(label, "\n");

                        RenderUtil.DrawLabel(graphics, label, labelPos, styles.microBorders.Font, solidBrush, styles.microBorders.textStyle);
                    }

                    foreach (Label label in sector.Labels)
                    {
                        string text = label.Text;
                        Hex labelHex = new Hex(label.Hex);
                        PointF labelPos = Astrometrics.HexToCenter(Astrometrics.LocationToCoordinates(new Location(sector.Location, labelHex)));
                        // TODO: Adopt some of the tweaks from .MSEC
                        labelPos.Y -= label.OffsetY * 0.7f;

                        XFont font;
                        switch (label.Size)
                        {
                            case "small": font = styles.microBorders.SmallFont; break;
                            case "large": font = styles.microBorders.LargeFont; break;
                            default: font = styles.microBorders.Font; break;
                        }

                        if (!styles.grayscale &&
                            ColorUtil.NoticeableDifference(label.Color, styles.backgroundColor) &&
                            (label.Color != Label.DefaultColor))
                            solidBrush.Color = label.Color;
                        else
                            solidBrush.Color = styles.microBorders.textColor;
                        RenderUtil.DrawLabel(graphics, text, labelPos, font, solidBrush, styles.microBorders.textStyle);
                    }
                }
            }
        }

        private void DrawRoutes()
        {
            using (RenderUtil.SaveState(graphics))
            {
                graphics.SmoothingMode = XSmoothingMode.AntiAlias;
                XPen pen = new XPen(XColor.Empty);
                styles.microRoutes.pen.Apply(ref pen);
                float baseWidth = styles.microRoutes.pen.width;

                foreach (Sector sector in selector.Sectors)
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

                        // Shorten line to leave room for world glyph
                        OffsetSegment(ref startPoint, ref endPoint, 0.25f);

                        float? routeWidth = route.Width;
                        Color? routeColor = route.Color;
                        LineStyle? routeStyle = styles.overrideLineStyle ?? route.Style;

                        SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("route", route.Allegiance ?? route.Type ?? "Im");
                        routeStyle = routeStyle ?? ssr.GetEnum<LineStyle>("style");
                        routeColor = routeColor ?? ssr.GetColor("color");
                        routeWidth = routeWidth ?? (float?)ssr.GetNumber("width") ?? 1.0f;

                        // In grayscale, convert default color and style to non-default style
                        if (styles.grayscale && !routeColor.HasValue && !routeStyle.HasValue)
                            routeStyle = LineStyle.Dashed;

                        routeColor = routeColor ?? styles.microRoutes.pen.color;
                        routeStyle = routeStyle ?? LineStyle.Solid;

                        // Ensure color is visible
                        if (styles.grayscale || !ColorUtil.NoticeableDifference(routeColor.Value, styles.backgroundColor))
                            routeColor = styles.microRoutes.pen.color; // default

                        if (routeStyle.Value == LineStyle.None)
                            continue;

                        pen.Color = routeColor.Value;
                        pen.Width = routeWidth.Value * baseWidth;
                        pen.DashStyle = LineStyleToDashStyle(routeStyle.Value);

                        graphics.DrawLine(pen, startPoint, endPoint);
                    }
                }
            }
        }

        private static void OffsetSegment(ref PointF startPoint, ref PointF endPoint, float offset)
        {
            float dx = endPoint.X - startPoint.X;
            float dy = endPoint.Y - startPoint.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            float ddx = dx * offset / length;
            float ddy = dy * offset / length;
            startPoint.X += ddx;
            startPoint.Y += ddy;
            endPoint.X -= ddx;
            endPoint.Y -= ddy;
        }

        private static XDashStyle LineStyleToDashStyle(LineStyle style)
        {
            switch (style)
            {
                default:
                case LineStyle.Solid: return XDashStyle.Solid;
                case LineStyle.Dashed: return XDashStyle.Dash;
                case LineStyle.Dotted: return XDashStyle.Dot;
                case LineStyle.None: throw new ApplicationException("LineStyle.None should be detected earlier");
            }
        }

        private enum BorderLayer { Fill, Stroke };
        private void DrawMicroBorders(BorderLayer layer)
        {
            const byte FILL_ALPHA = 64;

            float[] edgex, edgey;
            PathUtil.PathType borderPathType = styles.microBorderStyle == MicroBorderStyle.Square ?
                PathUtil.PathType.Square : PathUtil.PathType.Hex;
            RenderUtil.HexEdges(borderPathType, out edgex, out edgey);

            XSolidBrush solidBrush = new XSolidBrush();
            XPen pen = new XPen(XColor.Empty);
            styles.microBorders.pen.Apply(ref pen);

            foreach (Sector sector in selector.Sectors)
            {
                XGraphicsPath sectorClipPath = null;

                using (RenderUtil.SaveState(graphics))
                {
                    // This looks craptacular for Candy style borders :(
                    if (ClipOutsectorBorders &&
                        (layer == BorderLayer.Fill || styles.microBorderStyle != MicroBorderStyle.Curve))
                    {
                        Sector.ClipPath clip = sector.ComputeClipPath(borderPathType);
                        if (!tileRect.IntersectsWith(clip.bounds))
                            continue;

                        sectorClipPath = new XGraphicsPath(clip.clipPathPoints, clip.clipPathPointTypes, XFillMode.Alternate);
                        if (sectorClipPath != null)
                            graphics.IntersectClip(sectorClipPath);
                    }

                    graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                    foreach (Border border in sector.Borders)
                    {
                        BorderPath borderPath = border.ComputeGraphicsPath(sector, borderPathType);

                        XGraphicsPath drawPath = new XGraphicsPath(borderPath.points, borderPath.types, XFillMode.Alternate);

                        Color? borderColor = border.Color;
                        LineStyle? borderStyle = border.Style;

                        SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("border", border.Allegiance);
                        borderStyle = borderStyle ?? ssr.GetEnum<LineStyle>("style") ?? LineStyle.Solid;
                        borderColor = borderColor ?? ssr.GetColor("color") ?? styles.microBorders.pen.color;

                        if (layer == BorderLayer.Stroke && borderStyle.Value == LineStyle.None)
                            continue;

                        if (styles.grayscale ||
                            !ColorUtil.NoticeableDifference(borderColor.Value, styles.backgroundColor))
                        {
                            borderColor = styles.microBorders.pen.color; // default
                        }

                        pen.Color = borderColor.Value;
                        pen.DashStyle = LineStyleToDashStyle(borderStyle.Value);

                        if (styles.microBorderStyle != MicroBorderStyle.Curve)
                        {
                            // Clip to the path itself - this means adjacent borders don't clash
                            using (RenderUtil.SaveState(graphics))
                            {
                                graphics.IntersectClip(drawPath);
                                switch (layer)
                                {
                                    case BorderLayer.Fill:
                                        solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                        graphics.DrawPath(solidBrush, drawPath);
                                        break;
                                    case BorderLayer.Stroke:
                                        graphics.DrawPath(pen, drawPath);
                                        break;
                                }
                            }
                        }
                        else
                        {
                            switch (layer)
                            {
                                case BorderLayer.Fill:
                                    solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                    graphics.DrawClosedCurve(solidBrush, borderPath.points);
                                    break;

                                case BorderLayer.Stroke:
                                    foreach (var segment in borderPath.curves)
                                    {
                                        if (segment.closed)
                                            graphics.DrawClosedCurve(pen, segment.points, 0.6f);
                                        else
                                            graphics.DrawCurve(pen, segment.points, 0.6f);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draw the regions
        /// </summary>
        /// <param name="layer">The layer</param>
        private void DrawRegions(BorderLayer layer)
        {
            const byte FILL_ALPHA = 64;

            float[] edgex, edgey;
            PathUtil.PathType borderPathType = styles.microBorderStyle == MicroBorderStyle.Square ?
                PathUtil.PathType.Square : PathUtil.PathType.Hex;
            RenderUtil.HexEdges(borderPathType, out edgex, out edgey);

            XSolidBrush solidBrush = new XSolidBrush();
            XPen pen = new XPen(XColor.Empty);
            styles.microBorders.pen.Apply(ref pen);

            foreach (Sector sector in selector.Sectors)
            {
                XGraphicsPath sectorClipPath = null;

                using (RenderUtil.SaveState(graphics))
                {
                    // This looks craptacular for Candy style borders :(
                    if (ClipOutsectorBorders &&
                        (layer == BorderLayer.Fill || styles.microBorderStyle != MicroBorderStyle.Curve))
                    {
                        Sector.ClipPath clip = sector.ComputeClipPath(borderPathType);
                        if (!tileRect.IntersectsWith(clip.bounds))
                            continue;

                        sectorClipPath = new XGraphicsPath(clip.clipPathPoints, clip.clipPathPointTypes, XFillMode.Alternate);
                        if (sectorClipPath != null)
                            graphics.IntersectClip(sectorClipPath);
                    }

                    graphics.SmoothingMode = XSmoothingMode.AntiAlias;

                    foreach (Region region in sector.Regions)
                    {
                        BorderPath borderPath = region.ComputeGraphicsPath(sector, borderPathType);

                        XGraphicsPath drawPath = new XGraphicsPath(borderPath.points, borderPath.types, XFillMode.Alternate);

                        Color? borderColor = region.Color;
                        LineStyle? borderStyle = region.Style;

                        SectorStylesheet.StyleResult ssr = sector.ApplyStylesheet("border", region.Allegiance);
                        borderStyle = borderStyle ?? ssr.GetEnum<LineStyle>("style") ?? LineStyle.Solid;
                        borderColor = borderColor ?? ssr.GetColor("color") ?? styles.microBorders.pen.color;

                        if (layer == BorderLayer.Stroke && borderStyle.Value == LineStyle.None)
                            continue;

                        if (styles.grayscale ||
                            !ColorUtil.NoticeableDifference(borderColor.Value, styles.backgroundColor))
                        {
                            borderColor = styles.microBorders.pen.color; // default
                        }

                        pen.Color = borderColor.Value;
                        pen.DashStyle = LineStyleToDashStyle(borderStyle.Value);

                        if (styles.microBorderStyle != MicroBorderStyle.Curve)
                        {
                            // Clip to the path itself - this means adjacent borders don't clash
                            using (RenderUtil.SaveState(graphics))
                            {
                                graphics.IntersectClip(drawPath);
                                switch (layer)
                                {
                                    case BorderLayer.Fill:
                                        solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                        graphics.DrawPath(solidBrush, drawPath);
                                        break;
                                    case BorderLayer.Stroke:
                                        graphics.DrawPath(pen, drawPath);
                                        break;
                                }
                            }
                        }
                        else
                        {
                            switch (layer)
                            {
                                case BorderLayer.Fill:
                                    solidBrush.Color = Color.FromArgb(FILL_ALPHA, borderColor.Value);
                                    graphics.DrawClosedCurve(solidBrush, borderPath.points);
                                    break;

                                case BorderLayer.Stroke:
                                    foreach (var segment in borderPath.curves)
                                    {
                                        if (segment.closed)
                                            graphics.DrawClosedCurve(pen, segment.points, 0.6f);
                                        else
                                            graphics.DrawCurve(pen, segment.points, 0.6f);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }
    }
}
