using System;
using System.Drawing;
using System.Globalization;

namespace Maps
{

    public static class Astrometrics
    {
        public const int SectorWidth = 32; // parsecs
        public const int SectorHeight = 40; // parsecs

        public const int SectorCentralHex = (SectorWidth / 2) * 100 + (SectorHeight / 2);

        public const int SubsectorWidth = 8; // parsecs
        public const int SubsectorHeight = 10; // parsecs

        // Parsecs are not square - there is horizontal overlap in a hex grid
        // width:height ratio for parsecs is cos(30)
        // (a subsector is 8:10 parsecs but 0.69:1 aspect ratio)
        public const float ParsecScaleX = 0.8660254037844387f; // Math.Cos(Math.PI / 6);
        public const float ParsecScaleY = 1.0f;

        // Reference (Core 0140)
        // Origin of the coordinate system, relative to the containing sector
        // ("Reference, Center of the Imperium" The Travellers' Digest 10)
        public static readonly Point ReferenceSector = new Point(0, 0);
        public static readonly Point ReferenceHex = new Point(01, 40);

        public static Point LocationToCoordinates(Location location)
        {
            return LocationToCoordinates(location.SectorLocation, location.HexLocation);
        }
        public static Point LocationToCoordinates(Point sector, Point hex)
        {
            int x = (sector.X - ReferenceSector.X) * SectorWidth + (hex.X - ReferenceHex.X);
            int y = (sector.Y - ReferenceSector.Y) * SectorHeight + (hex.Y - ReferenceHex.Y);

            return new Point(x, y);
        }
        public static Location CoordinatesToLocation(Point coordinates)
        {
            return CoordinatesToLocation(coordinates.X, coordinates.Y);
        }
        public static Location CoordinatesToLocation(int x, int y)
        {
            x += Astrometrics.ReferenceHex.X - 1;
            y += Astrometrics.ReferenceHex.Y - 1;

            Point sector = Point.Empty;
            Point hex = Point.Empty;

            sector.X = (x - (x < 0 ? Astrometrics.SectorWidth - 1 : 0)) / Astrometrics.SectorWidth;
            sector.Y = (y - (y < 0 ? Astrometrics.SectorHeight - 1 : 0)) / Astrometrics.SectorHeight;

            hex.X = x - (sector.X * Astrometrics.SectorWidth) + 1;
            hex.Y = y - (sector.Y * Astrometrics.SectorHeight) + 1;

            return new Location(sector, hex);
        }

        public static int HexDistance(Point hex1, Point hex2)
        {
            int dx = hex2.X - hex1.X;
            int dy = hex2.Y - hex1.Y;

            int adx = Math.Abs(dx);
            int ody = dy + (adx / 2);

            if ((hex1.X % 2 == 0) && (hex2.X % 2 != 0)) 
                ody += 1;

            return Math.Max(adx - ody, Math.Max(ody, adx));

        }

        public static PointF HexToCenter(Point point)
        {
            PointF pf = PointF.Empty;

            pf.X = point.X - 0.5f;
            pf.Y = point.Y - ((point.X % 2) != 0 ? 0.0f : 0.5f);

            return pf;
        }

        public static int HexNeighbor(int hex, int direction)
        {
            // return col,row of a neighboring hex (0..5, starting LL and going clockwise)

            int c = hex / 100;
            int r = hex % 100;

            switch (direction)
            {
                case 0: r += 1 - (c-- % 2); break;
                case 1: r -= (c-- % 2); break;
                case 2: r--; break;
                case 3: r -= (c++ % 2); break;
                case 4: r += 1 - (c++ % 2); break;
                case 5: r++; break;
            }

            return c * 100 + r;
        }
        public static Point HexNeighbor(Point coord, int direction)
        {
            int c = coord.X;
            int r = coord.Y;

            // NOTE: semantics of even/odd column handing are opposite of numbered hexes since this 
            // is Reference-centric (and reference is in an "odd number" hex 0140)
            switch (direction)
            {
                case 0: r += 1 - (c-- % 2 != 0 ? 0 : 1); break;
                case 1: r -= (c-- % 2 != 0 ? 0 : 1); break;
                case 2: r--; break;
                case 3: r -= (c++ % 2 != 0 ? 0 : 1); break;
                case 4: r += 1 - (c++ % 2 != 0 ? 0 : 1); break;
                case 5: r++; break;
            }

            return new Point(c, r);
        }

        public static string PointToHex(Point pt)
        {
            return (pt.X * 100 + pt.Y).ToString("0000", CultureInfo.InvariantCulture);
        }

        public static string IntToHex(int i)
        {
            return i.ToString("0000", CultureInfo.InvariantCulture);
        }

        public static Point HexToPoint(string s)
        {
            int hex;
            if (int.TryParse(s, out hex))
            {
                return new Point(hex / 100, hex % 100);
            }
            return new Point();
        }

        public static int HexToInt(string s)
        {
            return int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
    }
}