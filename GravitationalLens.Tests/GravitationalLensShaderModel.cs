namespace GravitationalLens.Tests;

internal static class GravitationalLensShaderModel
{
    const double EdgeWidth = 1.5;
    const double SingleSampleLimit = 1.5;
    const double FourSampleLimit = 4.0;
    const double ShadowEdgeFootprint = 2.0 * FourSampleLimit;
    const int SamplesPerAxis = 4;
    const double TwoPi = 2.0 * Math.PI;

    public static Bgra Shade(Func<int, int, Bgra> premultipliedSource, int x, int y, int width, int height, double lensX, double lensY, double halfFieldTangent, GravitationalLensTable table, double amount)
    {
        var own = premultipliedSource(x, y);
        if (amount <= 0.0)
            return own;

        var focal = 0.5 * width / halfFieldTangent;
        var center = (X: 0.5 * width, Y: 0.5 * height);
        var lens = (X: center.X + lensX, Y: center.Y + lensY);
        var position = (X: x + 0.5, Y: y + 0.5);
        var (lensed, stretch) = Trace(premultipliedSource, width, height, position, center, lens, focal, table);
        if (stretch > SingleSampleLimit)
        {
            var count = stretch > FourSampleLimit ? SamplesPerAxis : SamplesPerAxis / 2;
            var sum = new double[4];
            for (var row = 0; row < count; row++)
            {
                for (var column = 0; column < count; column++)
                {
                    var tap = (X: position.X + (column + 0.5) / count - 0.5, Y: position.Y + (row + 0.5) / count - 0.5);
                    var (color, _) = Trace(premultipliedSource, width, height, tap, center, lens, focal, table);
                    for (var channel = 0; channel < 4; channel++)
                        sum[channel] += color[channel];
                }
            }

            lensed = sum.Select(value => value / (count * count)).ToArray();
        }

        byte Mix(double from, double to) => (byte)Math.Clamp((int)Math.Round(from + (to - from) * amount), 0, 255);
        return new Bgra(Mix(own.Blue, lensed[0]), Mix(own.Green, lensed[1]), Mix(own.Red, lensed[2]), Mix(own.Alpha, lensed[3]));
    }

    static (double[] Color, double Stretch) Trace(Func<int, int, Bgra> source, int width, int height, (double X, double Y) position, (double X, double Y) center, (double X, double Y) lens, double focal, GravitationalLensTable table)
    {
        var p = (X: position.X - center.X, Y: position.Y - center.Y);
        var c = (X: lens.X - center.X, Y: lens.Y - center.Y);
        var delta = (X: p.X - c.X, Y: p.Y - c.Y);
        var crossX = focal * delta.Y;
        var crossY = -focal * delta.X;
        var crossZ = p.X * c.Y - p.Y * c.X;
        var viewAngle = Math.Atan2(Math.Sqrt(crossX * crossX + crossY * crossY + crossZ * crossZ), p.X * c.X + p.Y * c.Y + focal * focal);
        var stretch = viewAngle <= table.ShadowAngle + EdgeWidth / focal ? (viewAngle <= table.ShadowAngle ? 0.0 : ShadowEdgeFootprint) : 0.0;
        if (viewAngle <= table.ShadowAngle)
            return ([0.0, 0.0, 0.0, 255.0], stretch);

        var separation = viewAngle - table.ShadowAngle;
        var (sourceAngle, slope) = LookUp(table, separation);
        var folded = sourceAngle - TwoPi * Math.Floor((sourceAngle + Math.PI) / TwoPi);
        var viewLength = Math.Sqrt(p.X * p.X + p.Y * p.Y + focal * focal);
        var lensLength = Math.Sqrt(c.X * c.X + c.Y * c.Y + focal * focal);
        var wView = Math.Sin(folded) / Math.Sin(viewAngle);
        var wLens = Math.Sin(viewAngle - folded) / Math.Sin(viewAngle);
        var directionX = wLens * c.X / lensLength + wView * p.X / viewLength;
        var directionY = wLens * c.Y / lensLength + wView * p.Y / viewLength;
        var directionZ = wLens * focal / lensLength + wView * focal / viewLength;
        var depth = Math.Max(directionZ, 1e-3);
        var sample = (X: center.X + focal * directionX / depth, Y: center.Y + focal * directionY / depth);
        sample = (Math.Clamp(sample.X, 0.5, width - 0.5), Math.Clamp(sample.Y, 0.5, height - 0.5));
        stretch = Math.Max(stretch, Math.Max(Math.Abs(slope) / separation, Math.Abs(Math.Sin(folded)) / Math.Sin(viewAngle)));
        return (Bilinear(source, width, height, sample), stretch);
    }

    static (double Value, double Slope) LookUp(GravitationalLensTable table, double separation)
    {
        var scale = 1.0 / table.LogStep;
        var width = table.Values.Length;
        var position = Math.Clamp((Math.Log(Math.Max(separation, Math.Exp(table.LogStart))) - table.LogStart) * scale, 0.0, width - 1.0);
        var lower = Math.Min(Math.Floor(position), width - 2.0);
        var first = (double)table.Values[(int)lower];
        var second = (double)table.Values[(int)lower + 1];
        return (first + (second - first) * (position - lower), (second - first) * scale);
    }

    static double[] Bilinear(Func<int, int, Bgra> source, int width, int height, (double X, double Y) sample)
    {
        var left = sample.X - 0.5;
        var top = sample.Y - 0.5;
        var x0 = (int)Math.Floor(left);
        var y0 = (int)Math.Floor(top);
        var fx = left - x0;
        var fy = top - y0;
        var color = new double[4];
        for (var dy = 0; dy < 2; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                var weight = (dx == 0 ? 1.0 - fx : fx) * (dy == 0 ? 1.0 - fy : fy);
                var pixel = source(Math.Clamp(x0 + dx, 0, width - 1), Math.Clamp(y0 + dy, 0, height - 1));
                color[0] += weight * pixel.Blue;
                color[1] += weight * pixel.Green;
                color[2] += weight * pixel.Red;
                color[3] += weight * pixel.Alpha;
            }
        }

        return color;
    }
}
