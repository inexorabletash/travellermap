#nullable enable
using Maps.Graphics;
using Maps.Rendering;
using Maps.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Web;

namespace Maps.API
{
    internal class JumpMapHandler : ImageHandlerBase
    {
        protected override DataResponder GetResponder(HttpContext context) => new Responder(context);

        private class Responder : ImageResponder
        {
            public Responder(HttpContext context) : base(context) { }
            public override void Process(ResourceManager resourceManager)
            {
                //
                // Jump
                //
                int jump = GetIntOption("jump", 6).Clamp(0, 20);

                //
                // Content & Coordinates
                //
                Selector selector;
                Location loc;
                if (Context.Request.HttpMethod == "POST")
                {
                    Sector sector;
                    bool lint = GetBoolOption("lint", defaultValue: false);
                    Func<ErrorLogger.Record, bool>? filter = null;
                    if (lint)
                    {
                        bool hide_uwp = GetBoolOption("hide-uwp", defaultValue: false);
                        bool hide_tl = GetBoolOption("hide-tl", defaultValue: false);
                        filter = (ErrorLogger.Record record) =>
                        {
                            if (hide_uwp && record.message.StartsWith("UWP")) return false;
                            if (hide_tl && record.message.StartsWith("UWP: TL")) return false;
                            return true;
                        };
                    }

                    ErrorLogger errors = new ErrorLogger(filter);
                    sector = GetPostedSector(Context.Request, errors) ??
                        throw new HttpError(400, "Bad Request", "Either file or data must be supplied in the POST data.");
                    if (lint && !errors.Empty)
                        throw new HttpError(400, "Bad Request", errors.ToString());

                    int hex = GetIntOption("hex", Astrometrics.SectorCentralHex);
                    loc = new Location(new Point(0, 0), hex);
                    selector = new HexSectorSelector(resourceManager, sector, loc.Hex, jump);
                }
                else
                {
                    // NOTE: This (re)initializes a static data structure used for 
                    // resolving names into sector locations, so needs to be run
                    // before any other objects (e.g. Worlds) are loaded.
                    SectorMap.Milieu map = SectorMap.ForMilieu(resourceManager, GetStringOption("milieu"));

                    if (HasOption("sector") && HasOption("hex"))
                    {
                        string sectorName = GetStringOption("sector")!;
                        int hex = GetIntOption("hex", 0);
                        Sector sector = map.FromName(sectorName) ??
                            throw new HttpError(404, "Not Found", $"The specified sector '{sectorName}' was not found.");

                        loc = new Location(sector.Location, hex);
                    }
                    else if (HasLocation())
                    {
                        loc = GetLocation();
                    }
                    else
                    {
                        loc = Location.Empty;
                    }
                    selector = new HexSelector(map, resourceManager, loc, jump);
                }


                //
                // Scale
                //
                double scale = GetDoubleOption("scale", 64).Clamp(MinScale, MaxScale);

                //
                // Options & Style
                //
                MapOptions options = MapOptions.BordersMajor | MapOptions.BordersMinor | MapOptions.ForceHexes;
                Style style = Style.Poster;
                ParseOptions(ref options, ref style);

                //
                // Border
                //
                bool border = GetBoolOption("border", defaultValue: true);

                //
                // Clip
                //
                bool clip = GetBoolOption("clip", defaultValue: true);

                // Hex Rotation
                int hrot = GetIntOption("hrotation", defaultValue: 0);

                //
                // What to render
                //

                RectangleF tileRect = new RectangleF();

                Point coords = Astrometrics.LocationToCoordinates(loc);
                tileRect.X = coords.X - jump - 1;
                tileRect.Width = jump + 1 + jump;
                tileRect.Y = coords.Y - jump - 1;
                tileRect.Height = jump + 1 + jump;

                // Account for jagged hexes
                tileRect.Y += (coords.X % 2 == 0) ? 0 : 0.5f;
                tileRect.Inflate(0.35f, 0.15f);

                Size tileSize = new Size((int)Math.Floor(tileRect.Width * scale * Astrometrics.ParsecScaleX), (int)Math.Floor(tileRect.Height * scale * Astrometrics.ParsecScaleY));


                // Construct clipping path
                List<Point> clipPath = new List<Point>(jump * 6 + 1);
                Point cur = coords;
                for (int i = 0; i < jump; ++i)
                {
                    // Move J parsecs to the upper-left (start of border path logic)
                    cur = Astrometrics.HexNeighbor(cur, 1);
                }
                clipPath.Add(cur);
                for (int dir = 0; dir < 6; ++dir)
                {
                    for (int i = 0; i < jump; ++i)
                    {
                        cur = Astrometrics.HexNeighbor(cur, (dir + 3) % 6); // Clockwise from upper left
                        clipPath.Add(cur);
                    }
                }

                Stylesheet styles = new Stylesheet(scale, options, style);

                // If any names are showing, show them all
                if (styles.worldDetails.HasFlag(WorldDetails.KeyNames))
                    styles.worldDetails |= WorldDetails.AllNames;

                // Compute path
                RenderUtil.HexEdges(styles.hexStyle == HexStyle.Square ? PathUtil.PathType.Square : PathUtil.PathType.Hex,
                    out float[] edgeX, out float[] edgeY);
                PathUtil.ComputeBorderPath(clipPath, edgeX, edgeY, out PointF[] boundingPathCoords, out byte[] boundingPathTypes);

                AbstractMatrix transform = AbstractMatrix.Identity;
                if (hrot != 0)
                    ApplyHexRotation(hrot, styles, ref tileSize, ref transform);

                RenderContext ctx = new RenderContext(resourceManager, selector, tileRect, scale, options, styles, tileSize)
                {
                    DrawBorder = border,
                    ClipOutsectorBorders = true,

                    // TODO: Widen path to allow for single-pixel border
                    ClipPath = clip ? new AbstractPath(boundingPathCoords, boundingPathTypes) : null
                };
                ProduceResponse(Context, "Jump Map", ctx, tileSize, transform, transparent: clip);
            }
        }
    }
}
