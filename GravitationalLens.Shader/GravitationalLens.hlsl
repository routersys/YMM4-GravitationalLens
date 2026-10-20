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
static const float TwoPi = 6.28318530717959;
static const float EdgeWidth = 1.5;
static const float SingleSampleLimit = 1.5;
static const float FourSampleLimit = 4.0;

float ReadTable(float index)
{
    return TableTexture.SampleLevel(TableSampler, float2((index + 0.5) / tableWidth, 0.5), 0).x;
}

float LookUp(float separation, out float slope)
{
    float position = clamp((log(max(separation, exp(tableStart))) - tableStart) * tableScale, 0.0, tableWidth - 1.0);
    float lower = min(floor(position), tableWidth - 2.0);
    float first = ReadTable(lower);
    float second = ReadTable(lower + 1.0);
    slope = (second - first) * tableScale;
    return lerp(first, second, position - lower);
}

float4 Trace(float2 position, float2 anchor, float4 uv0, float2 center, float2 lens, float3 lensDirection, float lensLength, float focal, out float stretch)
{
    float2 p = position - center;
    float2 c = lens - center;
    float2 delta = p - c;
    float3 crossVector = float3(focal * delta.y, -focal * delta.x, p.x * c.y - p.y * c.x);
    float crossLength = length(crossVector);
    float dotProduct = dot(p, c) + focal * focal;
    float viewAngle = atan2(crossLength, dotProduct);
    if (viewAngle <= shadowAngle + EdgeWidth / focal)
        stretch = (viewAngle <= shadowAngle) ? 0.0 : 5.0;
    else
        stretch = 0.0;

    if (viewAngle <= shadowAngle)
        return float4(0.0, 0.0, 0.0, 1.0);

    float separation = viewAngle - shadowAngle;
    float slope;
    float sourceAngle = LookUp(separation, slope);
    float folded = sourceAngle - TwoPi * floor((sourceAngle + Pi) / TwoPi);
    float3 viewVector = float3(p, focal);
    float viewLength = length(viewVector);
    float norm = viewLength * lensLength;
    float sine = crossLength / norm;
    float cosine = dotProduct / norm;
    float sineFolded;
    float cosineFolded;
    sincos(folded, sineFolded, cosineFolded);
    float sineDifference = sine * cosineFolded - cosine * sineFolded;
    float3 sourceDirection = (sineDifference * lensDirection + (sineFolded / viewLength) * viewVector) / sine;
    float2 sourcePosition = center + focal * sourceDirection.xy / max(sourceDirection.z, 1e-3);
    float2 clamped = clamp(sourcePosition, inputBounds.xy + 0.5, inputBounds.zw - 0.5);
    float2 uv = uv0.xy + (clamped - anchor) * uv0.zw;
    stretch = max(stretch, max(abs(slope) / separation, abs(sineFolded) / sine));
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
    float count = 1.0;
    float2 offset = float2(0.0, 0.0);
    [loop]
    for (int iteration = 0; iteration <= 16; iteration++)
    {
        float stretch;
        float4 color = Trace(scenePosition.xy + offset, scenePosition.xy, uv0, center, lens, lensDirection, lensLength, focal, stretch);
        if (iteration == 0)
        {
            lensed = color;
            if (stretch <= SingleSampleLimit)
                break;

            count = stretch > FourSampleLimit ? 4.0 : 2.0;
        }
        else
        {
            sum += color;
            if (iteration >= count * count)
            {
                lensed = sum / (count * count);
                break;
            }
        }

        float row = floor(iteration / count);
        float column = iteration - row * count;
        offset = (float2(column, row) + 0.5) / count - 0.5;
    }

    return lerp(source, lensed, amount);
}
