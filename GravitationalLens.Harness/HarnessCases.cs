using System.Globalization;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Json;

namespace GravitationalLens.Harness;

internal static class HarnessCases
{
    public static IEnumerable<(string Name, GravitationalLensEffect Effect, IReadOnlyList<int> Frames)> All()
    {
        yield return ("default", Create(), [0]);
        yield return ("distance-animated-frames", Create(effect => effect.Distance.CopyFrom(Linear(10d, 300d))), [0, 100, 200, 299]);
        yield return ("position-x-animated-frames", Create(effect => effect.PositionX.CopyFrom(Linear(-300d, 300d))), [0, 100, 200, 299]);
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0), [0]);
        yield return ("amount-50", Create(effect => effect.Amount.Values[0].Value = 50), [0]);
        yield return ("position-x-minus-250", Create(effect => effect.PositionX.Values[0].Value = -250), [0]);
        yield return ("position-x-250", Create(effect => effect.PositionX.Values[0].Value = 250), [0]);
        yield return ("position-y-minus-150", Create(effect => effect.PositionY.Values[0].Value = -150), [0]);
        yield return ("position-y-150", Create(effect => effect.PositionY.Values[0].Value = 150), [0]);
        yield return ("distance-5", Create(effect => effect.Distance.Values[0].Value = 5), [0]);
        yield return ("distance-12", Create(effect => effect.Distance.Values[0].Value = 12), [0]);
        yield return ("distance-30", Create(effect => effect.Distance.Values[0].Value = 30), [0]);
        yield return ("distance-300", Create(effect => effect.Distance.Values[0].Value = 300), [0]);
        yield return ("distance-1000", Create(effect => effect.Distance.Values[0].Value = 1000), [0]);
        yield return ("field-of-view-25", Create(effect => effect.FieldOfView.Values[0].Value = 25), [0]);
        yield return ("field-of-view-110", Create(effect => effect.FieldOfView.Values[0].Value = 110), [0]);
    }

    public static IEnumerable<(string Name, Func<GravitationalLensEffect> Create, Action<GravitationalLensEffect> Change, int Frame)> Transitions()
    {
        yield return ("amount-100-to-0", () => Create(), effect => effect.Amount.Values[0].Value = 0, 0);
        yield return ("amount-0-to-100", () => Create(effect => effect.Amount.Values[0].Value = 0), effect => effect.Amount.Values[0].Value = 100, 0);
        yield return ("amount-100-to-50", () => Create(), effect => effect.Amount.Values[0].Value = 50, 0);
        yield return ("position-x-0-to-200", () => Create(), effect => effect.PositionX.Values[0].Value = 200, 0);
        yield return ("position-y-0-to-minus-120", () => Create(), effect => effect.PositionY.Values[0].Value = -120, 0);
        yield return ("distance-100-to-20", () => Create(), effect => effect.Distance.Values[0].Value = 20, 0);
        yield return ("distance-100-to-600", () => Create(), effect => effect.Distance.Values[0].Value = 600, 0);
        yield return ("field-of-view-60-to-100", () => Create(), effect => effect.FieldOfView.Values[0].Value = 100, 0);
    }

    public static IEnumerable<(string Name, GravitationalLensEffect Effect)> Benchmarks()
    {
        yield return ("default", Create());
        yield return ("distance-animated", Create(effect => effect.Distance.CopyFrom(Linear(10d, 300d))));
        yield return ("position-x-animated", Create(effect => effect.PositionX.CopyFrom(Linear(-300d, 300d))));
        yield return ("amount-0", Create(effect => effect.Amount.Values[0].Value = 0));
    }

    static Animation Linear(double from, double to)
        => Json.LoadFromText<Animation>(string.Create(CultureInfo.InvariantCulture, $$"""{"AnimationType":"直線移動","Values":[{"Value":{{from}}},{"Value":{{to}}}]}""")) ?? throw new HarnessException("アニメーションを読み込めません。");

    public static GravitationalLensEffect Create(Action<GravitationalLensEffect>? configure = null)
    {
        var effect = new GravitationalLensEffect();
        configure?.Invoke(effect);
        return effect;
    }
}
