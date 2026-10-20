namespace GravitationalLens;

internal static class GravitationalLensSettings
{
    public const int TableWidth = 2048;
    public const int TexelChannels = 4;
    public const int TexelBytes = TexelChannels * sizeof(float);
    public const double MinimumSeparation = 1e-7;
    public const double MinimumDistance = 3.0;
    public const double MaximumDistance = 1000.0;
    public const double MinimumFieldOfViewDegrees = 10.0;
    public const double MaximumFieldOfViewDegrees = 150.0;
    public const double MaximumPositionPixels = 4000.0;
    public const double PositionSliderPixels = 1000.0;
    public const double RadiansPerDegree = Math.PI / 180.0;

    public static readonly double MaximumShadowAngle = SchwarzschildOptics.GetShadowAngle(MinimumDistance);
    public static readonly float MinimumHalfFieldTangent = GetHalfFieldOfViewTangent(MinimumFieldOfViewDegrees);
    public static readonly float MaximumHalfFieldTangent = GetHalfFieldOfViewTangent(MaximumFieldOfViewDegrees);

    public static double ClampDistance(double distance)
        => Math.Clamp(distance, MinimumDistance, MaximumDistance);

    public static float GetHalfFieldOfViewTangent(double fieldOfViewDegrees)
    {
        var field = Math.Clamp(fieldOfViewDegrees, MinimumFieldOfViewDegrees, MaximumFieldOfViewDegrees);
        return (float)Math.Tan(field * RadiansPerDegree / 2.0);
    }
}
