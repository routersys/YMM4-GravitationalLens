namespace GravitationalLens;

internal static class ShaderResourceUri
{
    public static Uri Get(string shaderName) => new($"pack://application:,,,/GravitationalLens;component/Resources/Shader/{shaderName}.cso", UriKind.Absolute);
}
