using PdfSharp.Drawing;
using System;
using System.Drawing;

namespace Maps.Rendering
{
    [Flags]
    public enum MapOptions : int
    {
        SectorGrid = 0x0001,
        SubsectorGrid = 0x0002,

        SectorsSelected = 0x0004,
        SectorsAll = 0x0008,
        SectorsMask = SectorsSelected | SectorsAll,

        BordersMajor = 0x0010,
        BordersMinor = 0x0020,
        BordersMask = BordersMajor | BordersMinor,

        NamesMajor = 0x0040,
        NamesMinor = 0x0080,
        NamesMask = NamesMajor | NamesMinor,

        WorldsCapitals = 0x0100,
        WorldsHomeworlds = 0x0200,
        WorldsMask = WorldsCapitals | WorldsHomeworlds,

        RoutesSelectedDeprecated = 0x0400,

        PrintStyleDeprecated = 0x0800,
        CandyStyleDeprecated = 0x1000,
        StyleMaskDeprecated = PrintStyleDeprecated | CandyStyleDeprecated,

        ForceHexes = 0x2000,
        WorldColors = 0x4000,
        FilledBorders = 0x8000
    };

    [Flags]
    public enum WorldDetails : int
    {
        None = 0x0000,

        Type = 0x0001,
        KeyNames = 0x0002,
        Starport = 0x0004,
        GasGiant = 0x0008,
        Allegiance = 0x0010,
        Bases = 0x0020,
        Hex = 0x0040,
        Zone = 0x0080,
        AllNames = 0x0100,
        Uwp = 0x0200,
        Asteroids = 0x0400,

        Dotmap = None,
        Atlas = Type | KeyNames | Starport | GasGiant | Allegiance | Bases | Zone,
        Poster = Type | KeyNames | Starport | GasGiant | Allegiance | Bases | Hex | Zone | AllNames | Asteroids,
    }

    public enum TextBackgroundStyle
    {
        None,
        Rectangle,
        Shadow,
        Outline
    }

    public struct FontInfo
    {
        public FontFamily family;
        public string name;
        public float size;
        public XFontStyle style;

        public FontInfo(string name, float size, XFontStyle style = XFontStyle.Regular)
        {
            this.family = null;
            this.name = name;
            this.size = size;
            this.style = style;
        }

        public FontInfo(FontFamily family, float size, XFontStyle style = XFontStyle.Regular)
        {
            this.family = family;
            this.name = null;
            this.size = size;
            this.style = style;
        }

