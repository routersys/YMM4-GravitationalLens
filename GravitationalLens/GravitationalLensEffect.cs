using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;

namespace GravitationalLens;

[PluginDetails(AuthorName = "routersys")]
[VideoEffect(nameof(Texts.GravitationalLens), [VideoEffectCategories.Filtering], [nameof(Texts.TagGravitationalLens), nameof(Texts.TagBlackHole), nameof(Texts.TagDistortion)], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
public sealed class GravitationalLensEffect : VideoEffectBase
{
    public override string Label => Texts.GravitationalLens;

    public GravitationalLensEffect()
    {
        GravitationalLensTelemetry.EnsureStartedOnce();
        GravitationalLensUpdateNotifier.EnsureCheckedOnce();
    }

    [Display(GroupName = nameof(Texts.BasicGroup), Name = nameof(Texts.Amount), Description = nameof(Texts.AmountDescription), Order = 0, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation Amount { get; } = new Animation(100, 0, 100);

    [Display(GroupName = nameof(Texts.BlackHoleGroup), Name = nameof(Texts.PositionX), Description = nameof(Texts.PositionXDescription), Order = 10, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -GravitationalLensSettings.PositionSliderPixels, GravitationalLensSettings.PositionSliderPixels)]
    public Animation PositionX { get; } = new Animation(0, -GravitationalLensSettings.MaximumPositionPixels, GravitationalLensSettings.MaximumPositionPixels);

    [Display(GroupName = nameof(Texts.BlackHoleGroup), Name = nameof(Texts.PositionY), Description = nameof(Texts.PositionYDescription), Order = 11, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "px", -GravitationalLensSettings.PositionSliderPixels, GravitationalLensSettings.PositionSliderPixels)]
    public Animation PositionY { get; } = new Animation(0, -GravitationalLensSettings.MaximumPositionPixels, GravitationalLensSettings.MaximumPositionPixels);

    [Display(GroupName = nameof(Texts.BlackHoleGroup), Name = nameof(Texts.Distance), Description = nameof(Texts.DistanceDescription), Order = 12, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "×", GravitationalLensSettings.MinimumDistance, GravitationalLensSettings.MaximumDistance)]
    public Animation Distance { get; } = new Animation(100, GravitationalLensSettings.MinimumDistance, GravitationalLensSettings.MaximumDistance);

    [Display(GroupName = nameof(Texts.CameraGroup), Name = nameof(Texts.FieldOfView), Description = nameof(Texts.FieldOfViewDescription), Order = 20, ResourceType = typeof(Texts))]
    [AnimationSlider("F1", "°", GravitationalLensSettings.MinimumFieldOfViewDegrees, GravitationalLensSettings.MaximumFieldOfViewDegrees)]
    public Animation FieldOfView { get; } = new Animation(60, GravitationalLensSettings.MinimumFieldOfViewDegrees, GravitationalLensSettings.MaximumFieldOfViewDegrees);

    private IAnimatable[]? _animatables;

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        try
        {
            return new GravitationalLensEffectProcessor(devices, this);
        }
        catch (Exception exception)
        {
            GravitationalLensTelemetry.Report(exception);
            throw;
        }
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
        => _animatables ??= [Amount, PositionX, PositionY, Distance, FieldOfView];
}
