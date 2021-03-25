#nullable enable
using Maps.Graphics;
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;

namespace Maps.Rendering
{
    public static class TravellerColors
    {
        public static readonly Color Red = Color.FromArgb(0xE3, 0x27, 0x36);
        public static readonly Color Amber = Color.FromArgb(0xFF, 0xCC, 0x00);
        public static readonly Color Green = Color.FromArgb(0x04, 0x81, 0x04);
    }

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
        None = 0,

        Type = 1 << 0, // Show world type (water/no water/asteroid/unknown)
        KeyNames = 1 << 1, // Show HiPop/Capital names
        Starport = 1 << 2, // Show starport
        GasGiant = 1 << 3, // Show gas giant glyph
        Allegiance = 1 << 4, // Show allegiance code
        Bases = 1 << 5, // Show bases
        Hex = 1 << 6, // Include hex numbers
        Zone = 1 << 7, // Show Amber/Red zones
        AllNames = 1 << 8, // Show all world names, not just HiPop/Capitals
        Uwp = 1 << 9, // Show UWP below world name
        Asteroids = 1 << 10, // Render asteroids as pseudorandom ovals
        Highlight = 1 << 11, // Highlight (text font, text color) HiPopCapital worlds

        Dotmap = None,
        Atlas = Type | KeyNames | Starport | GasGiant | Allegiance | Bases | Zone | Highlight,
        Poster = Atlas | Hex | AllNames | Asteroids,
    }

    public enum TextBackgroundStyle
    {
        None,
        Rectangle,
        Shadow,
        Outline,
        Filled
    }

    public enum LayerId
    {
        //------------------------------------------------------------
        // Background
        //------------------------------------------------------------

        Background_Solid,
        Background_NebulaTexture,
        Background_Galaxy,

        Background_PseudoRandomStars,
        Background_Rifts,

        //------------------------------------------------------------
        // Foreground
        //------------------------------------------------------------

        Macro_Borders,
        Macro_Routes,

        Grid_Sector,
        Grid_Subsector,
        Grid_Parsec,

        Names_Subsector,

        Micro_BordersFill,
        Micro_BordersShade,
        Micro_BordersStroke,
        Micro_Routes,
        Micro_BorderExplicitLabels,

        Names_Sector,

        Macro_GovernmentRiftRouteNames,
        Macro_CapitalsAndHomeWorlds,
        Mega_GalaxyScaleLabels,

        Worlds_Background,
        Worlds_Foreground,
        Worlds_Overlays,

        //------------------------------------------------------------
        // Overlays
        //------------------------------------------------------------

        Overlay_DroyneChirperWorlds,
        Overlay_MinorHomeworlds,
        Overlay_AncientsWorlds,
        Overlay_ReviewStatus,
    }

    internal struct FontInfo
    {
        public string name;
        public float size;
        public FontStyle style;

        public FontInfo(string name, float size, FontStyle style = FontStyle.Regular)
        {
            this.name = name;
            this.size = size;
            this.style = style;
        }

        public AbstractFont MakeFont()
        {
            if (name == null)
                throw new ApplicationException("AbstractFont has null name");
            return new AbstractFont(name, size * 1.4f, style, GraphicsUnit.World);
        }
    }

    internal struct PenInfo
    {
        public Color color;
        public float width;
        public DashStyle dashStyle;
        public float[]? dashPattern;

        public PenInfo(Color color, float width, DashStyle style = DashStyle.Solid)
        {
            this.color = color;
            this.width = width;
            dashStyle = style;
            dashPattern = null;
        }

        public void Apply(ref AbstractPen pen)
        {
            if (width == 0f)
                throw new ArgumentOutOfRangeException(nameof(width), width, "Hairline pens not supported, set width > 0");

            pen.Color = color;
            pen.Width = width;
            pen.DashStyle = dashStyle;
            pen.CustomDashPattern = dashPattern;
        }
    }

    internal struct LabelStyle
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

    public enum HexStyle
    {
        None,
        Hex,
        Square,
    }

    public enum HexCoordinateStyle
    {
        Sector,
        Subsector
    };

    public enum Style { Poster, Atlas, Candy, Print, Draft, FASA, Terminal, Mongoose };

    internal class Stylesheet
    {
        public const string DEFAULT_FONT = "Arial";

        private const float SectorGridMinScale = 1 / 2f; // Below this, no sector grid is shown
        private const float SectorGridFullScale = 4; // Above this, sector grid opaque
        private const float SectorNameMinScale = 1;
        private const float SectorNameAllSelectedScale = 4; // At this point, "Selected" == "All"
        private const float SectorNameMaxScale = 16;
        private const float PseudoRandomStarsMinScale = 1; // Below this, no pseudo-random stars
        private const float PseudoRandomStarsMaxScale = 4; // Above this, no pseudo-random stars
        private const float SubsectorsMinScale = 8;
        private const float SubsectorNameMinScale = 24;
        private const float SubsectorNameMaxScale = 64;
        private const float MegaLabelMaxScale = 1f / 4;
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

        private const float T5AllegianceCodeMinScale = 64;

        public Stylesheet(double scale, MapOptions options, Style style)
        {
            float onePixel = 1f / (float)scale;

            grayscale = false;
            lightBackground = false;

            subsectorGrid.visible = ((scale >= SubsectorsMinScale) && options.HasFlag(MapOptions.SubsectorGrid));
            sectorGrid.visible = ((scale >= SectorGridMinScale) && options.HasFlag(MapOptions.SectorGrid));
            parsecGrid.visible = (scale >= ParsecMinScale);
            showSomeSectorNames = ((scale >= SectorNameMinScale) && (scale <= SectorNameMaxScale) && ((options & MapOptions.SectorsMask) != 0));
            showAllSectorNames = showSomeSectorNames && ((scale >= SectorNameAllSelectedScale) || options.HasFlag(MapOptions.SectorsAll));
            subsectorNames.visible = ((scale >= SubsectorNameMinScale) && (scale <= SubsectorNameMaxScale) && ((options & MapOptions.SectorsMask) != 0));

            worlds.visible = (scale >= WorldMinScale);
            pseudoRandomStars.visible = (PseudoRandomStarsMinScale <= scale) && (scale <= PseudoRandomStarsMaxScale);
            showRiftOverlay = (scale <= PseudoRandomStarsMaxScale || style == Style.Candy);

            t5AllegianceCodes = scale >= T5AllegianceCodeMinScale;

            riftOpacity = ScaleInterpolate(0f, 0.85f, scale, 1 / 4f, 4f);

            deepBackgroundOpacity = ScaleInterpolate(1f, 0f, scale, 1 / 8f, 2f);

            macroRoutes.visible = (scale >= MacroRouteMinScale) && (scale <= MacroRouteMaxScale);
            macroNames.visible = (scale >= MacroLabelMinScale) && (scale <= MacroLabelMaxScale);
            megaNames.visible = scale <= MegaLabelMaxScale && ((options & MapOptions.NamesMask) != 0);
            showMicroNames = ((scale >= MicroNameMinScale) && ((options & MapOptions.NamesMask) != 0));
            capitals.visible = (scale >= MacroWorldsMinScale) && (scale <= MacroWorldsMaxScale);

            hexStyle = (((options & MapOptions.ForceHexes) == 0) && (scale < ParsecHexMinScale))
                ? HexStyle.Square
                : HexStyle.Hex;
            microBorderStyle = hexStyle == HexStyle.Square ? MicroBorderStyle.Square : MicroBorderStyle.Hex;

            macroBorders.visible = (scale >= MacroBorderMinScale) && (scale < MicroBorderMinScale) && ((options & MapOptions.BordersMask) != 0);
            microBorders.visible = (scale >= MicroBorderMinScale) && ((options & MapOptions.BordersMask) != 0);
            fillMicroBorders = microBorders.visible && options.HasFlag(MapOptions.FilledBorders);
            microRoutes.visible = (scale >= RouteMinScale);

            worldDetails = !worlds.visible ? WorldDetails.None :
                (scale < WorldBasicMinScale) ? WorldDetails.Dotmap :
                (scale < WorldFullMinScale) ? WorldDetails.Atlas :
                WorldDetails.Poster;

            discRadius = worldDetails.HasFlag(WorldDetails.Type) ? 0.1f : 0.2f;

            showWorldDetailColors = worldDetails == WorldDetails.Poster && options.HasFlag(MapOptions.WorldColors);

            lowerCaseAllegiance = (scale < WorldFullMinScale);
            showGasGiantRing = (scale >= WorldUwpMinScale);

            worlds.textBackgroundStyle = TextBackgroundStyle.Rectangle;

            hexCoordinateStyle = HexCoordinateStyle.Sector;
            numberAllHexes = false;

            if (scale < WorldFullMinScale)
            {
                // Atlas-style

                const float x = 0.225f;
                const float y = 0.125f;

                BaseTopPosition = new PointF(-x, -y);
                BaseBottomPosition = new PointF(-x, y);
                GasGiantPosition = new PointF(x, -y);
                AllegiancePosition = new PointF(x, y);

                BaseMiddlePosition = new PointF(options.HasFlag(MapOptions.ForceHexes) ? -0.35f : -0.2f, 0);
                starport.position = new PointF(0, -0.24f);
                uwp.position = new PointF(0, 0.24f);
                worlds.position = new PointF(0, 0.4f);
            }
            else
            {
                // Poster-style

                const float x = 0.25f;
                const float y = 0.18f;

                BaseTopPosition = new PointF(-x, -y);
                BaseBottomPosition = new PointF(-x, y);
                GasGiantPosition = new PointF(x, -y);
                AllegiancePosition = new PointF(x, y);

                BaseMiddlePosition = new PointF(-0.35f, 0);
                starport.position = new PointF(0, -0.225f);
                uwp.position = new PointF(0, 0.225f);
                worlds.position = new PointF(0, 0.37f); // Don't hide hex bottom, leave room for UWP
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

                worlds.fontInfo = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.2f : 0.15f * fontScale, FontStyle.Bold);
                wingdingFont = new FontInfo("Wingdings", scale < WorldFullMinScale ? 0.2f : 0.175f * fontScale);
                glyphFont = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.175f : 0.15f * fontScale, FontStyle.Bold);
                uwp.fontInfo = hexNumber.fontInfo = new FontInfo(DEFAULT_FONT, 0.1f * fontScale);
                worlds.smallFontInfo = new FontInfo(DEFAULT_FONT, scale < WorldFullMinScale ? 0.2f : 0.1f * fontScale, FontStyle.Regular);
                worlds.largeFontInfo = worlds.fontInfo;
                starport.fontInfo = (scale < WorldFullMinScale) ? worlds.smallFontInfo : worlds.fontInfo;
            }

            sectorName.fontInfo = new FontInfo(DEFAULT_FONT, 5.5f);
            subsectorNames.fontInfo = new FontInfo(DEFAULT_FONT, 1.5f);

            float overlayFontSize = Math.Max(onePixel * 12f, 0.375f);
            droyneWorlds.fontInfo = new FontInfo(DEFAULT_FONT, overlayFontSize);
            ancientsWorlds.fontInfo = new FontInfo(DEFAULT_FONT, overlayFontSize);
            minorHomeWorlds.fontInfo = new FontInfo(DEFAULT_FONT, overlayFontSize);

            droyneWorlds.content = "\u2605\u2606"; // BLACK STAR / WHITE STAR
            minorHomeWorlds.content = "\u273B"; // TEARDROP-SPOKED ASTERISK
            ancientsWorlds.content = "\u2600"; // BLACK SUN WITH RAYS

            microBorders.fontInfo = new FontInfo(DEFAULT_FONT, (scale == MicroNameMinScale) ? 0.6f : 0.25f, FontStyle.Bold);
            microBorders.smallFontInfo = new FontInfo(DEFAULT_FONT, 0.15f, FontStyle.Bold);
            microBorders.largeFontInfo = new FontInfo(DEFAULT_FONT, 0.75f, FontStyle.Bold);

            macroNames.fontInfo = new FontInfo(DEFAULT_FONT, 8f / 1.4f, FontStyle.Bold);
            macroNames.smallFontInfo = new FontInfo(DEFAULT_FONT, 5f / 1.4f, FontStyle.Regular);
            macroNames.mediumFontInfo = new FontInfo(DEFAULT_FONT, 6.5f / 1.4f, FontStyle.Italic);

            float megaNameScaleFactor = Math.Min(35f, 0.75f * onePixel);
            megaNames.fontInfo = new FontInfo(DEFAULT_FONT, 24f * megaNameScaleFactor, FontStyle.Bold);
            megaNames.mediumFontInfo = new FontInfo(DEFAULT_FONT, 22f * megaNameScaleFactor, FontStyle.Regular);
            megaNames.smallFontInfo = new FontInfo(DEFAULT_FONT, 18f * megaNameScaleFactor, FontStyle.Italic);

            capitals.fillColor = Color.Wheat;
            capitals.textColor = TravellerColors.Red;
            amberZone.visible = redZone.visible = true;
            amberZone.pen.color = TravellerColors.Amber;
            redZone.pen.color = TravellerColors.Red;
            macroBorders.pen.color = TravellerColors.Red;
            macroRoutes.pen.color = Color.White;
            microBorders.pen.color = Color.Gray;
            microRoutes.pen.color = Color.Gray;

            backgroundColor = Color.Black;

            microBorders.textColor = TravellerColors.Amber;
            worldWater.fillColor = Color.DeepSkyBlue;
            worldNoWater.fillColor = Color.White;
            worldNoWater.pen.color = Color.Empty;

            Color gridColor = Color.FromArgb(ScaleInterpolate(0, 255, scale, SectorGridMinScale, SectorGridFullScale), Color.Gray);
            parsecGrid.pen = new PenInfo(gridColor, onePixel);
            subsectorGrid.pen = new PenInfo(gridColor, onePixel * 2);
            sectorGrid.pen = new PenInfo(gridColor, (subsectorGrid.visible ? 4 : 2) * onePixel);
            worldWater.pen = new PenInfo(Color.Empty, Math.Max(0.01f, onePixel));

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

            hexNumber.position = new PointF(0, -0.5f);

            showNebulaBackground = false;
            showGalaxyBackground = deepBackgroundOpacity > 0.0f;
            useWorldImages = false;

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

            amberZone.pen.width = redZone.pen.width = 0.05f * penScale;

            macroRoutes.pen.width = borderPenWidth;
            macroRoutes.pen.dashStyle = DashStyle.Dash;

            populationOverlay.fillColor = Color.FromArgb(0x80, 0xff, 0xff, 0x00);
            importanceOverlay.fillColor = Color.FromArgb(0x20, 0x80, 0xff, 0x00);
            highlightWorlds.fillColor = Color.FromArgb(0x80, 0xff, 0x00, 0x00);

            populationOverlay.pen = new PenInfo(Color.Empty, 0.03f * penScale, DashStyle.Dash);
            importanceOverlay.pen = new PenInfo(Color.Empty, 0.03f * penScale, DashStyle.Dot);
            highlightWorlds.pen = new PenInfo(Color.Empty, 0.03f * penScale, DashStyle.DashDot);

            capitalOverlay.fillColor = Color.FromArgb(0x80, TravellerColors.Green);
            capitalOverlayAltA.fillColor = Color.FromArgb(0x80, Color.Blue);
            capitalOverlayAltB.fillColor = Color.FromArgb(0x80, TravellerColors.Amber);

            bool fadeSectorSubsectorNames = true;

            placeholder.content = "*";
            placeholder.fontInfo = new FontInfo("Georgia", 0.6f);
            placeholder.position = new PointF(0, 0.17f);

            anomaly.content = "\u2316"; // POSITION INDICATOR
            anomaly.fontInfo = new FontInfo("Segoe UI Symbol", 0.6f);

            var layers = new List<LayerId>
            {
                //------------------------------------------------------------
                // Background
                //------------------------------------------------------------

                LayerId.Background_Solid,
                LayerId.Background_NebulaTexture,
                LayerId.Background_Galaxy,
                LayerId.Background_PseudoRandomStars,
                LayerId.Background_Rifts,

                //------------------------------------------------------------
                // Foreground
                //------------------------------------------------------------

                LayerId.Macro_Borders,
                LayerId.Macro_Routes,

                LayerId.Grid_Sector,
                LayerId.Grid_Subsector,
                LayerId.Grid_Parsec,

                LayerId.Names_Subsector,

                LayerId.Micro_BordersFill,
                LayerId.Micro_BordersShade,
                LayerId.Micro_BordersStroke,
                LayerId.Micro_Routes,
                LayerId.Micro_BorderExplicitLabels,

                LayerId.Names_Sector,

                LayerId.Macro_GovernmentRiftRouteNames,
                LayerId.Macro_CapitalsAndHomeWorlds,
                LayerId.Mega_GalaxyScaleLabels,

                LayerId.Worlds_Background,
                LayerId.Worlds_Foreground,
                LayerId.Worlds_Overlays,

                //------------------------------------------------------------
                // Overlays
                //------------------------------------------------------------

                LayerId.Overlay_DroyneChirperWorlds,
                LayerId.Overlay_MinorHomeworlds,
                LayerId.Overlay_AncientsWorlds,
                LayerId.Overlay_ReviewStatus,
            };

            // Generic colors; applied to various elements by default (see end of this method).
            // May be overridden by specific styles
            Color foregroundColor = Color.White;
            Color lightColor = Color.LightGray;
            Color darkColor = Color.DarkGray;
            Color dimColor = Color.DimGray;
            Color highlightColor = TravellerColors.Red;

            preferredMimeType = ContentTypes.Image.Png;

            switch (style)
            {
                case Style.Poster:
                    {
                        // This is the default - no changes
                        break;
                    }
                case Style.Atlas:
                    {
                        grayscale = true;
                        lightBackground = true;

                        capitals.fillColor = Color.DarkGray;
                        capitals.textColor = Color.Black;
                        amberZone.pen.color = Color.LightGray;
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

                        populationOverlay.fillColor = Color.FromArgb(0x40, highlightColor);
                        populationOverlay.pen.color = Color.Gray;

                        importanceOverlay.fillColor = Color.FromArgb(0x20, highlightColor);
                        importanceOverlay.pen.color = Color.Gray;

                        highlightWorlds.fillColor = Color.FromArgb(0x30, highlightColor);
                        highlightWorlds.pen.color = Color.Gray;

                        break;
                    }
                case Style.FASA:
                    {
                        showGalaxyBackground = false;
                        deepBackgroundOpacity = 0f;
                        riftOpacity = 0;

                        Color inkColor = Color.FromArgb(0x5C, 0x40, 0x33);

                        foregroundColor = inkColor;
                        backgroundColor = Color.White;

                        grayscale = true; // TODO: Tweak to be "monochrome"
                        lightBackground = true;

                        capitals.fillColor = inkColor;
                        capitals.textColor = inkColor;
                        amberZone.pen.color = inkColor;
                        amberZone.pen.width = onePixel * 2;
                        redZone.pen.color = Color.Empty;
                        redZone.fillColor = Color.FromArgb(0x80, inkColor);

                        macroBorders.pen.color = inkColor;
                        macroRoutes.pen.color = inkColor;

                        microBorders.pen.color = inkColor;
                        microBorders.pen.width = onePixel * 2;
                        microBorders.fontInfo.size *= 0.6f;
                        microBorders.fontInfo.style = FontStyle.Regular;

                        microRoutes.pen.color = inkColor;

                        lightColor = Color.FromArgb(0x80, inkColor);
                        darkColor = inkColor;
                        dimColor = inkColor;
                        highlightColor = inkColor;
                        microBorders.textColor = inkColor;
                        hexStyle = HexStyle.Hex;
                        microBorderStyle = MicroBorderStyle.Curve;

                        parsecGrid.pen.color = lightColor;
                        sectorGrid.pen.color = lightColor;
                        subsectorGrid.pen.color = lightColor;

                        worldWater.fillColor = inkColor;
                        worldNoWater.fillColor = inkColor;
                        worldWater.pen.color = Color.Empty;
                        worldNoWater.pen.color = Color.Empty;

                        showWorldDetailColors = false;

                        worldDetails &= ~WorldDetails.Starport;
                        worldDetails &= ~WorldDetails.Allegiance;
                        worldDetails &= ~WorldDetails.Bases;
                        worldDetails &= ~WorldDetails.GasGiant;
                        worldDetails &= ~WorldDetails.Highlight;
                        worldDetails &= ~WorldDetails.Uwp;
                        worlds.fontInfo.size *= 0.85f;
                        worlds.textStyle.Translation = new PointF(0, 0.25f);

                        numberAllHexes = true;
                        hexCoordinateStyle = HexCoordinateStyle.Subsector;
                        overrideLineStyle = LineStyle.Solid;

                        populationOverlay.fillColor = Color.FromArgb(0x40, highlightColor);
                        populationOverlay.pen.color = Color.Gray;

                        importanceOverlay.fillColor = Color.FromArgb(0x20, highlightColor);
                        importanceOverlay.pen.color = Color.Gray;

                        highlightWorlds.fillColor = Color.FromArgb(0x30, highlightColor);
                        highlightWorlds.pen.color = Color.Gray;

                        break;
                    }
                case Style.Print:
                    {
                        lightBackground = true;

                        foregroundColor = Color.Black;
                        backgroundColor = Color.White;
                        lightColor = Color.DarkGray;
                        darkColor = Color.DarkGray;
                        dimColor = Color.LightGray;
                        microRoutes.pen.color = Color.Gray;

                        microBorders.textColor = Color.Brown;

                        amberZone.pen.color = TravellerColors.Amber;
                        worldNoWater.fillColor = Color.White;
                        worldNoWater.pen = new PenInfo(Color.Black, onePixel);

                        riftOpacity = Math.Min(riftOpacity, 0.70f);

                        populationOverlay.fillColor = Color.FromArgb(0x40, populationOverlay.fillColor);
                        populationOverlay.pen.color = Color.Gray;

                        importanceOverlay.fillColor = Color.FromArgb(0x20, importanceOverlay.fillColor);
                        importanceOverlay.pen.color = Color.Gray;

                        highlightWorlds.fillColor = Color.FromArgb(0x30, highlightWorlds.fillColor);
                        highlightWorlds.pen.color = Color.Gray;

                        break;
                    }
                case Style.Draft:
                    {
                        int inkOpacity = 0xB0;

                        showGalaxyBackground = false;
                        lightBackground = true;

                        deepBackgroundOpacity = 0f;

                        backgroundColor = Color.AntiqueWhite;
                        foregroundColor = Color.FromArgb(inkOpacity, Color.Black);
                        highlightColor = Color.FromArgb(inkOpacity, TravellerColors.Red);

                        lightColor = Color.FromArgb(inkOpacity, Color.DarkCyan);
                        darkColor = Color.FromArgb(inkOpacity, Color.Black);
                        dimColor = Color.FromArgb(inkOpacity / 2, Color.Black);


                        subsectorGrid.pen.color = Color.FromArgb(inkOpacity, Color.Firebrick);

                        const string FONT_NAME = "Comic Sans MS";
                        worlds.fontInfo.name = FONT_NAME;
                        worlds.smallFontInfo.name = FONT_NAME;
                        starport.fontInfo.name = FONT_NAME;
                        worlds.largeFontInfo.name = FONT_NAME;
                        worlds.largeFontInfo.size = worlds.fontInfo.size * 1.25f;
                        worlds.fontInfo.size *= 0.8f;

                        macroNames.fontInfo.name = FONT_NAME;
                        macroNames.mediumFontInfo.name = FONT_NAME;
                        macroNames.smallFontInfo.name = FONT_NAME;
                        megaNames.fontInfo.name = FONT_NAME;
                        megaNames.mediumFontInfo.name = FONT_NAME;
                        megaNames.smallFontInfo.name = FONT_NAME;
                        microBorders.smallFontInfo.name = FONT_NAME;
                        microBorders.largeFontInfo.name = FONT_NAME;
                        microBorders.fontInfo.name = FONT_NAME;
                        macroBorders.fontInfo.name = FONT_NAME;
                        macroRoutes.fontInfo.name = FONT_NAME;
                        capitals.fontInfo.name = FONT_NAME;
                        macroBorders.smallFontInfo.name = FONT_NAME;

                        microBorders.textStyle.Uppercase = true;

                        sectorName.textStyle.Uppercase = true;
                        subsectorNames.textStyle.Uppercase = true;

                        // TODO: Render small, around edges
                        subsectorNames.visible = false;

                        worlds.textStyle.Uppercase = true;

                        // TODO: Decide on this. It's nice to not overwrite the parsec grid, but
                        // it looks very cluttered, especially amber/red zones.
                        worlds.textBackgroundStyle = TextBackgroundStyle.None;

                        worldDetails &= ~WorldDetails.Allegiance;

                        subsectorNames.fontInfo.name = FONT_NAME;
                        sectorName.fontInfo.name = FONT_NAME;

                        worlds.largeFontInfo.style |= FontStyle.Underline;

                        microBorders.pen.width = onePixel * 4;
                        microBorders.pen.dashStyle = DashStyle.Dot;

                        worldNoWater.fillColor = foregroundColor;
                        worldWater.fillColor = Color.Empty;
                        worldWater.pen = new PenInfo(foregroundColor, onePixel * 2);

                        amberZone.pen.color = foregroundColor;
                        amberZone.pen.width = onePixel;
                        redZone.pen.width = onePixel * 2;

                        microRoutes.pen.color = Color.Gray;

                        parsecGrid.pen.color = lightColor;
                        microBorders.textColor = Color.FromArgb(inkOpacity, Color.Brown);

                        riftOpacity = Math.Min(riftOpacity, 0.30f);

                        numberAllHexes = true;

                        populationOverlay.fillColor = Color.FromArgb(0x40, populationOverlay.fillColor);
                        populationOverlay.pen.color = Color.Gray;

                        importanceOverlay.fillColor = Color.FromArgb(0x20, importanceOverlay.fillColor);
                        importanceOverlay.pen.color = Color.Gray;

                        highlightWorlds.fillColor = Color.FromArgb(0x30, highlightWorlds.fillColor);
                        highlightWorlds.pen.color = Color.Gray;

                        break;
                    }
                case Style.Candy:
                    {
                        preferredMimeType = ContentTypes.Image.Jpeg;

                        useWorldImages = true;
                        pseudoRandomStars.visible = false;
                        fadeSectorSubsectorNames = false;

                        showNebulaBackground = deepBackgroundOpacity < 0.5f;

                        hexStyle = HexStyle.None;
                        microBorderStyle = MicroBorderStyle.Curve;

                        sectorGrid.visible = sectorGrid.visible && (scale >= 4);
                        subsectorGrid.visible = subsectorGrid.visible && (scale >= 32);
                        parsecGrid.visible = false;

                        subsectorGrid.pen.width = 0.03f * (64.0f / (float)scale);
                        subsectorGrid.pen.dashStyle = DashStyle.Custom;
                        subsectorGrid.pen.dashPattern = new float[] { 10.0f, 8.0f };

                        sectorGrid.pen.width = 0.03f * (64.0f / (float)scale);
                        sectorGrid.pen.dashStyle = DashStyle.Custom;
                        sectorGrid.pen.dashPattern = new float[] { 10.0f, 8.0f };

                        worlds.textBackgroundStyle = TextBackgroundStyle.Shadow;

                        worldDetails = worldDetails
                            & ~WorldDetails.Starport & ~WorldDetails.Allegiance & ~WorldDetails.Bases & ~WorldDetails.Hex;

                        if (scale < CandyMinWorldNameScale)
                            worldDetails = worldDetails & ~WorldDetails.KeyNames & ~WorldDetails.AllNames;
                        if (scale < CandyMinUwpScale)
                            worldDetails &= ~WorldDetails.Uwp;

                        amberZone.pen.color = Color.Goldenrod;
                        amberZone.pen.width = redZone.pen.width = 0.035f;

                        sectorName.textStyle.Rotation = 0;
                        sectorName.textStyle.Translation = new PointF(0, -0.25f);
                        sectorName.textStyle.Scale = new SizeF(0.5f, 0.25f);
                        sectorName.textStyle.Uppercase = true;

                        subsectorNames.textStyle.Rotation = 0;
                        subsectorNames.textStyle.Translation = new PointF(0, -0.25f);
                        subsectorNames.textStyle.Scale = new SizeF(0.3f, 0.15f); // Expand
                        subsectorNames.textStyle.Uppercase = true;

                        subsectorNames.textColor = sectorName.textColor = Color.FromArgb(128, Color.Goldenrod);

                        microBorders.textStyle.Rotation = 0;
                        microBorders.textStyle.Translation = new PointF(0, 0.25f);
                        microBorders.textStyle.Scale = new SizeF(1.0f, 0.5f); // Expand
                        microBorders.textStyle.Uppercase = true;

                        microBorders.pen.color = Color.FromArgb(128, TravellerColors.Red);
                        microRoutes.pen.width = scale < CandyMaxRouteRelativeScale ? routePenWidth : routePenWidth / 2;
                        macroBorders.pen.width = scale < CandyMaxBorderRelativeScale ? borderPenWidth : borderPenWidth / 4;
                        microBorders.pen.width = scale < CandyMaxBorderRelativeScale ? borderPenWidth : borderPenWidth / 4;

                        worlds.textStyle.Rotation = 0;
                        worlds.textStyle.Scale = new SizeF(1f, 0.5f); // Expand
                        worlds.textStyle.Translation = new PointF(0, 0);
                        worlds.textStyle.Uppercase = true;

                        if (scale > CandyMaxWorldRelativeScale)
                            hexContentScale = CandyMaxWorldRelativeScale / (float)scale;

                        break;
                    }
                case Style.Terminal:
                    {
                        fadeSectorSubsectorNames = false;
                        showGalaxyBackground = false;
                        lightBackground = false;

                        backgroundColor = Color.Black;
                        foregroundColor = Color.Cyan;
                        highlightColor = Color.White;

                        lightColor = Color.LightBlue;
                        darkColor = Color.DarkBlue;
                        dimColor = Color.DimGray;

                        subsectorGrid.pen.color = Color.Cyan;

                        const string FONT_NAME = "Courier New";
                        worlds.fontInfo.name = FONT_NAME;
                        worlds.smallFontInfo.name = FONT_NAME;
                        starport.fontInfo.name = FONT_NAME;
                        worlds.largeFontInfo.name = FONT_NAME;
                        worlds.largeFontInfo.size = worlds.fontInfo.size * 1.25f;
                        worlds.fontInfo.size *= 0.8f;

                        macroNames.fontInfo.name = FONT_NAME;
                        macroNames.mediumFontInfo.name = FONT_NAME;
                        macroNames.smallFontInfo.name = FONT_NAME;
                        megaNames.fontInfo.name = FONT_NAME;
                        megaNames.mediumFontInfo.name = FONT_NAME;
                        megaNames.smallFontInfo.name = FONT_NAME;
                        microBorders.smallFontInfo.name = FONT_NAME;
                        microBorders.largeFontInfo.name = FONT_NAME;
                        microBorders.fontInfo.name = FONT_NAME;
                        macroBorders.fontInfo.name = FONT_NAME;
                        macroRoutes.fontInfo.name = FONT_NAME;
                        capitals.fontInfo.name = FONT_NAME;
                        macroBorders.smallFontInfo.name = FONT_NAME;

                        worlds.textStyle.Uppercase = true;
                        microBorders.textStyle.Uppercase = true;
                        microBorders.fontInfo.style |= FontStyle.Underline;

                        sectorName.textColor = foregroundColor;
                        sectorName.textStyle.Scale = new SizeF(1, 1);
                        sectorName.textStyle.Rotation = 0;
                        sectorName.textStyle.Uppercase = true;
                        sectorName.fontInfo.style |= FontStyle.Bold;
                        sectorName.fontInfo.size *= 0.5f;

                        subsectorNames.textColor = foregroundColor;
                        subsectorNames.textStyle.Scale = new SizeF(1, 1);
                        subsectorNames.textStyle.Rotation = 0;
                        subsectorNames.textStyle.Uppercase = true;
                        subsectorNames.fontInfo.style |= FontStyle.Bold;
                        subsectorNames.fontInfo.size *= 0.5f;

                        worlds.textStyle.Uppercase = true;

                        worlds.textBackgroundStyle = TextBackgroundStyle.None;

                        subsectorNames.fontInfo.name = FONT_NAME;
                        sectorName.fontInfo.name = FONT_NAME;

                        worlds.largeFontInfo.style |= FontStyle.Underline;

                        microBorders.pen.width = onePixel * 4;
                        microBorders.pen.dashStyle = DashStyle.Dot;

                        worldNoWater.fillColor = foregroundColor;
                        worldWater.fillColor = Color.Empty;
                        worldWater.pen = new PenInfo(foregroundColor, onePixel * 2);

                        amberZone.pen.color = foregroundColor;
                        amberZone.pen.width = onePixel;
                        redZone.pen.width = onePixel * 2;

                        microRoutes.pen.color = Color.Gray;

                        parsecGrid.pen.color = Color.Plum;
                        microBorders.textColor = Color.Cyan;

                        riftOpacity = Math.Min(riftOpacity, 0.30f);

                        numberAllHexes = true;

                        if (scale >= 64)
                            subsectorNames.visible = false;

                        break;
                    }
                case Style.Mongoose:
                    {
                        showGalaxyBackground = false;
                        lightBackground = true;
                        showGasGiantRing = true;
                        showTL = true;
                        ignoreBaseBias = true;
                        shadeMicroBorders = true;

                        // Re-order these elements
                        layers.MoveAfter(LayerId.Worlds_Background, LayerId.Micro_BordersStroke);
                        layers.MoveAfter(LayerId.Worlds_Foreground, LayerId.Micro_Routes);

                        imageBorderWidth = 0.1f;
                        deepBackgroundOpacity = 0f;

                        backgroundColor = Color.FromArgb(0xe6, 0xe7, 0xe8);
                        foregroundColor = Color.Black;
                        highlightColor = Color.Red;

                        lightColor = Color.Black;
                        darkColor = Color.Black;
                        dimColor = Color.Gray;

                        sectorGrid.pen.color = subsectorGrid.pen.color = parsecGrid.pen.color = foregroundColor;

                        microBorders.textColor = Color.DarkSlateGray;

                        const string FONT_NAME = "Calibri,Arial";
                        worlds.fontInfo.name = FONT_NAME;
                        worlds.smallFontInfo.name = FONT_NAME;
                        starport.fontInfo.name = FONT_NAME;
                        starport.fontInfo.style = FontStyle.Regular;
                        worlds.largeFontInfo.name = FONT_NAME;

                        worlds.fontInfo.style = FontStyle.Regular;
                        worlds.largeFontInfo.style = FontStyle.Bold;

                        hexNumber.fontInfo = worlds.fontInfo;
                        hexNumber.position.Y = -0.49f;
                        starport.fontInfo.style = FontStyle.Italic;

                        macroNames.fontInfo.name = FONT_NAME;
                        macroNames.mediumFontInfo.name = FONT_NAME;
                        macroNames.smallFontInfo.name = FONT_NAME;
                        megaNames.fontInfo.name = FONT_NAME;
                        megaNames.mediumFontInfo.name = FONT_NAME;
                        megaNames.smallFontInfo.name = FONT_NAME;
                        microBorders.smallFontInfo.name = FONT_NAME;
                        microBorders.largeFontInfo.name = FONT_NAME;
                        microBorders.fontInfo.name = FONT_NAME;
                        macroBorders.fontInfo.name = FONT_NAME;
                        macroRoutes.fontInfo.name = FONT_NAME;
                        capitals.fontInfo.name = FONT_NAME;
                        macroBorders.smallFontInfo.name = FONT_NAME;

                        microBorders.textStyle.Uppercase = true;

                        sectorName.textStyle.Uppercase = true;
                        subsectorNames.textStyle.Uppercase = true;

                        subsectorNames.visible = false;

                        worlds.textStyle.Uppercase = true;

                        worldDetails &= ~WorldDetails.Allegiance;

                        subsectorNames.fontInfo.name = FONT_NAME;
                        sectorName.fontInfo.name = FONT_NAME;

                        microBorders.pen.width = 0.11f;
                        microBorders.pen.dashStyle = DashStyle.Dot;

                        worldWater.fillColor = Color.MediumBlue;
                        worldNoWater.fillColor = Color.DarkKhaki;
                        worldWater.pen = worldNoWater.pen = new PenInfo(Color.DarkGray, onePixel * 2);

                        showZonesAsPerimeters = true;
                        greenZone.visible = true;
                        greenZone.pen.width = amberZone.pen.width = redZone.pen.width = 0.05f;

                        greenZone.pen.color = Color.FromArgb(0x80, 0xc6, 0x76);
                        amberZone.pen.color = Color.FromArgb(0xfb, 0xb0, 0x40);
                        redZone.pen.color = Color.FromArgb(0xff, 0x00, 0x00);

                        microBorders.textColor = Color.DarkSlateGray;

                        riftOpacity = Math.Min(riftOpacity, 0.30f);

                        discRadius = 0.11f;
                        GasGiantPosition = new PointF(0, -0.23f);
                        BaseTopPosition = new PointF(-0.22f, -0.21f);
                        BaseMiddlePosition = new PointF(-0.32f, 0.17f);
                        BaseBottomPosition = new PointF(0.22f, -0.21f);
                        starport.position = new PointF(0.175f, 0.17f);
                        uwp.position = new PointF(0, 0.40f);
                        DiscPosition = new PointF(-discRadius, 0.16f);
                        worlds.textStyle.Translation = new PointF(0, -0.04f);

                        worlds.textBackgroundStyle = TextBackgroundStyle.None;

                        uwp.fontInfo = hexNumber.fontInfo;
                        uwp.fillColor = Color.Black;
                        uwp.textColor = Color.White;
                        uwp.textBackgroundStyle = TextBackgroundStyle.Filled;

                        break;
                    }
            }

            // TODO: Do this with opacity.
            if (fadeSectorSubsectorNames)
            {
                sectorName.textColor = scale < 16 ? foregroundColor :
                    scale < 48 ? darkColor : dimColor;
                subsectorNames.textColor = scale < 16 ? foregroundColor :
                    scale < 48 ? darkColor : dimColor;
            }

            // Base element colors on foreground/light/dim/dark/highlight, if not specified by style.

            DefaultTo(ref pseudoRandomStars.fillColor, foregroundColor);

            DefaultTo(ref droyneWorlds.textColor, microBorders.textColor);
            DefaultTo(ref minorHomeWorlds.textColor, microBorders.textColor);
            DefaultTo(ref ancientsWorlds.textColor, microBorders.textColor);

            DefaultTo(ref megaNames.textColor, foregroundColor);
            DefaultTo(ref megaNames.textHighlightColor, highlightColor);

            DefaultTo(ref macroNames.textColor, foregroundColor);
            DefaultTo(ref macroNames.textHighlightColor, highlightColor);

            DefaultTo(ref macroRoutes.textColor, foregroundColor);
            DefaultTo(ref macroRoutes.textHighlightColor, highlightColor);

            DefaultTo(ref worlds.textColor, foregroundColor);
            DefaultTo(ref worlds.textHighlightColor, highlightColor);

            DefaultTo(ref hexNumber.textColor, lightColor);
            DefaultTo(ref uwp.textColor, foregroundColor);

            DefaultTo(ref placeholder.textColor, foregroundColor);
            DefaultTo(ref anomaly.textColor, highlightColor);

            DefaultTo(ref imageBorderColor, lightColor);

            // Convert list into a id -> index mapping.
            layerOrder = new Dictionary<LayerId, int>();
            for (var i = 0; i < layers.Count; ++i)
                layerOrder[layers[i]] = i;
        }

        private static void DefaultTo(ref Color property, Color defaultValue)
        {
            if (property.IsEmpty)
                property = defaultValue;
        }

        internal struct StyleElement
        {
            public bool visible;

            public Color fillColor;
            public string content;

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

            private AbstractFont? font;
            public AbstractFont Font => font ??= fontInfo.MakeFont();
            private AbstractFont? smallFont;
            public AbstractFont SmallFont => smallFont ??= smallFontInfo.MakeFont();
            private AbstractFont mediumFont;
            public AbstractFont MediumFont => mediumFont ??= mediumFontInfo.MakeFont();
            private AbstractFont? largeFont;
            public AbstractFont LargeFont => largeFont ??= largeFontInfo.MakeFont();
        }


        // Options

        public Color backgroundColor;

        public Color imageBorderColor;
        public float imageBorderWidth = 0.2f;

        public bool showNebulaBackground;
        public bool showGalaxyBackground;
        public bool useWorldImages;
        public bool dimUnofficialSectors;
        public bool colorCodeSectorStatus;

        public float deepBackgroundOpacity;

        public bool grayscale;
        public bool lightBackground;

        public bool showRiftOverlay;
        public float riftOpacity;

        public float hexContentScale = 1.0f;
        public float hexRotation = 0f;

        public float routeEndAdjust = 0.25f;

        public string preferredMimeType;
        public bool t5AllegianceCodes;

        public StyleElement highlightWorlds;
        public HighlightWorldPattern? highlightWorldsPattern;

        public StyleElement droyneWorlds;
        public StyleElement ancientsWorlds;
        public StyleElement minorHomeWorlds;

        // Worlds
        public StyleElement worlds;
        public bool showWorldDetailColors;
        public StyleElement populationOverlay;
        public StyleElement importanceOverlay;
        public StyleElement capitalOverlay;
        public StyleElement capitalOverlayAltA;
        public StyleElement capitalOverlayAltB;
        public bool showStellarOverlay;

        public bool HasWorldOverlays => populationOverlay.visible || importanceOverlay.visible || highlightWorlds.visible || showStellarOverlay || capitalOverlay.visible;
        public PointF DiscPosition;
        public float discRadius = 0.1f;
        public PointF GasGiantPosition;
        public PointF AllegiancePosition;
        public PointF BaseTopPosition;
        public PointF BaseBottomPosition;
        public PointF BaseMiddlePosition;

        public StyleElement uwp;
        public StyleElement starport;

        public FontInfo glyphFont;
        public WorldDetails worldDetails;
        public bool lowerCaseAllegiance;
        public FontInfo wingdingFont;
        public bool showGasGiantRing;

        public bool showTL;
        public bool ignoreBaseBias;
        public bool showZonesAsPerimeters;

        // Hex Coordinates
        public StyleElement hexNumber;
        public HexCoordinateStyle hexCoordinateStyle;
        public bool numberAllHexes;

        // Sector Name
        public StyleElement sectorName;
        public bool showSomeSectorNames;
        public bool showAllSectorNames;

        public StyleElement capitals;
        public StyleElement subsectorNames;
        public StyleElement greenZone;
        public StyleElement amberZone;
        public StyleElement redZone;
        public StyleElement sectorGrid;
        public StyleElement subsectorGrid;
        public StyleElement parsecGrid;
        public StyleElement worldWater;
        public StyleElement worldNoWater;
        public StyleElement macroRoutes;
        public StyleElement microRoutes;
        public StyleElement macroBorders;
        public StyleElement megaNames;
        public StyleElement macroNames;
        public StyleElement pseudoRandomStars;
        public StyleElement placeholder;
        public StyleElement anomaly;

        public StyleElement microBorders;
        public bool fillMicroBorders;
        public bool shadeMicroBorders;
        public bool showMicroNames;
        public MicroBorderStyle microBorderStyle;
        public HexStyle hexStyle;
        public LineStyle? overrideLineStyle;

        public Dictionary<LayerId, int> layerOrder;

        public void WorldColors(World world, out Color penColorOut, out Color brushColorOut)
        {
            Color penColor;
            Color brushColor;

            if (showWorldDetailColors)
            {
                if (world.IsAg && world.IsRi)
                {
                    penColor = brushColor = TravellerColors.Amber;
                }
                else if (world.IsAg)
                {
                    penColor = brushColor = TravellerColors.Green;
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

            penColorOut = penColor.IsEmpty ? Color.Empty : penColor;
            brushColorOut = brushColor.IsEmpty ? Color.Empty : brushColor;
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

    internal class HighlightWorldPattern
    {
        public enum Field
        {
            Starport,
            Size,
            Atmosphere,
            Hydrosphere,
            Population,
            Government,
            Law,
            Tech,
            Importance,
            Bases
        }


        public Field field = Field.Starport;
        public int? min = null;
        public int? max = null;
        public char[]? matches = null;

        public HighlightWorldPattern() { }

        private bool InRange(int value)
        {
            if (min.HasValue && value < min.Value)
                return false;
            if (max.HasValue && value > max.Value)
                return false;
            return true;
        }
        public bool Matches(World world)
        {
            if (matches != null)
            {
                var v = field switch
                {
                    Field.Starport => world.Starport.ToString(),
                    Field.Bases => world.Bases,
                    _ => throw new ApplicationException("Invalid pattern"),
                };
                return matches.Intersect(v.ToCharArray()).Count() > 0;
            }
            else
            {
                var v = field switch
                {
                    Field.Starport => "XEDCBA".IndexOf(world.Starport),
                    Field.Size => world.Size,
                    Field.Atmosphere => world.Atmosphere,
                    Field.Hydrosphere => world.Hydrographics,
                    Field.Population => world.PopulationExponent,
                    Field.Government => world.Government,
                    Field.Law => world.Law,
                    Field.Tech => world.TechLevel,
                    Field.Importance => SecondSurvey.Importance(world),
                    _ => throw new ApplicationException("Invalid pattern"),
                };
                return InRange(v);
            }
        }

        private static Regex HIGHLIGHT_BASIC_REGEX = new Regex(@"^([A-Z]+)(-?\d+|[0-9A-Z])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex HIGHLIGHT_MIN_REGEX = new Regex(@"^([A-Z]+)(-?\d+|[0-9A-Z])\+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex HIGHLIGHT_MAX_REGEX = new Regex(@"^([A-Z]+)(-?\d+|[0-9A-Z])\-$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex HIGHLIGHT_RANGE_REGEX = new Regex(@"^([A-Z]+)(-?\d+|[0-9A-Z])\-(-?\d+|[0-9A-Z])$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static Regex HIGHLIGHT_CHARS_REGEX = new Regex(@"^([A-Z]+):([A-Z]+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static bool ParseField(string s, ref Field f)
        {
            switch (s.ToLowerInvariant())
            {
                case "st": f = Field.Starport; return true;
                case "s": f = Field.Size; return true;
                case "a": f = Field.Atmosphere; return true;
                case "h": f = Field.Hydrosphere; return true;
                case "p": f = Field.Population; return true;
                case "g": f = Field.Government; return true;
                case "l": f = Field.Law; return true;
                case "t": f = Field.Tech; return true;
                case "ix": f = Field.Importance; return true;
                case "b": f = Field.Bases; return true;
                default: return false;
            }
        }

        private static bool ParseNumber(string s, out int n)
        {
            const string HEX = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
            if (s.Length == 1)
            {
                int i = HEX.IndexOf(s[0]);
                if (i > 0)
                {
                    n = i;
                    return true;
                }
            }
            if (Int32.TryParse(s, out n)) return true;
            return false;
        }

        public static HighlightWorldPattern? Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;

            HighlightWorldPattern p = new HighlightWorldPattern();

            Match m;
            if ((m = HIGHLIGHT_BASIC_REGEX.Match(s)).Success)
            {
                if (!ParseField(m.Groups[1].Value, ref p.field)) return null;
                if (!ParseNumber(m.Groups[2].Value, out int min)) return null;
                p.min = p.max = min;
                return p;
            }
            if ((m = HIGHLIGHT_MIN_REGEX.Match(s)).Success)
            {
                if (!ParseField(m.Groups[1].Value, ref p.field)) return null;
                if (!ParseNumber(m.Groups[2].Value, out int min)) return null;
                p.min = min;
                return p;
            }
            if ((m = HIGHLIGHT_MAX_REGEX.Match(s)).Success)
            {
                if (!ParseField(m.Groups[1].Value, ref p.field)) return null;
                if (!ParseNumber(m.Groups[2].Value, out int max)) return null;
                p.max = max;
                return p;
            }
            if ((m = HIGHLIGHT_RANGE_REGEX.Match(s)).Success)
            {
                if (!ParseField(m.Groups[1].Value, ref p.field)) return null;
                if (!ParseNumber(m.Groups[2].Value, out int min)) return null;
                if (!ParseNumber(m.Groups[3].Value, out int max)) return null;
                p.min = min;
                p.max = max;
                return p;
            }
            if ((m = HIGHLIGHT_CHARS_REGEX.Match(s)).Success)
            {
                if (!ParseField(m.Groups[1].Value, ref p.field)) return null;
                p.matches = m.Groups[2].Value.ToUpperInvariant().ToCharArray();
                return p;
            }
            return null;
        }
    }

    internal class FontCache : IDisposable
    {

        public FontCache(Stylesheet sheet)
        {
            this.sheet = sheet;
        }
        private Stylesheet sheet;

        private AbstractFont? wingdingFont;
        public AbstractFont WingdingFont => wingdingFont ??= sheet.wingdingFont.MakeFont();
        private AbstractFont? glyphFont;
        public AbstractFont GlyphFont => glyphFont ??= sheet.glyphFont.MakeFont();

        #region IDisposable Support
        private bool disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;
#if DISPOSABLE_RESOURCES
            if( disposing )
            {
                sectorNameFont?.Dispose();
                sectorNameFont = null;
                subsectorNameFont?.Dispose();
                subsectorNameFont = null;
                microPolityNameFont?.Dispose();
                microPolityNameFont = null;
                microPolityNameSmallFont?.Dispose();
                microPolityNameSmallFont = null;

                worldFont?.Dispose();
                worldFont = null;
                symbolFont?.Dispose();
                symbolFont = null;
                glyphFont?.Dispose();
                glyphFont = null;
                hexFont?.Dispose();
                hexFont = null;
                smallFont?.Dispose();
                smallFont = null;
                largeFont?.Dispose();
                largeFont = null;
                starportFont?.Dispose();
                starportFont = null;
            }
#endif
            disposed = true;
        }
        #endregion
    }
}
