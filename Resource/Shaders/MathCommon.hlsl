#ifndef MATHCOMMON
#define MATHCOMMON

// --------------------------------------------
// Notes:
//   - Constants should be `static const` so the compiler inlines them directly
//     instead of wasting a register or uniform slot.
//
//   - Small math helpers (`sqr`, `N01`, etc.) should be `inline` so they get
//     expanded at compile-time, no call overhead. (HLSL doesn’t actually do
//     real function calls, it just inlines unless you force `[noinline]`).
//
//   - Keep `[noinline]` only for big functions you want to keep out of the
//     giant inlined blob (helps with shader compile times).
//
//   - Using this split keeps compile-time sane while still running fast.
// --------------------------------------------

static const float PI = 3.14159265359;
static const float TAU = 6.28318530718;

inline float Sanitize(float v)
{
    return (isnan(v) || isinf(v)) ? 0.0 : v;
}

inline float sqr(float x) { return x * x; }
inline float2 sqr(float2 x) { return x * x; }
inline float3 sqr(float3 x) { return x * x; }
inline float4 sqr(float4 x) { return x * x; }

// Converts a number to [0,1]
inline float N01(float n)
{
    return 0.5 * n + 0.5;
}

// Converts a number to [-1,1]
inline float N11(float n)
{
    return saturate(n) * 2 - 1;
}

// Normalize to [-1,1] with saturate
inline float N11SAT(float n)
{
    return saturate(n) * 2 - 1;
}

// Quantize [0..1] value into N discrete bins (0..N-1)
inline uint QuantizeN(float value, uint bins)
{
    return (uint) (saturate(value) * bins); // clamp ensures never out of range
}

// Reverse quantize map bin index (0..N-1)
inline float ReverseQuantizeN(uint index, uint bins)
{
    return (index + 0.5f) / bins;
}

#endif