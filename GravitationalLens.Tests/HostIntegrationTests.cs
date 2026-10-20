using System.Windows;
using Telemetry;

namespace GravitationalLens.Tests;

public sealed class HostIntegrationTests
{
    [Fact]
    public void OutsideAWpfApplicationNoTelemetryIsStartedOrSent()
    {
        Assert.Null(Application.Current);

        GravitationalLensTelemetry.EnsureStartedOnce();
        GravitationalLensTelemetry.Report(new InvalidOperationException());

        Assert.Null(ProcessState.Read("DrainClaimed"));
        Assert.Null(ProcessState.Read("SentCount"));
    }

    [Fact]
    public void OutsideAWpfApplicationTheEffectCanStillBeCreated()
    {
        Assert.Null(Application.Current);

        var effect = new GravitationalLensEffect();

        Assert.Equal(Texts.GravitationalLens, effect.Label);
        Assert.Null(ProcessState.Read("DrainClaimed"));
    }
}
