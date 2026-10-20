using System.Collections.Immutable;
using System.Globalization;
using System.Numerics;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Player.Video;

namespace GravitationalLens.Tests;

[Collection("Direct2D")]
public sealed class GravitationalLensEffectProcessorTests
{
    const int Width = 128;
    const int Height = 96;
    const int Length = 30;
    const int Tolerance = 3;
    const int AllowedMismatches = Width * Height / 150;

    static Bgra Gradient(int x, int y)
        => Bgra.Opaque((byte)(30 + x * 150 / Width), (byte)(30 + y * 150 / Height), (byte)(40 + (x + y) * 90 / (Width + Height)));

    static Bgra Spot(int x, int y)
        => (x - Width / 2) * (x - Width / 2) + (y - Height / 2) * (y - Height / 2) <= 16 ? Bgra.Opaque(255, 255, 255) : Bgra.Opaque(20, 20, 20);

    static Bgra UpperThirdTransparent(int x, int y) => y < Height / 3 ? Bgra.Transparent : Gradient(x, y);

    static GravitationalLensEffect Strong(double amount = 100d)
    {
        var effect = new GravitationalLensEffect();
        effect.Amount.Values[0].Value = amount;
        effect.Distance.Values[0].Value = 20d;
        return effect;
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}"""))!;

    static Rendering RenderFrame(IGraphicsDevicesAndContext devices, IVideoEffectProcessor processor, int frame)
    {
        processor.Update(EffectDescriptions.At(frame, Length));
        return Rendering.Capture(devices, processor.Output);
    }

    sealed class Model
    {
        readonly GravitationalLensTable table;
        readonly double lensX;
        readonly double lensY;
        readonly double halfFieldTangent;
        readonly double amount;

        public Model(GravitationalLensEffect effect, int frame)
        {
            double Value(Animation animation) => animation.GetValue(frame, Length, EffectDescriptions.Fps);
            table = GravitationalLensTable.Build(GravitationalLensSettings.ClampDistance(Value(effect.Distance)));
            lensX = Value(effect.PositionX);
            lensY = Value(effect.PositionY);
            halfFieldTangent = GravitationalLensSettings.GetHalfFieldOfViewTangent(Value(effect.FieldOfView));
            amount = Value(effect.Amount) / 100.0;
        }

        public GravitationalLensTable Table => table;

        public Bgra Expected(Func<int, int, Bgra> source, int x, int y)
            => GravitationalLensShaderModel.Shade((px, py) => source(px, py).Premultiplied(), x, y, Width, Height, lensX, lensY, halfFieldTangent, table, amount);
    }

    static int Mismatches(Rendering rendering, Model model, Func<int, int, Bgra> source)
        => rendering.Coordinates().Count(point =>
        {
            var expected = model.Expected(source, point.X, point.Y);
            var actual = rendering[point.X, point.Y];
            return Math.Abs(expected.Blue - actual.Blue) > Tolerance
                || Math.Abs(expected.Green - actual.Green) > Tolerance
                || Math.Abs(expected.Red - actual.Red) > Tolerance
                || expected.Alpha != actual.Alpha;
        });

    static void AssertFollowsModel(Rendering rendering, Model model, Func<int, int, Bgra> source, string where)
    {
        var mismatches = Mismatches(rendering, model, source);

        Assert.True(mismatches <= AllowedMismatches, $"{where}: {mismatches} pixels differ from the model of the shader");
    }

    static int ChangedPixels(Rendering rendering, SourceImage source)
        => rendering.Coordinates().Count(point => rendering[point.X, point.Y] != source[point.X, point.Y]);

    static double FocalLength(double fieldOfView) => 0.5 * Width / GravitationalLensSettings.GetHalfFieldOfViewTangent(fieldOfView);

    [Fact]
    public void TheProcessorOnlyAddsTheControllerToTheDrawDescription()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = Strong().CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var description = EffectDescriptions.At(0, Length);

        var draw = processor.Update(description);

        Assert.Equal(description.DrawDescription with { Controllers = draw.Controllers }, draw);
        Assert.Single(draw.Controllers);
    }

    [Fact]
    public void TheControllerSitsOnTheBlackHoleAndBelongsToTheEffect()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        effect.PositionX.Values[0].Value = 14d;
        effect.PositionY.Values[0].Value = -11d;
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        var controller = Assert.IsType<VideoEffectController>(Assert.Single(draw.Controllers));
        Assert.Same(effect, controller.Owner);
        var point = Assert.Single(controller.Points);
        Assert.Equal(new Vector3(Width / 2f + 14f, Height / 2f - 11f, 0f), point.Position);
    }

    [Fact]
    public void DraggingTheControllerMovesTheBlackHoleOnTheNextFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var point = Assert.Single(Assert.Single(processor.Update(EffectDescriptions.At(0, Length)).Controllers).Points);

        point.OnDrag!(new ControlPointDragEventArgs(new Vector3(9f, -6f, 0f), ModifierKeys.None));

        Assert.Equal(9d, effect.PositionX.GetValue(0, Length, EffectDescriptions.Fps));
        Assert.Equal(-6d, effect.PositionY.GetValue(0, Length, EffectDescriptions.Fps));
        var moved = Assert.Single(Assert.Single(processor.Update(EffectDescriptions.At(0, Length)).Controllers).Points);
        Assert.Equal(new Vector3(Width / 2f + 9f, Height / 2f - 6f, 0f), moved.Position);
        AssertFollowsModel(Rendering.Capture(context, processor.Output), new Model(effect, 0), Gradient, "after the drag");
    }

    [Fact]
    public void TheControllerFollowsAnAnimatedPosition()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        effect.PositionX.CopyFrom(Linear(-30d, 30d));
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = Assert.Single(Assert.Single(processor.Update(EffectDescriptions.At(0, Length)).Controllers).Points);
        var last = Assert.Single(Assert.Single(processor.Update(EffectDescriptions.At(Length - 1, Length)).Controllers).Points);

        Assert.Equal(Width / 2f - 30f, first.Position.X, 1e-3f);
        Assert.Equal(Width / 2f + (float)effect.PositionX.GetValue(Length - 1, Length, EffectDescriptions.Fps), last.Position.X, 1e-3f);
        Assert.True(last.Position.X > first.Position.X + 50f);
    }

    [Fact]
    public void TheControllerStaysWithoutAnAmount()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = Strong(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        Assert.Single(Assert.Single(draw.Controllers).Points);
    }

    [Fact]
    public void TheControllersOfEarlierEffectsAreKeptInFront()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        var effect = Strong();
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);
        var earlier = new VideoEffectController(new GravitationalLensEffect(), [new ControllerPoint(Vector3.Zero)]);
        var description = EffectDescriptions.At(0, Length);
        description = description with { DrawDescription = description.DrawDescription with { Controllers = ImmutableList.Create<VideoEffectController>(earlier) } };

        var draw = processor.Update(description);

        Assert.Equal(2, draw.Controllers.Count);
        Assert.Same(earlier, draw.Controllers[0]);
        Assert.Same(effect, draw.Controllers[1].Owner);
    }

    [Fact]
    public void WithoutAnInputNoControllerIsAdded()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Strong().CreateVideoEffect(context);

        var draw = processor.Update(EffectDescriptions.At(0, Length));

        Assert.Empty(draw.Controllers);
    }

    [Fact]
    public void WithoutAmountTheProcessorPassesTheImageThrough()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = Strong(0d).CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal((0, 0, Width, Height), (rendering.Left, rendering.Top, rendering.Width, rendering.Height));
        Assert.All(rendering.Coordinates(), point => Assert.Equal(source[point.X, point.Y], rendering[point.X, point.Y]));
    }

    [Fact]
    public void EveryPixelFollowsTheModelOfTheShader()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsModel(rendering, new Model(effect, 0), Gradient, "frame 0");
        Assert.True(ChangedPixels(rendering, source) > Width * Height / 4);
    }

    [Fact]
    public void TheShadowIsOpaqueBlackAndAsWideAsThePhotonSphereShadow()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        Assert.Equal(Bgra.Opaque(0, 0, 0), rendering[Width / 2, Height / 2]);
        var black = Enumerable.Range(0, Width).Where(x => rendering[x, Height / 2] == Bgra.Opaque(0, 0, 0)).ToArray();
        var expectedRadius = FocalLength(60.0) * Math.Tan(SchwarzschildOptics.GetShadowAngle(20.0));
        Assert.InRange(black.Length / 2.0, expectedRadius - 1.5, expectedRadius + 1.5);
        Assert.Equal(black.Max() - black.Min() + 1, black.Length);
    }

    [Fact]
    public void ABrightSourceBehindTheLensAppearsAsARingAtTheEinsteinAngle()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Spot);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        var table = new Model(effect, 0).Table;
        double low = 2.0 * table.ShadowAngle, high = 0.5;
        for (var iteration = 0; iteration < 80; iteration++)
        {
            var middle = 0.5 * (low + high);
            if (table.GetSourceAngle(middle) < 0.0)
                low = middle;
            else
                high = middle;
        }

        var expectedRadius = FocalLength(60.0) * Math.Tan(0.5 * (low + high));
        var shadowRadius = FocalLength(60.0) * Math.Tan(table.ShadowAngle);
        var brightest = Enumerable.Range(Width / 2 + (int)shadowRadius + 3, Width / 2 - (int)shadowRadius - 4).MaxBy(x => rendering[x, Height / 2].Red);
        Assert.InRange(brightest - Width / 2.0 + 0.5, expectedRadius - 2.5, expectedRadius + 2.5);
        Assert.True(rendering[brightest, Height / 2].Red > 150);
        Assert.True(rendering[Width / 2 + (int)expectedRadius + 14, Height / 2].Red < 100);
    }

    [Fact]
    public void ReturningToAFrameReproducesItExactly()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        effect.Distance.CopyFrom(Linear(8d, 60d));
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var first = RenderFrame(context, processor, 5);
        RenderFrame(context, processor, 20);
        var again = RenderFrame(context, processor, 5);

        Assert.True(first.SamePixelsAs(again));
    }

    [Fact]
    public void AnimatedDistanceIsReadAtEachFrame()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        effect.Distance.CopyFrom(Linear(10d, 80d));
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var start = RenderFrame(context, processor, 0);
        var end = RenderFrame(context, processor, Length - 1);

        Assert.False(start.SamePixelsAs(end));
        AssertFollowsModel(start, new Model(effect, 0), Gradient, "start");
        AssertFollowsModel(end, new Model(effect, Length - 1), Gradient, "end");
    }

    public static readonly TheoryData<string, Action<GravitationalLensEffect>> Changes = new()
    {
        { nameof(GravitationalLensEffect.Amount), effect => effect.Amount.Values[0].Value = 40d },
        { nameof(GravitationalLensEffect.PositionX), effect => effect.PositionX.Values[0].Value = 14d },
        { nameof(GravitationalLensEffect.PositionY), effect => effect.PositionY.Values[0].Value = -11d },
        { nameof(GravitationalLensEffect.Distance), effect => effect.Distance.Values[0].Value = 9d },
        { nameof(GravitationalLensEffect.FieldOfView), effect => effect.FieldOfView.Values[0].Value = 100d },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void EverySettingChangedAfterTheFirstFrameReachesTheShader(string setting, Action<GravitationalLensEffect> change)
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var before = RenderFrame(context, processor, 3);
        change(effect);
        var after = RenderFrame(context, processor, 3);

        Assert.False(before.SamePixelsAs(after), setting);
        AssertFollowsModel(after, new Model(effect, 3), Gradient, setting);
    }

    [Fact]
    public void ReducingTheAmountToZeroRestoresTheSource()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var shown = RenderFrame(context, processor, 0);
        effect.Amount.Values[0].Value = 0d;
        var hidden = RenderFrame(context, processor, 0);

        Assert.False(shown.SamePixelsAs(hidden));
        Assert.All(hidden.Coordinates(), point => Assert.Equal(source[point.X, point.Y], hidden[point.X, point.Y]));
    }

    [Fact]
    public void TransparentPartsOfTheSourceAreLensedLikeAnyOtherColor()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong();
        using var source = new SourceImage(context, Width, Height, UpperThirdTransparent);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsModel(rendering, new Model(effect, 0), UpperThirdTransparent, "transparent");
        Assert.Contains(rendering.Coordinates(), point => rendering[point.X, point.Y].Alpha == 0);
        Assert.Equal(byte.MaxValue, rendering[Width / 2, Height / 2].Alpha);
    }

    [Fact]
    public void TheAmountBlendsLinearlyBetweenTheSourceAndTheLensedImage()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        var effect = Strong(50d);
        using var source = new SourceImage(context, Width, Height, Gradient);
        using var processor = effect.CreateVideoEffect(context);
        processor.SetInput(source.Bitmap);

        var rendering = RenderFrame(context, processor, 0);

        AssertFollowsModel(rendering, new Model(effect, 0), Gradient, "half amount");
    }

    [Fact]
    public void AFarAwayLensBendsTheLightLessThanANearOne()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var source = new SourceImage(context, Width, Height, Gradient);
        var near = Strong();
        var far = Strong();
        far.Distance.Values[0].Value = 900d;
        using var nearProcessor = near.CreateVideoEffect(context);
        using var farProcessor = far.CreateVideoEffect(context);
        nearProcessor.SetInput(source.Bitmap);
        farProcessor.SetInput(source.Bitmap);

        var nearChanged = ChangedPixels(RenderFrame(context, nearProcessor, 0), source);
        var farChanged = ChangedPixels(RenderFrame(context, farProcessor, 0), source);

        Assert.True(farChanged < nearChanged, $"{farChanged} < {nearChanged}");
    }

    [Fact]
    public void AFailureWhileUpdatingIsNotSwallowed()
    {
        using var devices = new GraphicsDevices();
        using var context = devices.CreateContext();
        using var processor = Strong().CreateVideoEffect(context);

        Assert.ThrowsAny<Exception>(() => processor.Update(null!));
    }

    [Fact]
    public void AFailureWhileCreatingTheProcessorIsNotSwallowed()
    {
        Assert.ThrowsAny<Exception>(() => Strong().CreateVideoEffect(null!));
    }
}
