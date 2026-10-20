namespace GravitationalLens.Tests;

public sealed class GravitationalLensSettingsTests
{
    [Theory]
    [InlineData(-5.0, 3.0)]
    [InlineData(0.0, 3.0)]
    [InlineData(2.999, 3.0)]
    [InlineData(3.0, 3.0)]
    [InlineData(100.0, 100.0)]
    [InlineData(1000.0, 1000.0)]
    [InlineData(1000.5, 1000.0)]
    [InlineData(double.PositiveInfinity, 1000.0)]
    [InlineData(double.NegativeInfinity, 3.0)]
    public void TheDistanceStaysBetweenThreeAndOneThousandSchwarzschildRadii(double value, double expected)
    {
        Assert.Equal(expected, GravitationalLensSettings.ClampDistance(value));
    }

    [Theory]
    [InlineData(60.0, 0.5773502691896257)]
    [InlineData(90.0, 1.0)]
    [InlineData(10.0, 0.08748866352592401)]
    [InlineData(150.0, 3.7320508075688776)]
    public void TheHalfFieldOfViewTangentFollowsTheAngle(double degrees, double expected)
    {
        Assert.Equal((float)expected, GravitationalLensSettings.GetHalfFieldOfViewTangent(degrees), 1e-6f);
    }

    [Theory]
    [InlineData(-30.0, 10.0)]
    [InlineData(0.0, 10.0)]
    [InlineData(9.9, 10.0)]
    [InlineData(151.0, 150.0)]
    [InlineData(1e9, 150.0)]
    public void TheFieldOfViewIsClampedBeforeTheTangentIsTaken(double degrees, double clamped)
    {
        Assert.Equal(GravitationalLensSettings.GetHalfFieldOfViewTangent(clamped), GravitationalLensSettings.GetHalfFieldOfViewTangent(degrees));
    }

    [Fact]
    public void TheTableHasTwoThousandAndFortyEightEntriesStartingAtOneTenMillionthOfARadian()
    {
        Assert.Equal(2048, GravitationalLensSettings.TableWidth);
        Assert.Equal(1e-7, GravitationalLensSettings.MinimumSeparation);
    }
}
