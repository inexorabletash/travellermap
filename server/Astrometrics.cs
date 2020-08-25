#nullable enable
using System;
using System.Drawing;
using System.Globalization;

namespace Maps
{
    internal static class Astrometrics
    {
        public const int SectorWidth = 32; // parsecs
        public const int SectorHeight = 40; // parsecs

        public const int SectorCentralHex = (SectorWidth / 2) * 100 + (SectorHeight / 2);

        public const int SubsectorWidth = 8; // parsecs
        public const int SubsectorHeight = 10; // parsecs

        // Parsecs are not square - there is horizontal overlap in a hex grid
        // width:height ratio for parsecs is cos(30deg)
        // (a subsector is 8:10 parsecs but 0.69:1 aspect ratio)
        public const float ParsecScaleX = 0.8660254037844387f; // Math.Cos(Math.PI / 6);
        public const float ParsecScaleY = 1.0f;

        // Reference (Core 0140)
        // Origin of the coordinate system, relative to the containing sector
        // ("Reference, Center of the Imperium" The Travellers' Digest 10)
        public static Point ReferenceSector => new Point(0, 0);
        public static Hex ReferenceHex => new Hex(01, 40);

        public static Hex SectorCenter => new Hex(SectorWidth / 2, SectorHeight / 2);

        public static Point LocationToCoordinates(Location location) => LocationToCoordinates(location.Sector, location.Hex);

        public static Point LocationToCoordinates(Point sector, Hex hex)
        {
            int x = (sector.X - ReferenceSector.X) * SectorWidth + (hex.X - ReferenceHex.X);
            int y = (sector.Y - ReferenceSector.Y) * SectorHeight + (hex.Y - ReferenceHex.Y);

            return new Point(x, y);
        }

        public static Location CoordinatesToLocation(Point coordinates) => CoordinatesToLocation(coordinates.X, coordinates.Y);

        public static Location CoordinatesToLocation(int x, int y)
        {
            x += Astrometrics.ReferenceHex.X - 1;
            y += Astrometrics.ReferenceHex.Y - 1;

            Point sector = Point.Empty;
            Hex hex = Hex.Empty;

            sector.X = (x - (x < 0 ? Astrometrics.SectorWidth - 1 : 0)) / Astrometrics.SectorWidth;
            sector.Y = (y - (y < 0 ? Astrometrics.SectorHeight - 1 : 0)) / Astrometrics.SectorHeight;

            hex.X = (byte)(x - (sector.X * Astrometrics.SectorWidth) + 1);
            hex.Y = (byte)(y - (sector.Y * Astrometrics.SectorHeight) + 1);

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

        public static Hex HexNeighbor(Hex hex, int direction)
        {
            // return col,row of a neighboring hex (0..5, starting LL and going clockwise)

            int c = hex.X;
            int r = hex.Y;

            switch (direction)
            {
                case 0: r += 1 - (c-- % 2); break;
                case 1: r -= (c-- % 2); break;
                case 2: r--; break;
                case 3: r -= (c++ % 2); break;
                case 4: r += 1 - (c++ % 2); break;
                case 5: r++; break;
            }

            return new Hex((byte)c, (byte)r);
        }
        public static Point HexNeighbor(Point coord, int direction)
        {
            int c = coord.X;
            int r = coord.Y;

            // NOTE: semantics of even/odd column handing are opposite of numbered hexes since this 
            // is Reference-centric (and Reference is in an "odd number" hex 0140)
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
    }

    internal struct Hex : IEquatable<Hex>
    {
        public Hex(int hex) { X = (byte)(hex / 100); Y = (byte)(hex % 100); }
        public Hex(byte x, byte y) { X = x; Y = y; }
        public Hex(Hex other) { X = other.X; Y = other.Y; }
        public Hex(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                X = Y = 0;
            }
            else if (s.Length == 4 && byte.TryParse(s.Substring(0, 2), out byte x) && byte.TryParse(s.Substring(2, 2), out byte y))
            {
                X = x; Y = y;
            }
            else
            {
                throw new Exception($"'{s}' is not a valid hex");
            }
        }

        public byte X { get; set; }
        public byte Y { get; set; }

        public bool IsEmpty => (X == 0 && Y == 0);
        public bool IsValid => (1 <= X && X <= Astrometrics.SectorWidth && 1 <= Y && Y <= Astrometrics.SectorHeight);
        public static bool operator <(Hex a, Hex b) => (a.X < b.X) || (a.X == b.X && a.Y < b.Y);
        public static bool operator >(Hex a, Hex b) => (a.X > b.X) || (a.X == b.X && a.Y > b.Y);

        public int ToInt() => X * 100 + Y;

        public override string ToString() => ToInt().ToString("0000", CultureInfo.InvariantCulture);

        public string ToSubsectorString() => (
                ((X - 1) % Astrometrics.SubsectorWidth + 1) * 100 +
                ((Y - 1) % Astrometrics.SubsectorHeight + 1)
            ).ToString("0000", CultureInfo.InvariantCulture);

        public override bool Equals(object other) => other is Hex hex && Equals(hex);
        public bool Equals(Hex other) => other.X == X && other.Y == Y;

        public static bool operator ==(Hex a, Hex b) => a.Equals(b);
        public static bool operator !=(Hex a, Hex b) => !a.Equals(b);
        public override int GetHashCode() => ToInt();

        public static readonly Hex Empty = new Hex();
    }
}