        private static XPdfFontOptions s_fontOptions = new XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode);
        public XFont makeFont()
        {
            if (this.family != null)
            {
                return new XFont(this.family, this.size * 1.4f, this.style, s_fontOptions);
            }
            else if (this.name != null)
            {
                return new XFont(this.name, this.size * 1.4f, this.style, s_fontOptions);
            }
            else
            {
                return null;
            }
        }
    }

    public struct PenInfo
    {
        public Color color;
        public float width;
        public XDashStyle dashStyle;
        public double[] dashPattern;
        //public float scaleX;
        //public float scaleY;

        public PenInfo(Color color, float width)
        {
            this.color = color;
            this.width = width;
            this.dashStyle = XDashStyle.Solid;
            this.dashPattern = null;
            //this.scaleX = 0;
            //this.scaleY = 0;
        }

        public XDashStyle DashStyle
        {
            get { return dashStyle; }
            set { dashStyle = value; }
        }

        public double[] DashPattern
        {
            get { return dashPattern; }
            set { dashPattern = value; }
        }

        //public void ScaleTransform( float x, float y )
        //{
        //    this.scaleX = x;
        //    this.scaleY = y;
        //}

        public void Apply(ref XPen pen)
        {
            pen.Color = this.color;
            pen.Width = this.width;
            pen.DashStyle = this.dashStyle;

            if (this.dashPattern != null)
            {
                pen.DashPattern = this.dashPattern;
            }
        }


    }

    public struct LabelStyle
    {
        public float Rotation { get; set; }
        public SizeF Scale { get; set; }
        public PointF Translation { get; set; }
        public bool Uppercase { get; set; }
        public bool Wrap { get; set; }
    }

    public enum MicroBorderStyle
    {
        Hex,
        Square,
        Curve,
    }

    public class Stylesheet
    {
        public const string DEFAULT_FONT = "Arial";

        private const float SectorGridMinScale = 1/2f; // Below this, no sector grid is shown
        private const float SectorGridFullScale = 4; // Above this, sector grid opaque
        private const float SectorNameMinScale = 1;
        private const float SectorNameAllSelectedScale = 4; // At this point, "Selected" == "All"
        private const float SectorNameMaxScale = 16;
        private const float PseudoRandomStarsMinScale = 1; // Below this, no pseudo-random stars
        private const float PseudoRandomStarsMaxScale = 4; // Above this, no pseudo-random stars
        private const float SubsectorsMinScale = 8;
        private const float SubsectorNameMinScale = 24;
        private const float SubsectorNameMaxScale = 64;
        private const float MacroWorldsMinScale = 1f / 2;
        private const float MacroWorldsMaxScale = 4;
        private const float MacroLabelMinScale = 1f / 2;
        private const float MacroLabelMaxScale = 4;
        private const float MacroRouteMinScale = 1f / 2;
        private const float MacroRouteMaxScale = 4;
        private const float MacroBorderMinScale = 1f / 32;
        private const float MicroBorderMinScale = 4;
        private const float MicroNameMinScale = 16;
        private const float RouteMinScale = 8; // Below this, routes not rendered
        private const float ParsecMinScale = 16; // Below this, parsec edges not rendered
        private const float ParsecHexMinScale = 48; // Below this, hex numbers not rendered
        private const float WorldMinScale = 4; // Below this: no worlds; above this: dotmap
        private const float WorldBasicMinScale = 24; // Above this: atlas-style abbreviated data
        private const float WorldFullMinScale = 48; // Above this: full poster-style data
        private const float WorldUwpMinScale = 96; // Above this: UWP shown above name

        private const float CandyMinWorldNameScale = 64;
        private const float CandyMinUwpScale = 256;
        private const float CandyMaxWorldRelativeScale = 512;
        private const float CandyMaxBorderRelativeScale = 32;
        private const float CandyMaxRouteRelativeScale = 32;

        public enum Style { Poster, Atlas, Candy, Print };

        public Stylesheet(double scale, MapOptions options, Style style)
        {
            grayscale = false;
            subsectorGrid.visible = ((scale >= SubsectorsMinScale) && options.HasFlag(MapOptions.SubsectorGrid));
            sectorGrid.visible = ((scale >= SectorGridMinScale) && options.HasFlag(MapOptions.SectorGrid));
            parsecGrid.visible = (scale >= ParsecMinScale);
            showSomeSectorNames = ((scale >= SectorNameMinScale) && (scale <= SectorNameMaxScale) && ((options & MapOptions.SectorsMask) != 0));
            showAllSectorNames = showSomeSectorNames && ((scale >= SectorNameAllSelectedScale) || options.HasFlag(MapOptions.SectorsAll));
            subsectorNames.visible = ((scale >= SubsectorNameMinScale) && (scale <= SubsectorNameMaxScale) && ((options & MapOptions.SectorsMask) != 0));

            worlds.visible = (scale >= WorldMinScale);
            pseudoRandomStars.visible = (PseudoRandomStarsMinScale <= scale) && (scale <= PseudoRandomStarsMaxScale);
            showRifts = (scale <= PseudoRandomStarsMaxScale || style == Style.Candy);


            float logscale = (float)Math.Log(scale, 2.0);
            riftOpacity = ScaleInterpolate(0f, 0.85f, scale, 1/4f, 4f);
            
            deepBackgroundOpacity = ScaleInterpolate(1f, 0f, scale, 1/8f, 2f);

            macroRoutes.visible = (scale >= MacroRouteMinScale) && (scale <= MacroRouteMaxScale);
            macroNames.visible = (scale >= MacroLabelMinScale) && (scale <= MacroLabelMaxScale);
            showMicroNames = ((scale >= MicroNameMinScale) && ((options & MapOptions.NamesMask) != 0));
            capitals.visible = (scale >= MacroWorldsMinScale) && (scale <= MacroWorldsMaxScale);

            microBorderStyle = (((options & MapOptions.ForceHexes) == 0) && (scale < ParsecHexMinScale))
                ? MicroBorderStyle.Square
                : MicroBorderStyle.Hex;

            macroBorders.visible = (scale >= MacroBorderMinScale) && (scale < MicroBorderMinScale) && ((options & MapOptions.BordersMask) != 0);
            microBorders.visible = (scale >= MicroBorderMinScale) && ((options & MapOptions.BordersMask) != 0);
            fillMicroBorders = microBorders.visible && options.HasFlag(MapOptions.FilledBorders);
            microRoutes.visible = (scale >= RouteMinScale);

            worldDetails = !worlds.visible ? 0 :
                (scale < WorldBasicMinScale) ? WorldDetails.Dotmap :
                (scale < WorldFullMinScale) ? WorldDetails.Atlas :
                WorldDetails.Poster;

            showWorldDetailColors = worldDetails == WorldDetails.Poster && options.HasFlag(MapOptions.WorldColors);

            lowerCaseAllegiance = (scale < WorldFullMinScale);

            worlds.textBackgroundStyle = TextBackgroundStyle.Rectangle;

            hexCoordinateStyle = HexCoordinateStyle.Sector;

            if (scale < WorldFullMinScale)
            {
                // Atlas-style

                const float x = 0.225f;
                const float y = 0.125f;

                BaseTopPosition.X = -x;
                BaseTopPosition.Y = -y;
                BaseBottomPosition.X = -x;
                BaseBottomPosition.Y = y;
                GasGiantPosition.X = x;
                GasGiantPosition.Y = -y;
                AllegiancePosition.X = x;
                AllegiancePosition.Y = y;

                BaseMiddlePosition.X = options.HasFlag(MapOptions.ForceHexes) ? -0.35f : -0.2f;
                BaseMiddlePosition.Y = 0;
                StarportPosition.X = 0.0f;
                StarportPosition.Y = -0.24f;
                worlds.position.X = 0.0f;
                worlds.position.Y = 0.4f;
            }
            else
            {
                // Poster-style

                const float x = 0.25f;
                const float y = 0.18f;

                BaseTopPosition.X = -x;
                BaseTopPosition.Y = -y;
                BaseBottomPosition.X = -x;
                BaseBottomPosition.Y = y;
                GasGiantPosition.X = x;
                GasGiantPosition.Y = -y;
                AllegiancePosition.X = x;
                AllegiancePosition.Y = y;

                BaseMiddlePosition.X = -0.35f;
                BaseMiddlePosition.Y = 0f;
                StarportPosition.X = 0f;
                StarportPosition.Y = -0.225f;
                worlds.position.X = 0.0f;
                worlds.position.Y = 0.37f; // Don't hide hex bottom, leave room for UWP
            }

            if (scale >= WorldUwpMinScale)
            {
                worldDetails |= WorldDetails.Uwp;
                BaseBottomPosition.Y = 0.1f;
                BaseMiddlePosition.Y = (BaseBottomPosition.Y + BaseTopPosition.Y) / 2;
                AllegiancePosition.Y = 0.1f;
            }

            if (worlds.visible)
            {
                float fontScale = (scale <= 96f || style == Style.Candy) ? 1f : 96f / Math.Min((float)scale, 192f);

                worlds.fontInfo = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.2f : 0.15f * fontScale, XFontStyle.Bold);
                wingdingFont = new FontInfo("Wingdings", scale < WorldFullMinScale ? 0.2f : 0.175f * fontScale);
                glyphFont = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.175f : 0.15f * fontScale, XFontStyle.Bold);
                hexNumber.fontInfo = new FontInfo(DEFAULT_FONT, 0.1f * fontScale);
                worlds.smallFontInfo = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.2f : 0.1f * fontScale, XFontStyle.Regular);
                starportFont = (scale < WorldFullMinScale) ? worlds.smallFontInfo : worlds.fontInfo;
            }

            sectorName.fontInfo = new FontInfo(DEFAULT_FONT, 5.5f);
            subsectorNames.fontInfo = new FontInfo(DEFAULT_FONT, 1.5f);

            microBorders.fontInfo = new FontInfo(DEFAULT_FONT, (scale == MicroNameMinScale) ? 0.6f : 0.25f, XFontStyle.Bold);
            microBorders.smallFontInfo = new FontInfo(DEFAULT_FONT, 0.15f, XFontStyle.Bold);
            microBorders.largeFontInfo = new FontInfo(DEFAULT_FONT, 0.75f, XFontStyle.Bold);

            macroNames.fontInfo = new FontInfo(DEFAULT_FONT, 8f / 1.4f, XFontStyle.Bold);
            macroNames.smallFontInfo = new FontInfo(DEFAULT_FONT, 5f / 1.4f, XFontStyle.Regular);
            macroNames.mediumFontInfo = new FontInfo(DEFAULT_FONT, 6.5f / 1.4f, XFontStyle.Italic);


            capitals.fillColor = Color.Wheat;
            capitals.textColor = Color.Red;
            blueZone.pen.color = Color.Blue;
            amberZone.pen.color = Color.Gold;
            redZone.pen.color = Color.Red;
            macroBorders.pen.color = Color.Red;
            macroRoutes.pen.color = Color.White;
            microBorders.pen.color = Color.Gray;
            Color gridColor = Color.FromArgb(ScaleInterpolate(0, 255, scale, SectorGridMinScale, SectorGridFullScale), Color.Gray);
            microRoutes.pen.color = Color.Green;

            Color foregroundColor = Color.White;
            backgroundColor = Color.Black;
            Color lightColor = Color.LightGray;
            Color darkColor = Color.DarkGray;
            Color dimColor = Color.DimGray;
            Color highlightColor = Color.Red;
            microBorders.textColor = Color.Gold;
            worldWater.fillColor = Color.DeepSkyBlue;
            worldNoWater.fillColor = Color.White;
            worldNoWater.pen.color = Color.Empty;

            float onePixel = 1f / (float)scale;

            parsecGrid.pen = new PenInfo(gridColor, onePixel);
            subsectorGrid.pen = new PenInfo(gridColor, onePixel * 2);
            sectorGrid.pen = new PenInfo(gridColor, (subsectorGrid.visible ? 4 : 2) * onePixel);

            microBorders.textStyle.Rotation = 0;
            microBorders.textStyle.Translation = new PointF(0, 0);
            microBorders.textStyle.Scale = new SizeF(1.0f, 1.0f);
            microBorders.textStyle.Uppercase = false;

            sectorName.textStyle.Rotation = -50; // degrees
            sectorName.textStyle.Translation = new PointF(0, 0);
            sectorName.textStyle.Scale = new SizeF(0.75f, 1.0f);
            sectorName.textStyle.Uppercase = false;
            sectorName.textStyle.Wrap = true;

            subsectorNames.textStyle = sectorName.textStyle;

            worlds.textStyle.Rotation = 0;
            worlds.textStyle.Scale = new SizeF(1.0f, 1.0f);
            worlds.textStyle.Translation = worlds.position;
            worlds.textStyle.Uppercase = false;

            useBackgroundImage = false;
            useGalaxyImage = false;
            useWorldImages = false;

            switch (style)
            {
                case Style.Poster:
                    {
                        // This is the default - no changes
                        useGalaxyImage = deepBackgroundOpacity > 0.0f;
                        break;
                    }
                case Style.Atlas:
                    {
                        deepBackgroundOpacity = 0f;

                        grayscale = true;
                        capitals.fillColor = Color.DarkGray;
                        capitals.textColor = Color.Black;
                        amberZone.pen.color = Color.LightGray;
                        blueZone.pen.color = Color.Gray; // TODO: make dashed
                        redZone.pen.color = Color.Black;
                        macroBorders.pen.color = Color.Black;
                        macroRoutes.pen.color = Color.Gray;
                        microBorders.pen.color = Color.Black;
                        microRoutes.pen.color = Color.Gray;

                        foregroundColor = Color.Black;
                        backgroundColor = Color.White;
                        lightColor = Color.DarkGray;
                        darkColor = Color.DarkGray;
                        dimColor = Color.LightGray;
                        highlightColor = Color.Gray;
                        microBorders.textColor = Color.Gray;
                        worldWater.fillColor = Color.Black;
                        worldNoWater.fillColor = Color.Empty;

                        worldNoWater.fillColor = Color.White;
                        worldNoWater.pen = new PenInfo(Color.Black, onePixel);

                        riftOpacity = Math.Min(riftOpacity, 0.70f);

                        showWorldDetailColors = false;

                        break;
                    }
                case Style.Print:
                    {
                        deepBackgroundOpacity = 0f;

                        foregroundColor = Color.Black;
                        backgroundColor = Color.White;
                        lightColor = Color.DarkGray;
                        darkColor = Color.DarkGray;
                        dimColor = Color.LightGray;
                        microRoutes.pen.color = Color.Gray;

                        microBorders.textColor = Color.Brown;

                        amberZone.pen.color = Color.Gold;
                        worldNoWater.fillColor = Color.White;
                        worldNoWater.pen = new PenInfo(Color.Black, onePixel);

                        riftOpacity = Math.Min(riftOpacity, 0.70f);

                        break;
                    }
                case Style.Candy:
                    {
                        useWorldImages = true;
                        pseudoRandomStars.visible = false;

                        useBackgroundImage = deepBackgroundOpacity < 0.5f;
                        useGalaxyImage = deepBackgroundOpacity > 0.0f;


                        microBorderStyle = MicroBorderStyle.Curve;

                        sectorGrid.visible = sectorGrid.visible && (scale >= 4);
                        subsectorGrid.visible = subsectorGrid.visible && (scale >= 32);
                        parsecGrid.visible = false;

                        subsectorGrid.pen.width = 0.03f * (64.0f / (float)scale);
                        subsectorGrid.pen.DashPattern = new double[] { 10.0, 8.0 };

                        sectorGrid.pen.width = 0.03f * (64.0f / (float)scale);
                        sectorGrid.pen.DashPattern = new double[] { 10.0, 8.0 };

                        worlds.textBackgroundStyle = TextBackgroundStyle.Shadow;

                        worldDetails = worldDetails
                            & ~WorldDetails.Starport & ~WorldDetails.Allegiance & ~WorldDetails.Bases & ~WorldDetails.Hex;

                        if (scale < CandyMinWorldNameScale)
                            worldDetails = worldDetails & ~WorldDetails.KeyNames & ~WorldDetails.AllNames;
                        if (scale < CandyMinUwpScale)
                            worldDetails = worldDetails & ~WorldDetails.Uwp;

                        amberZone.pen.color = Color.Goldenrod;

                        sectorName.textStyle.Rotation = 0;
                        sectorName.textStyle.Translation = new PointF(0, -0.25f);
                        sectorName.textStyle.Scale = new SizeF(0.5f, 0.25f);
                        sectorName.textStyle.Uppercase = true;

                        subsectorNames.textStyle.Rotation = 0;
                        subsectorNames.textStyle.Translation = new PointF(0, -0.25f);
                        subsectorNames.textStyle.Scale = new SizeF(0.3f, 0.15f); // Expand
                        subsectorNames.textStyle.Uppercase = true;

                        microBorders.textStyle.Rotation = 0;
                        microBorders.textStyle.Translation = new PointF(0, 0.25f);
                        microBorders.textStyle.Scale = new SizeF(1.0f, 0.5f); // Expand
                        microBorders.textStyle.Uppercase = true;

                        microBorders.pen.color = Color.FromArgb(128, Color.Red);

                        worlds.textStyle.Rotation = 0;
                        worlds.textStyle.Scale = new SizeF(1f, 0.5f); // Expand
                        worlds.textStyle.Translation = new PointF(0, 0);
                        worlds.textStyle.Uppercase = true;

                        if (scale > CandyMaxWorldRelativeScale)
                        {
                            hexContentScale = CandyMaxWorldRelativeScale / (float)scale;
                        }

                        break;
                    }
            }

            sectorName.textColor = scale < 16 ? foregroundColor :
                scale < 48 ? darkColor : dimColor;
            subsectorNames.textColor = scale < 16 ? foregroundColor :
                scale < 48 ? darkColor : dimColor;

            // Cap pen widths when zooming in
            float penScale = (scale <= 64) ? 1f : (64f / (float)scale);

            float borderPenWidth =
                (scale < MicroBorderMinScale) ? 1 : // When rendering vector borders
                (scale < ParsecMinScale) ? 1 :      // When not rendering "hexes"
                0.16f * penScale; // ... but cut in half by clipping

            float routePenWidth =
                scale <= 16 ? 0.2f :
                0.08f * penScale;

            microBorders.pen.width = borderPenWidth;
            macroBorders.pen.width = borderPenWidth;
            microRoutes.pen.width = routePenWidth;

            amberZone.pen.width = redZone.pen.width = blueZone.pen.width = 0.05f * penScale;

            macroRoutes.pen.width = borderPenWidth;
            macroRoutes.pen.DashStyle = XDashStyle.Dash;

            if (style == Style.Candy)
            {
                subsectorNames.textColor = sectorName.textColor = Color.FromArgb(128, Color.Goldenrod);

                amberZone.pen.width = redZone.pen.width = blueZone.pen.width = 0.035f;

                microRoutes.pen.width = scale < CandyMaxRouteRelativeScale ? routePenWidth : routePenWidth / 2;
                macroBorders.pen.width = scale < CandyMaxBorderRelativeScale ? borderPenWidth : borderPenWidth / 4;
                microBorders.pen.width = scale < CandyMaxBorderRelativeScale ? borderPenWidth : borderPenWidth / 4;
            }

            preferredMimeType = (style == Style.Candy)
                ? System.Net.Mime.MediaTypeNames.Image.Jpeg
                : Util.MediaTypeName_Image_Png;

            pseudoRandomStars.fillColor = foregroundColor;

            macroNames.textColor = foregroundColor;
            macroNames.textHighlightColor = highlightColor;

            macroRoutes.textColor = foregroundColor;
            macroRoutes.textHighlightColor = highlightColor;

            worlds.textColor = foregroundColor;
            worlds.textHighlightColor = highlightColor;

            hexNumber.textColor = lightColor;
            imageBorderColor = lightColor;
        }

        public struct StyleElement
        {
            public bool visible;

            public Color fillColor;

            public PenInfo pen;

            public Color textColor;
            public Color textHighlightColor;
            public LabelStyle textStyle;
            public TextBackgroundStyle textBackgroundStyle;
            public FontInfo fontInfo;
            public FontInfo smallFontInfo;
            public FontInfo mediumFontInfo;
            public FontInfo largeFontInfo;

            public PointF position;

            private XFont font;
            public XFont Font { get { if (font == null) { font = this.fontInfo.makeFont(); } return font; } }
            private XFont smallFont;
            public XFont SmallFont { get { if (smallFont == null) { smallFont = this.smallFontInfo.makeFont(); } return smallFont; } }
            private XFont mediumFont;
            public XFont MediumFont { get { if (mediumFont == null) { mediumFont = this.mediumFontInfo.makeFont(); } return mediumFont; } }
            private XFont largeFont;
            public XFont LargeFont { get { if (largeFont == null) { largeFont = this.largeFontInfo.makeFont(); } return largeFont; } }
        }


        // Options

        public Color backgroundColor;
        public Color imageBorderColor;

        public bool useBackgroundImage;
        public bool useGalaxyImage;
        public bool useWorldImages;

        public float deepBackgroundOpacity;

        public bool grayscale;

        public bool showRifts;
        public float riftOpacity;

        public float hexContentScale = 1.0f;

        public String preferredMimeType;


        // Worlds
        public StyleElement worlds;
        public bool showWorldDetailColors;

        public PointF StarportPosition;
        public PointF GasGiantPosition;
        public PointF AllegiancePosition;
        public PointF BaseTopPosition;
        public PointF BaseBottomPosition;
        public PointF BaseMiddlePosition;


        public FontInfo glyphFont;
        public FontInfo starportFont;
        public WorldDetails worldDetails;
        public bool lowerCaseAllegiance;
        public FontInfo wingdingFont;

        // Hex Coordinates
        public StyleElement hexNumber;
        public enum HexCoordinateStyle
        {
            Sector,
            Subsector
        };
        public HexCoordinateStyle hexCoordinateStyle;

        // Sector Name
        public StyleElement sectorName;
        public bool showSomeSectorNames;
        public bool showAllSectorNames;

        public StyleElement capitals;
        public StyleElement subsectorNames;
        public StyleElement amberZone;
        public StyleElement redZone;
        public StyleElement blueZone;
        public StyleElement sectorGrid;
        public StyleElement subsectorGrid;
        public StyleElement parsecGrid;
        public StyleElement worldWater;
        public StyleElement worldNoWater;
        public StyleElement macroRoutes;
        public StyleElement microRoutes;
        public StyleElement macroBorders;
        public StyleElement macroNames;
        public StyleElement pseudoRandomStars;

        public StyleElement microBorders;
        public bool fillMicroBorders;
        public bool showMicroNames;
        public MicroBorderStyle microBorderStyle;

        public void WorldColors(World world, out XColor penColor, out XColor brushColor)
        {
            penColor = XColor.Empty;
            brushColor = XColor.Empty;

            if (showWorldDetailColors)
            {
                if (world.IsAg && world.IsRi)
                {
                    penColor = brushColor = Color.Gold;
                }
                else if (world.IsAg)
                {
                    penColor = brushColor = Color.Green;
                }
                else if (world.IsRi)
                {
                    penColor = brushColor = Color.Purple;
                }
                else if (world.IsIn)
                {
                    penColor = brushColor = Color.FromArgb(0x88, 0x88, 0x88); // Gray
                }
                else if (world.Atmosphere > 10)
                {
                    penColor = brushColor = Color.FromArgb(0xcc, 0x66, 0x26); // Rust
                }
                else if (world.IsVa)
                {
                    brushColor = Color.Black;
                    penColor = Color.White;
                }
                else if (world.WaterPresent)
                {
                    brushColor = worldWater.fillColor;
                    penColor = worldWater.pen.color;
                }
                else
                {
                    brushColor = worldNoWater.fillColor;
                    penColor = worldNoWater.pen.color;
                }
            }
            else
            {
                // Classic colors

                // World disc
                brushColor = (world.WaterPresent) ? worldWater.fillColor : worldNoWater.fillColor;
                penColor = (world.WaterPresent) ? worldWater.pen.color : worldNoWater.pen.color;
            }
        }

        public static float ScaleInterpolate(float minValue, float maxValue, double scale, float minScale, float maxScale)
        {
            if (scale <= minScale) return minValue;
            if (scale >= maxScale) return maxValue;

            float logscale = (float)Math.Log(scale, 2.0);
            float logmin = (float)Math.Log(minScale, 2.0);
            float logmax = (float)Math.Log(maxScale, 2.0);
            float p = (logscale - logmin) / (logmax - logmin);
            float value = minValue + (maxValue - minValue) * p;
            return value;
        }

        public static int ScaleInterpolate(int minValue, int maxValue, double scale, float minScale, float maxScale)
        {
            if (scale <= minScale) return minValue;
            if (scale >= maxScale) return maxValue;

            float logscale = (float)Math.Log(scale, 2.0);
            float logmin = (float)Math.Log(minScale, 2.0);
            float logmax = (float)Math.Log(maxScale, 2.0);
            float p = (logscale - logmin) / (logmax - logmin);
            float value = minValue + (maxValue - minValue) * p;
            return (int)Math.Round(value);
        }

    }

    public class FontCache : IDisposable
    {

        public FontCache(Stylesheet sheet)
        {
            this.sheet = sheet;
        }
        private Stylesheet sheet;

        private XFont wingdingFont;
        public XFont WingdingFont { get { if (wingdingFont == null) { wingdingFont = this.sheet.wingdingFont.makeFont(); } return wingdingFont; } }

        private XFont glyphFont;
        public XFont GlyphFont { get { if (glyphFont == null) { glyphFont = this.sheet.glyphFont.makeFont(); } return glyphFont; } }

        private XFont starportFont;
        public XFont StarportFont { get { if (starportFont == null) { starportFont = this.sheet.starportFont.makeFont(); } return starportFont; } }

        private bool disposed;
        public void Dispose() { Dispose(true); GC.SuppressFinalize(this); }
        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
#if DISPOSABLE_RESOURCES
                if( disposing )
                {
                    if( this.sectorNameFont != null ) this.sectorNameFont.Dispose();
                    if( this.subsectorNameFont != null ) this.subsectorNameFont.Dispose();
                    if( this.microPolityNameFont != null ) this.microPolityNameFont.Dispose();
                    if( this.microPolityNameSmallFont != null ) this.microPolityNameSmallFont.Dispose();

                    if( this.worldFont != null ) this.worldFont.Dispose();
                    if( this.symbolFont != null ) this.symbolFont.Dispose();
                    if( this.glyphFont != null ) this.glyphFont.Dispose();
                    if( this.hexFont != null ) this.hexFont.Dispose();
                    if( this.smallFont != null ) this.smallFont.Dispose();
                    if( this.largeFont != null ) this.largeFont.Dispose();
                    if( this.starportFont != null ) this.starportFont.Dispose();
                }
#endif
                this.disposed = true;
            }
        }
    }
}
