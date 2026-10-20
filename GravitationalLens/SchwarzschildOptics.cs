namespace GravitationalLens;

internal static class SchwarzschildOptics
{
    private const int QuadratureOrder = 48;
    private const int MaximumNewtonIterations = 100;

    private static readonly double MachineEpsilon = Math.BitIncrement(1.0) - 1.0;
    private static readonly double RfTolerance = Math.Pow(4.0 * MachineEpsilon, 1.0 / 6.0);

    private static readonly (double Node, double Weight)[] Quadrature = BuildQuadrature();

    public static readonly double CriticalImpactParameter = 1.5 * Math.Sqrt(3.0);

    public static double GetShadowAngle(double distance)
        => Math.Asin(CriticalImpactParameter * Math.Sqrt(1.0 - 1.0 / distance) / distance);

    public static double GetImpactParameter(double distance, double viewAngle)
        => distance * Math.Sin(viewAngle) / Math.Sqrt(1.0 - 1.0 / distance);

    public static double GetSweepAngle(double distance, double viewAngle)
    {
        var impact = GetImpactParameter(distance, viewAngle);
        var outgoing = viewAngle >= Math.PI / 2.0;
        if (impact <= CriticalImpactParameter)
            return outgoing ? GetSweepWithoutTurningPoint(distance, impact) : double.PositiveInfinity;

        return GetSweepWithTurningPoint(distance, impact, outgoing);
    }

    public static double GetSourceAngle(double distance, double viewAngle)
        => Math.PI - GetSweepAngle(distance, viewAngle);

    public static double GetDeflection(double distance, double viewAngle)
        => GetSweepAngle(distance, viewAngle) - (Math.PI - viewAngle);

    private static double GetSweepWithTurningPoint(double distance, double impact, bool outgoing)
    {
        var roots = Roots.Create(impact);
        var observer = Math.Min(1.0 / distance, roots.Periapsis);
        var sign = outgoing ? -1.0 : 1.0;
        return 2.0 / Math.Sqrt(roots.OuterSpan) * (roots.IncompleteIntegral(0.0) + sign * roots.IncompleteIntegral(observer));
    }

    private static double GetSweepWithoutTurningPoint(double distance, double impact)
    {
        var constant = 1.0 / (impact * impact);
        var observer = 1.0 / distance;
        var total = 0.0;
        foreach (var (node, weight) in Quadrature)
        {
            var u = 0.5 * observer * (node + 1.0);
            total += weight / Math.Sqrt(constant - u * u + u * u * u);
        }

        return 0.5 * observer * total;
    }

    private readonly record struct Roots(double Highest, double Periapsis, double Lowest, double Gap, double OuterSpan, double InnerSpan)
    {
        public static Roots Create(double impact)
        {
            var third = Math.Asin(CriticalImpactParameter / impact) / 3.0;
            var half = Math.Sin(third);
            var sine = Math.Sin(2.0 * third);
            var cosine = Math.Cos(2.0 * third);
            var root = Math.Sqrt(3.0);
            return new Roots(
                1.0 - 4.0 / 3.0 * half * half,
                2.0 / 3.0 * half * half + sine / root,
                2.0 / 3.0 * half * half - sine / root,
                cosine - sine / root,
                cosine + sine / root,
                2.0 * sine / root);
        }

        public double IncompleteIntegral(double position)
        {
            var denominator = InnerSpan * (Highest - position);
            var sine2 = OuterSpan * (Periapsis - position) / denominator;
            var cosine2 = Gap * (position - Lowest) / denominator;
            return Math.Sqrt(Math.Max(sine2, 0.0)) * CarlsonRf(cosine2, Gap / OuterSpan + InnerSpan / OuterSpan * cosine2, 1.0);
        }
    }

    private static double CarlsonRf(double x, double y, double z)
    {
        double average, deltaX, deltaY, deltaZ;
        do
        {
            var rootX = Math.Sqrt(x);
            var rootY = Math.Sqrt(y);
            var rootZ = Math.Sqrt(z);
            var lambda = rootX * (rootY + rootZ) + rootY * rootZ;
            x = 0.25 * (x + lambda);
            y = 0.25 * (y + lambda);
            z = 0.25 * (z + lambda);
            average = (x + y + z) / 3.0;
            deltaX = (average - x) / average;
            deltaY = (average - y) / average;
            deltaZ = (average - z) / average;
        }
        while (Math.Max(Math.Abs(deltaX), Math.Max(Math.Abs(deltaY), Math.Abs(deltaZ))) > RfTolerance);

        var e2 = deltaX * deltaY - deltaZ * deltaZ;
        var e3 = deltaX * deltaY * deltaZ;
        return (1.0 + (e2 / 24.0 - 0.1 - 3.0 * e3 / 44.0) * e2 + e3 / 14.0) / Math.Sqrt(average);
    }

    private static (double Node, double Weight)[] BuildQuadrature()
    {
        var result = new (double, double)[QuadratureOrder];
        for (var index = 0; index < QuadratureOrder; index++)
        {
            var x = Math.Cos(Math.PI * (index + 0.75) / (QuadratureOrder + 0.5));
            var derivative = 1.0;
            for (var iteration = 0; iteration < MaximumNewtonIterations; iteration++)
            {
                double previous = 1.0, current = x;
                for (var degree = 2; degree <= QuadratureOrder; degree++)
                {
                    var next = ((2.0 * degree - 1.0) * x * current - (degree - 1.0) * previous) / degree;
                    previous = current;
                    current = next;
                }

                derivative = QuadratureOrder * (x * current - previous) / (x * x - 1.0);
                var step = current / derivative;
                x -= step;
                if (Math.Abs(step) < MachineEpsilon)
                    break;
            }

            result[index] = (x, 2.0 / ((1.0 - x * x) * derivative * derivative));
        }

        return result;
    }
}
