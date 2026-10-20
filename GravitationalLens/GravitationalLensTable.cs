namespace GravitationalLens;

internal sealed class GravitationalLensTable
{
    private GravitationalLensTable(double distance, double shadowAngle, double logStart, double logStep, float[] values)
    {
        Distance = distance;
        ShadowAngle = shadowAngle;
        LogStart = logStart;
        LogStep = logStep;
        Values = values;
    }

    public double Distance { get; }

    public double ShadowAngle { get; }

    public double LogStart { get; }

    public double LogStep { get; }

    public float[] Values { get; }

    public static GravitationalLensTable Build(double distance)
    {
        var width = GravitationalLensSettings.TableWidth;
        var shadow = SchwarzschildOptics.GetShadowAngle(distance);
        var first = Math.Log(GravitationalLensSettings.MinimumSeparation);
        var last = Math.Log(Math.PI - shadow);
        var step = (last - first) / (width - 1);
        var values = new float[width];
        Parallel.For(0, width, index =>
        {
            var angle = Math.Min(shadow + Math.Exp(first + index * step), Math.PI);
            values[index] = (float)SchwarzschildOptics.GetSourceAngle(distance, angle);
        });

        return new GravitationalLensTable(distance, shadow, first, step, values);
    }

    public double GetSourceAngle(double viewAngle)
    {
        var separation = Math.Max(viewAngle - ShadowAngle, GravitationalLensSettings.MinimumSeparation);
        var position = Math.Clamp((Math.Log(separation) - LogStart) / LogStep, 0.0, Values.Length - 1);
        var lower = (int)Math.Floor(position);
        var upper = Math.Min(lower + 1, Values.Length - 1);
        var fraction = position - lower;
        return Values[lower] + (Values[upper] - Values[lower]) * fraction;
    }

    public float[] ToTexels()
    {
        var texels = new float[Values.Length * GravitationalLensSettings.TexelChannels];
        for (var index = 0; index < Values.Length; index++)
            texels[index * GravitationalLensSettings.TexelChannels] = Values[index];
        return texels;
    }
}
