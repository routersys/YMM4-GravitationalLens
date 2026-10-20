namespace GravitationalLens.Tests;

public sealed class SchwarzschildOpticsTests
{
    const double Critical = 2.598076211353316;

    static double ViewAngleOf(double distance, double impact)
        => Math.Asin(impact * Math.Sqrt(1.0 - 1.0 / distance) / distance);

    static double Simpson(Func<double, double> function, double from, double to, int intervals)
    {
        var step = (to - from) / intervals;
        var sum = function(from) + function(to);
        for (var index = 1; index < intervals; index++)
            sum += function(from + index * step) * (index % 2 == 1 ? 4.0 : 2.0);
        return sum * step / 3.0;
    }

    static double Bisect(Func<double, double> function, double low, double high)
    {
        var lowSign = Math.Sign(function(low));
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var middle = 0.5 * (low + high);
            if (Math.Sign(function(middle)) == lowSign)
                low = middle;
            else
                high = middle;
        }

        return 0.5 * (low + high);
    }

    static double OracleSweep(double distance, double viewAngle)
    {
        var impact = distance * Math.Sin(viewAngle) / Math.Sqrt(1.0 - 1.0 / distance);
        var constant = 1.0 / (impact * impact);
        Func<double, double> cubic = u => u * u * u - u * u + constant;
        var lowest = Bisect(cubic, -1.0 / 3.0, 0.0);
        var periapsis = Bisect(cubic, 0.0, 2.0 / 3.0);
        var highest = Bisect(cubic, 2.0 / 3.0, 1.0);
        double Integrand(double t)
        {
            var u = periapsis * (1.0 - t * t);
            return 2.0 * Math.Sqrt(periapsis) / Math.Sqrt((highest - u) * (u - lowest));
        }

        var observerFraction = Math.Sqrt(Math.Max(1.0 - 1.0 / distance / periapsis, 0.0));
        var sign = viewAngle < Math.PI / 2.0 ? 1.0 : -1.0;
        return Simpson(Integrand, 0.0, 1.0, 40000) + sign * Simpson(Integrand, 0.0, observerFraction, 40000);
    }

    [Fact]
    public void TheCriticalImpactParameterIsThePhotonSphereValue()
    {
        Assert.Equal(Critical, SchwarzschildOptics.CriticalImpactParameter, 1e-14);
    }

    [Fact]
    public void TheShadowAtThreeSchwarzschildRadiiSubtendsFortyFiveDegrees()
    {
        Assert.Equal(Math.PI / 4.0, SchwarzschildOptics.GetShadowAngle(3.0), 1e-12);
    }

    [Theory]
    [InlineData(1e4)]
    [InlineData(1e6)]
    [InlineData(1e8)]
    public void TheShadowSizeApproachesTheCriticalImpactParameterOverTheDistance(double distance)
    {
        var scaled = SchwarzschildOptics.GetShadowAngle(distance) * distance;

        Assert.Equal(Critical, scaled, Critical * 2.0 / distance);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(10.0)]
    [InlineData(300.0)]
    public void TheShadowEdgeIsWhereTheImpactParameterEqualsTheCriticalValue(double distance)
    {
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);

        Assert.Equal(Critical, SchwarzschildOptics.GetImpactParameter(distance, shadow), 1e-12);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(40.0)]
    public void RaysInsideTheShadowAreCaptured(double distance)
    {
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);

        Assert.True(double.IsPositiveInfinity(SchwarzschildOptics.GetSweepAngle(distance, 0.5 * shadow)));
        Assert.True(double.IsPositiveInfinity(SchwarzschildOptics.GetSweepAngle(distance, shadow * (1.0 - 1e-9))));
        Assert.False(double.IsPositiveInfinity(SchwarzschildOptics.GetSweepAngle(distance, shadow * (1.0 + 1e-9))));
    }

    [Theory]
    [InlineData(200.0)]
    [InlineData(1000.0)]
    public void TheDeflectionFollowsTheWeakFieldSeries(double impact)
    {
        const double distance = 1e10;
        var series = 2.0 / impact
            + 15.0 * Math.PI / 16.0 / Math.Pow(impact, 2.0)
            + 16.0 / 3.0 / Math.Pow(impact, 3.0)
            + 3465.0 * Math.PI / 1024.0 / Math.Pow(impact, 4.0);

        var deflection = SchwarzschildOptics.GetDeflection(distance, ViewAngleOf(distance, impact));

        Assert.Equal(series, deflection, 1e-8);
    }

    [Theory]
    [InlineData(1e-4)]
    [InlineData(1e-6)]
    [InlineData(1e-8)]
    public void TheDeflectionNearThePhotonSphereFollowsTheLogarithmicLaw(double excess)
    {
        const double distance = 1e9;
        var impact = Critical * (1.0 + excess);
        var asymptote = -Math.Log(excess) + Math.Log(216.0 * (7.0 - 4.0 * Math.Sqrt(3.0))) - Math.PI;

        var deflection = SchwarzschildOptics.GetDeflection(distance, ViewAngleOf(distance, impact));

        Assert.Equal(asymptote, deflection, 40.0 * excess * Math.Abs(Math.Log(excess)) + 1e-6);
    }

    [Theory]
    [InlineData(3.0, 1.2)]
    [InlineData(3.0, 1.9)]
    [InlineData(10.0, 0.5)]
    [InlineData(10.0, 1.4)]
    [InlineData(10.0, 1.9)]
    [InlineData(10.0, 2.8)]
    [InlineData(100.0, 0.05)]
    [InlineData(100.0, 0.3)]
    [InlineData(100.0, 2.0)]
    [InlineData(1000.0, 0.01)]
    [InlineData(1000.0, 0.1)]
    [InlineData(1000.0, 3.0)]
    public void TheClosedFormSweepMatchesDirectIntegration(double distance, double viewAngle)
    {
        var impact = SchwarzschildOptics.GetImpactParameter(distance, viewAngle);
        Assert.True(impact > 1.2 * Critical, $"impact {impact}");

        Assert.Equal(OracleSweep(distance, viewAngle), SchwarzschildOptics.GetSweepAngle(distance, viewAngle), 1e-9);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(10.0)]
    [InlineData(500.0)]
    public void TheSweepIsContinuousAtARightAngle(double distance)
    {
        var below = SchwarzschildOptics.GetSweepAngle(distance, Math.PI / 2.0 - 1e-9);
        var above = SchwarzschildOptics.GetSweepAngle(distance, Math.PI / 2.0 + 1e-9);

        Assert.Equal(below, above, 1e-7);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(10.0)]
    [InlineData(500.0)]
    public void TheSweepIsContinuousWhereTheRayStopsHavingATurningPoint(double distance)
    {
        var edge = Math.PI - SchwarzschildOptics.GetShadowAngle(distance);
        var inside = SchwarzschildOptics.GetSweepAngle(distance, edge + 1e-8);
        var outside = SchwarzschildOptics.GetSweepAngle(distance, edge - 1e-8);

        Assert.Equal(inside, outside, 2e-6);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(25.0)]
    [InlineData(800.0)]
    public void ARayLookingStraightAwayHasNoSweep(double distance)
    {
        Assert.Equal(0.0, SchwarzschildOptics.GetSweepAngle(distance, Math.PI), 1e-12);
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(7.5)]
    [InlineData(60.0)]
    [InlineData(1000.0)]
    public void TheSourceAngleGrowsWithTheViewAngle(double distance)
    {
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);
        var previous = double.NegativeInfinity;
        for (var index = 1; index <= 4000; index++)
        {
            var angle = shadow + (Math.PI - shadow) * Math.Pow(index / 4000.0, 3.0);
            var source = SchwarzschildOptics.GetSourceAngle(distance, angle);

            Assert.True(source > previous, $"angle {angle}: {source} <= {previous}");
            previous = source;
        }
    }

    [Theory]
    [InlineData(1e3)]
    [InlineData(1e4)]
    public void TheEinsteinRingMatchesTheDirectIntegration(double distance)
    {
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);
        var ring = Bisect(angle => SchwarzschildOptics.GetSourceAngle(distance, angle), 2.0 * shadow, 0.5);
        var oracle = Bisect(angle => Math.PI - OracleSweep(distance, angle), 2.0 * shadow, 0.5);

        Assert.Equal(oracle, ring, oracle * 1e-9);
    }

    [Fact]
    public void TheEinsteinRingFollowsTheWeakFieldSeriesAtAnEffectivelyInfiniteDistance()
    {
        const double distance = 1e8;
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);
        var ring = Bisect(angle => SchwarzschildOptics.GetSourceAngle(distance, angle), 2.0 * shadow, 0.5);
        var expected = Bisect(angle =>
        {
            var impact = SchwarzschildOptics.GetImpactParameter(distance, angle);
            return angle - (2.0 / impact + 15.0 * Math.PI / 16.0 / (impact * impact) + 16.0 / 3.0 / (impact * impact * impact));
        }, 2.0 * shadow, 0.5);

        Assert.Equal(expected, ring, expected * 1e-7);
    }

    [Theory]
    [InlineData(1e3)]
    [InlineData(1e6)]
    public void TheEinsteinRingApproachesTheSquareRootOfTwoOverTheDistance(double distance)
    {
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);
        var ring = Bisect(angle => SchwarzschildOptics.GetSourceAngle(distance, angle), 2.0 * shadow, 0.5);

        Assert.Equal(Math.Sqrt(2.0 / distance), ring, ring * 1.6 / Math.Sqrt(2.0 * distance));
    }

    [Fact]
    public void TheTableCoversTheWholeRangeOfViewAngles()
    {
        foreach (var distance in new[] { 3.0, 12.0, 250.0, 1000.0 })
        {
            var table = GravitationalLensTable.Build(distance);

            Assert.Equal(GravitationalLensSettings.TableWidth, table.Values.Length);
            Assert.Equal(SchwarzschildOptics.GetShadowAngle(distance), table.ShadowAngle, 1e-15);
            Assert.Equal(Math.PI, table.Values[^1], 1e-6);
            Assert.True(table.Values[0] < -8.0);
        }
    }

    [Theory]
    [InlineData(3.0)]
    [InlineData(12.0)]
    [InlineData(250.0)]
    [InlineData(1000.0)]
    public void TheTableInterpolatesTheExactSourceAngle(double distance)
    {
        var table = GravitationalLensTable.Build(distance);
        var shadow = table.ShadowAngle;
        var worst = 0.0;
        for (var index = 1; index < 3000; index++)
        {
            var separation = GravitationalLensSettings.MinimumSeparation * Math.Pow((Math.PI - shadow) / GravitationalLensSettings.MinimumSeparation, index / 3000.0);
            var angle = Math.Min(shadow + separation, Math.PI);
            worst = Math.Max(worst, Math.Abs(table.GetSourceAngle(angle) - SchwarzschildOptics.GetSourceAngle(distance, angle)));
        }

        Assert.True(worst < 5e-5, $"worst {worst}");
    }

    [Fact]
    public void TheTexelsCarryTheSourceAngleInTheFirstChannel()
    {
        var table = GravitationalLensTable.Build(40.0);
        var texels = table.ToTexels();

        Assert.Equal(GravitationalLensSettings.TableWidth * 4, texels.Length);
        for (var index = 0; index < table.Values.Length; index += 97)
        {
            Assert.Equal(table.Values[index], texels[index * 4]);
            Assert.Equal(0f, texels[index * 4 + 1]);
            Assert.Equal(0f, texels[index * 4 + 2]);
            Assert.Equal(0f, texels[index * 4 + 3]);
        }
    }

    [Fact]
    public void TheLookupBelowTheSmallestSeparationUsesTheFirstEntry()
    {
        var table = GravitationalLensTable.Build(60.0);

        Assert.Equal(table.Values[0], table.GetSourceAngle(table.ShadowAngle), 1e-6);
        Assert.Equal(table.Values[0], table.GetSourceAngle(table.ShadowAngle - 0.1), 1e-6);
    }
}
