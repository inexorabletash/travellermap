#nullable enable
using System;
using System.Drawing;

namespace Maps.Utilities
{
    internal static class ColorUtil
    {
        public static void RGBtoXYZ(int r, int g, int b, out double x, out double y, out double z)
        {
            double rl = (double)r / 255.0;
            double gl = (double)g / 255.0;
            double bl = (double)b / 255.0;

            double sr = (rl > 0.04045) ? Math.Pow((rl + 0.055) / (1 + 0.055), 2.2) : (rl / 12.92);
            double sg = (gl > 0.04045) ? Math.Pow((gl + 0.055) / (1 + 0.055), 2.2) : (gl / 12.92);
            double sb = (bl > 0.04045) ? Math.Pow((bl + 0.055) / (1 + 0.055), 2.2) : (bl / 12.92);

            x = sr * 0.4124 + sg * 0.3576 + sb * 0.1805;
            y = sr * 0.2126 + sg * 0.7152 + sb * 0.0722;
            z = sr * 0.0193 + sg * 0.1192 + sb * 0.9505;
        }

        private static double Fxyz(double t) => ((t > 0.008856) ? Math.Pow(t, (1.0 / 3.0)) : (7.787 * t + 16.0 / 116.0));

        public static void XYZtoLab(double x, double y, double z, out double l, out double a, out double b)
        {
            const double D65X = 0.9505, D65Y = 1.0, D65Z = 1.0890;
            l = 116.0 * Fxyz(y / D65Y) - 16;
            a = 500.0 * (Fxyz(x / D65X) - Fxyz(y / D65Y));
            b = 200.0 * (Fxyz(y / D65Y) - Fxyz(z / D65Z));
        }

        public static double DeltaE76(double l1, double a1, double b1, double l2, double a2, double b2)
        {
            double c1 = l1 - l2, c2 = a1 - a2, c3 = b1 - b2;
            return Math.Sqrt(c1 * c1 + c2 * c2 + c3 * c3);
        }

        // TODO: Replace this with a "sufficient contrast?" test.
        public static bool NoticeableDifference(Color a, Color b)
        {
            const double JND = 13;// 2.3;

            RGBtoXYZ(a.R, a.G, a.B, out double ax, out double ay, out double az);
            RGBtoXYZ(b.R, b.G, b.B, out double bx, out double by, out double bz);

            XYZtoLab(ax, ay, az, out double al, out double aa, out double ab);
            XYZtoLab(bx, by, bz, out double bl, out double ba, out double bb);

            return DeltaE76(al, aa, ab, bl, ba, bb) > JND;
        }

        public static Color ParseColor(string value)
        {
            try
            {
                return ColorTranslator.FromHtml(value);
            }
            catch (Exception)
            {
                throw new Exception($"'{value}' is not a valid color name.");
            }
        }
    }
}