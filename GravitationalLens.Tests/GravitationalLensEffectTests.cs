using System.ComponentModel.DataAnnotations;
using System.Reflection;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Json;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Project;

namespace GravitationalLens.Tests;

public sealed class GravitationalLensEffectTests
{
    static PropertyInfo Property(string name) => typeof(GravitationalLensEffect).GetProperty(name)!;

    static T Attribute<T>(string property) where T : Attribute => Property(property).GetCustomAttribute<T>()!;

    static Animation[] Animations(GravitationalLensEffect effect)
        => [effect.Amount, effect.PositionX, effect.PositionY, effect.Distance, effect.FieldOfView];

    [Theory]
    [InlineData(nameof(GravitationalLensEffect.Amount), 100d, 0d, 100d)]
    [InlineData(nameof(GravitationalLensEffect.PositionX), 0d, -4000d, 4000d)]
    [InlineData(nameof(GravitationalLensEffect.PositionY), 0d, -4000d, 4000d)]
    [InlineData(nameof(GravitationalLensEffect.Distance), 100d, 3d, 1000d)]
    [InlineData(nameof(GravitationalLensEffect.FieldOfView), 60d, 10d, 150d)]
    public void AnimatedParametersStartFromTheirDefaultsWithinTheirRange(string name, double defaultValue, double minimum, double maximum)
    {
        var effect = new GravitationalLensEffect();

        var animation = (Animation)Property(name).GetValue(effect)!;

        Assert.Equal(defaultValue, animation.DefaultValue);
        Assert.Equal(minimum, animation.MinValue);
        Assert.Equal(maximum, animation.MaxValue);
        Assert.Equal(defaultValue, animation.GetValue(0, 1, EffectDescriptions.Fps));
    }

    [Fact]
    public void TheRangesMatchTheLimitsTheModelCovers()
    {
        var effect = new GravitationalLensEffect();

        Assert.Equal(GravitationalLensSettings.MinimumDistance, effect.Distance.MinValue);
        Assert.Equal(GravitationalLensSettings.MaximumDistance, effect.Distance.MaxValue);
        Assert.Equal(GravitationalLensSettings.MinimumFieldOfViewDegrees, effect.FieldOfView.MinValue);
        Assert.Equal(GravitationalLensSettings.MaximumFieldOfViewDegrees, effect.FieldOfView.MaxValue);
        Assert.Equal(-GravitationalLensSettings.MaximumPositionPixels, effect.PositionX.MinValue);
        Assert.Equal(GravitationalLensSettings.MaximumPositionPixels, effect.PositionY.MaxValue);
    }

