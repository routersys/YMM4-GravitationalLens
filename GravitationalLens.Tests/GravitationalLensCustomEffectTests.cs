using YukkuriMovieMaker.Commons;
using static GravitationalLens.GravitationalLensCustomEffect;

namespace GravitationalLens.Tests;

[Collection("Direct2D")]
public sealed class GravitationalLensCustomEffectTests
{
    const float MaxOffset = 4000f;

    static float Read(GravitationalLensCustomEffect effect, PropertyIndex index) => effect.GetFloatValue((int)index);

    static Rendering Render(IGraphicsDevicesAndContext devices, SourceImage source, Action<GravitationalLensCustomEffect> configure)
    {
        using var table = SourceImage.Solid(devices, GravitationalLensSettings.TableWidth, 1, Bgra.Opaque(0, 0, 0));
        using var effect = new GravitationalLensCustomEffect(devices);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, table.Bitmap, true);
        configure(effect);
        using var output = effect.Output;

        return Rendering.Capture(devices, output);
    }

    [Fact]
    public void TheEffectIsEnabledOnceCreated()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        Assert.True(effect.IsEnabled);
    }

    [Fact]
    public void ThePropertiesStartFromTheirDefaults()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        Assert.Equal(0f, Read(effect, PropertyIndex.Amount));
        Assert.Equal(0f, Read(effect, PropertyIndex.LensX));
        Assert.Equal(0f, Read(effect, PropertyIndex.LensY));
        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
        Assert.Equal(0f, Read(effect, PropertyIndex.ShadowAngle));
        Assert.Equal(0f, Read(effect, PropertyIndex.TableStart));
        Assert.Equal(1f, Read(effect, PropertyIndex.TableScale));
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, 0f)]
    public void TheAmountIsClampedToTheUnitRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.Amount = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.Amount));
    }

    [Theory]
    [InlineData(-MaxOffset - 1f, -MaxOffset)]
    [InlineData(-30f, -30f)]
    [InlineData(0f, 0f)]
    [InlineData(45.5f, 45.5f)]
    [InlineData(MaxOffset + 1f, MaxOffset)]
    [InlineData(float.PositiveInfinity, MaxOffset)]
    [InlineData(float.NegativeInfinity, -MaxOffset)]
    [InlineData(float.NaN, 0f)]
    public void TheLensOffsetIsClampedToTheInputPixelRange(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.LensX = value;
        effect.LensY = -value;

        Assert.Equal(expected, Read(effect, PropertyIndex.LensX));
        Assert.Equal(float.IsNaN(value) ? 0f : -expected, Read(effect, PropertyIndex.LensY));
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(3.7f)]
    public void TheHalfFieldTangentKeepsValuesInsideTheFieldOfViewLimits(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(value, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(float.NegativeInfinity)]
    public void TheHalfFieldTangentNeverFallsBelowTheNarrowestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(GravitationalLensSettings.MinimumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(4f)]
    [InlineData(1e5f)]
    [InlineData(float.PositiveInfinity)]
    public void TheHalfFieldTangentNeverExceedsTheWidestFieldOfView(float value)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.HalfFieldTangent = value;

        Assert.Equal(GravitationalLensSettings.MaximumHalfFieldTangent, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Fact]
    public void ANonNumberLeavesTheHalfFieldTangentAtItsDefault()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.HalfFieldTangent = float.NaN;

        Assert.Equal(1f, Read(effect, PropertyIndex.HalfFieldTangent));
    }

    [Theory]
    [InlineData(-0.1f, 0f)]
    [InlineData(0.7f, 0.7f)]
    [InlineData(float.NaN, 0f)]
    public void TheShadowAngleStaysBetweenZeroAndTheShadowOfTheNearestDistance(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.ShadowAngle = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.ShadowAngle));
    }

    [Fact]
    public void AShadowWiderThanTheNearestDistanceAllowsIsCutBack()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.ShadowAngle = 10f;

        Assert.Equal((float)GravitationalLensSettings.MaximumShadowAngle, Read(effect, PropertyIndex.ShadowAngle));
    }

    [Theory]
    [InlineData(-16.1f, -16.1f)]
    [InlineData(25f, 25f)]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    public void TheTableStartMustBeFinite(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.TableStart = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.TableStart));
    }

    [Theory]
    [InlineData(-3f, 0f)]
    [InlineData(118.3f, 118.3f)]
    [InlineData(1e9f, 1e9f)]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    public void TheTableScaleIsFiniteAndNeverNegative(float value, float expected)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var effect = new GravitationalLensCustomEffect(context);

        effect.TableScale = value;

        Assert.Equal(expected, Read(effect, PropertyIndex.TableScale));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(0.6f)]
    [InlineData(1f)]
    public void TheOutputBoundsEqualTheInput(float amount)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 40, 24, Bgra.Opaque(10, 20, 30));
        using var table = SourceImage.Solid(context, GravitationalLensSettings.TableWidth, 1, Bgra.Opaque(0, 0, 0));
        using var effect = new GravitationalLensCustomEffect(context);
        effect.SetInput(0, source.Bitmap, true);
        effect.SetInput(1, table.Bitmap, true);
        effect.Amount = amount;
        effect.LensX = 500f;
        using var output = effect.Output;

        var bounds = context.DeviceContext.GetImageLocalBounds(output);

        Assert.Equal((0f, 0f, 40f, 24f), (bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
    }

    [Fact]
    public void WithoutAnAmountTheInputPassesThroughUnchanged()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, 48, 32, (x, y) => new Bgra((byte)(x * 5), (byte)(y * 7), (byte)(x + y), (byte)(100 + x)));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 0f;
            effect.ShadowAngle = 0.3f;
        });

        Assert.True(rendered.SamePixelsAs(Rendering.Capture(context, source.Bitmap)));
    }

    [Fact]
    public void ThePixelsInsideTheShadowAreOpaqueBlack()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = SourceImage.Solid(context, 64, 64, Bgra.Opaque(40, 90, 200));

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = 0.5f;
            effect.ShadowAngle = 0.2f;
            effect.TableStart = -16f;
            effect.TableScale = 100f;
        });

        Assert.Equal(Bgra.Opaque(0, 0, 0), rendered[32, 32]);
        Assert.Equal(Bgra.Opaque(0, 0, 0), rendered[30, 34]);
    }

    [Fact]
    public void ALensFarOutsideTheImageLeavesAUniformImageUniform()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var color = Bgra.Opaque(40, 90, 200);
        using var source = SourceImage.Solid(context, 64, 64, color);

        var rendered = Render(context, source, effect =>
        {
            effect.Amount = 1f;
            effect.HalfFieldTangent = 0.5f;
            effect.ShadowAngle = 0.05f;
            effect.LensX = 3000f;
            effect.LensY = 3000f;
        });

        Assert.All(rendered.Coordinates(), point => Assert.Equal(color, rendered[point.X, point.Y]));
    }
}
