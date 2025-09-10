#ifndef MATHCOMMON
#define MATHCOMMON

static const float PI = 3.14159265359;
static const float TAU = 6.28318530718;

inline float sqr(float x)
{
    return x * x;
}

inline float2 sqr(float2 x)
{
    return x * x;
}

inline float3 sqr(float3 x)
{
    return x * x;
}

inline float4 sqr(float4 x)
{
    return x * x;
}

// Converts a number to between 0-1 range.
float N01(float n)
{
    return 0.5 * n + 0.5;
}

// Converts a number to between -1 to 1 range.
float N11(float n)
{
    return saturate(n) * 2 - 1;
}

// Normalize -1 to 1 with saturate.
inline float N11SAT(float n)
{
    return saturate(n) * 2 - 1;
}

#endif