    [Fact]
    public void TheAnimatedParametersAreListedOnceAndReused()
    {
        var effect = new GravitationalLensEffect();
        var method = typeof(GravitationalLensEffect).GetMethod("GetAnimatables", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var first = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;
        var second = (IEnumerable<IAnimatable>)method.Invoke(effect, null)!;

        Assert.Same(first, second);
        Assert.Equal(Animations(effect), first);
    }

    [Fact]
    public void TheLabelIsTheLocalizedEffectName()
    {
        Assert.Equal(Texts.GravitationalLens, new GravitationalLensEffect().Label);
    }

    [Fact]
    public void TheFiveNumericParametersReceiveTheAnimationParameters()
    {
        var effect = new GravitationalLensEffect();

        effect.SetAnimationParameters(120, EffectDescriptions.Fps);

        Assert.Equal(5, Animations(effect).Length);
        Assert.All(Animations(effect), animation => Assert.Equal(120, animation.Length));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void NoExoFilterIsWrittenForAviUtl(int keyFrameIndex)
    {
        var effect = new GravitationalLensEffect();

        var description = new ExoOutputDescription(new VideoInfo(), string.Empty, new AviUtlDirectories(string.Empty, string.Empty));

        Assert.Empty(effect.CreateExoVideoFilters(keyFrameIndex, description));
    }

    [Fact]
    public void TheAuthorIsDeclared()
    {
        var details = typeof(GravitationalLensEffect).GetCustomAttribute<PluginDetailsAttribute>()!;

        Assert.Equal("routersys", details.AuthorName);
    }

    [Fact]
    public void TheEffectIsRegisteredForFilteringWithoutAviUtlSupport()
    {
        var attribute = typeof(GravitationalLensEffect).GetCustomAttribute<VideoEffectAttribute>()!;

        Assert.Equal(nameof(Texts.GravitationalLens), attribute.Name);
        Assert.Equal([VideoEffectCategories.Filtering], attribute.Categories);
        Assert.Equal([nameof(Texts.TagGravitationalLens), nameof(Texts.TagBlackHole), nameof(Texts.TagDistortion)], attribute.Keywords);
        Assert.False(attribute.IsAviUtlSupported);
        Assert.True(attribute.IsEffectItemSupported);
        Assert.Equal(typeof(Texts), attribute.ResourceType);
        Assert.Equal(Texts.GravitationalLens, attribute.GetName());
    }

    [Theory]
    [InlineData(nameof(GravitationalLensEffect.Amount), nameof(Texts.BasicGroup), nameof(Texts.Amount), nameof(Texts.AmountDescription), 0)]
    [InlineData(nameof(GravitationalLensEffect.PositionX), nameof(Texts.BlackHoleGroup), nameof(Texts.PositionX), nameof(Texts.PositionXDescription), 10)]
    [InlineData(nameof(GravitationalLensEffect.PositionY), nameof(Texts.BlackHoleGroup), nameof(Texts.PositionY), nameof(Texts.PositionYDescription), 11)]
    [InlineData(nameof(GravitationalLensEffect.Distance), nameof(Texts.BlackHoleGroup), nameof(Texts.Distance), nameof(Texts.DistanceDescription), 12)]
    [InlineData(nameof(GravitationalLensEffect.FieldOfView), nameof(Texts.CameraGroup), nameof(Texts.FieldOfView), nameof(Texts.FieldOfViewDescription), 20)]
    public void EveryParameterIsDisplayedInItsGroupInOrder(string property, string group, string name, string description, int order)
    {
        var display = Attribute<DisplayAttribute>(property);

        Assert.Equal(group, display.GroupName);
        Assert.Equal(name, display.Name);
        Assert.Equal(description, display.Description);
        Assert.Equal(order, display.Order);
        Assert.Equal(typeof(Texts), display.ResourceType);
    }

    [Theory]
    [InlineData(nameof(GravitationalLensEffect.Amount), "F1", "%", 0d, 100d)]
    [InlineData(nameof(GravitationalLensEffect.PositionX), "F1", "px", -1000d, 1000d)]
    [InlineData(nameof(GravitationalLensEffect.PositionY), "F1", "px", -1000d, 1000d)]
    [InlineData(nameof(GravitationalLensEffect.Distance), "F1", "×", 3d, 1000d)]
    [InlineData(nameof(GravitationalLensEffect.FieldOfView), "F1", "°", 10d, 150d)]
    public void AnimatedParametersAreEditedWithAnimationSliders(string property, string format, string unit, double minimum, double maximum)
    {
        var slider = Attribute<AnimationSliderAttribute>(property);

        Assert.Equal(format, slider.StringFormat);
        Assert.Equal(unit, slider.UnitText);
        Assert.Equal(minimum, slider.DefaultMin);
        Assert.Equal(maximum, slider.DefaultMax);
    }

    [Fact]
    public void EverySettingSurvivesAProjectRoundTrip()
    {
        var effect = new GravitationalLensEffect();
        var values = new[] { 55d, -120d, 340d, 17.5d, 90d };
        foreach (var (animation, value) in Animations(effect).Zip(values))
            animation.Values[0].Value = value;

        var clone = Json.GetClone(effect)!;

        Assert.NotSame(effect, clone);
        Assert.Equal(values, Animations(clone).Select(animation => animation.GetValue(0, 1, EffectDescriptions.Fps)));
    }
}
