Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
Texture2D TableTexture : register(t1);
SamplerState TableSampler : register(s1);

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float2 lensOffset : packoffset(c1.x);
    float halfFieldTangent : packoffset(c1.z);
    float amount : packoffset(c1.w);
    float shadowAngle : packoffset(c2.x);
    float tableStart : packoffset(c2.y);
    float tableScale : packoffset(c2.z);
    float tableWidth : packoffset(c2.w);
};

static const float Pi = 3.14159265358979;
static const float TwoPi = 2.0 * Pi;
static const float HalfPixel = 0.5;
static const float MinimumDepth = 1e-3;
static const float ShadowEdgeWidth = 1.5;
static const float SingleSampleFootprint = 1.5;
static const float FourSampleFootprint = 4.0;
static const float ShadowEdgeFootprint = 2.0 * FourSampleFootprint;
static const int SamplesPerAxis = 4;
static const int MaximumSamples = SamplesPerAxis * SamplesPerAxis;

float ReadTable(float index)
{
    return TableTexture.SampleLevel(TableSampler, float2((index + HalfPixel) / tableWidth, HalfPixel), 0).x;
}

float LookUp(float separation, out float slope)
{
    float position = clamp((log(separation) - tableStart) * tableScale, 0.0, tableWidth - 1.0);
    float lower = min(floor(position), tableWidth - 2.0);
    float first = ReadTable(lower);
    float second = ReadTable(lower + 1.0);
    slope = (second - first) * tableScale;
    return lerp(first, second, position - lower);
}

float4 Trace(float2 position, float2 anchor, float4 uv0, float2 center, float2 lens, float3 lensDirection, float lensLength, float focal, out float footprint)
{
    float2 viewOffset = position - center;
    float2 lensRelative = lens - center;
    float2 delta = viewOffset - lensRelative;
    float3 crossVector = float3(focal * delta.y, -focal * delta.x, viewOffset.x * lensRelative.y - viewOffset.y * lensRelative.x);
    float crossLength = length(crossVector);
    float dotProduct = dot(viewOffset, lensRelative) + focal * focal;
    float viewAngle = atan2(crossLength, dotProduct);
    bool inShadow = viewAngle <= shadowAngle;
    bool nearShadowEdge = viewAngle <= shadowAngle + ShadowEdgeWidth / focal;
    footprint = (nearShadowEdge && !inShadow) ? ShadowEdgeFootprint : 0.0;
    if (inShadow)
        return float4(0.0, 0.0, 0.0, 1.0);

    float separation = viewAngle - shadowAngle;
    float slope;
    float sourceAngle = LookUp(separation, slope);
    float folded = sourceAngle - TwoPi * floor((sourceAngle + Pi) / TwoPi);
    float3 viewVector = float3(viewOffset, focal);
    float viewLength = length(viewVector);
    float lengthProduct = viewLength * lensLength;
    float sineView = crossLength / lengthProduct;
    float cosineView = dotProduct / lengthProduct;
    float sineFolded;
    float cosineFolded;
    sincos(folded, sineFolded, cosineFolded);
    float sineDifference = sineView * cosineFolded - cosineView * sineFolded;
    float3 sourceDirection = (sineDifference * lensDirection + (sineFolded / viewLength) * viewVector) / sineView;
    float2 sourcePosition = center + focal * sourceDirection.xy / max(sourceDirection.z, MinimumDepth);
    float2 clamped = clamp(sourcePosition, inputBounds.xy + HalfPixel, inputBounds.zw - HalfPixel);
    float2 uv = uv0.xy + (clamped - anchor) * uv0.zw;
    footprint = max(footprint, max(abs(slope) / separation, abs(sineFolded) / sineView));
    return SourceTexture.SampleLevel(SourceSampler, uv, 0);
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (amount <= 0.0)
        return source;

    float2 center = 0.5 * (inputBounds.xy + inputBounds.zw);
    float2 lens = center + lensOffset;
    float focal = 0.5 * (inputBounds.z - inputBounds.x) / halfFieldTangent;
    float3 lensVector = float3(lens - center, focal);
    float lensLength = length(lensVector);
    float3 lensDirection = lensVector / lensLength;
    float4 lensed = float4(0.0, 0.0, 0.0, 0.0);
    float4 sum = float4(0.0, 0.0, 0.0, 0.0);
    float axisSamples = 1.0;
    float2 offset = float2(0.0, 0.0);
    [loop]
    for (int iteration = 0; iteration <= MaximumSamples; iteration++)
    {
        float footprint;
        float4 color = Trace(scenePosition.xy + offset, scenePosition.xy, uv0, center, lens, lensDirection, lensLength, focal, footprint);
        if (iteration == 0)
        {
            lensed = color;
            if (footprint <= SingleSampleFootprint)
                break;

            axisSamples = footprint > FourSampleFootprint ? SamplesPerAxis : SamplesPerAxis / 2;
        }
        else
        {
            sum += color;
            if (iteration >= axisSamples * axisSamples)
            {
                lensed = sum / (axisSamples * axisSamples);
                break;
            }
        }

        float row = floor(iteration / axisSamples);
        float column = iteration - row * axisSamples;
        offset = (float2(column, row) + 0.5) / axisSamples - 0.5;
    }

    return lerp(source, lensed, amount);
}
