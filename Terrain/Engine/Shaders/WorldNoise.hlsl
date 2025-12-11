#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "Lib/PerlinNoise.hlsl"
#include "ChunkFunctions.hlsl"

float SampleBiomeNoise(float3 p)
{
    // Low enough to produce large continent-like regions
    float n = fbm3D(p * 0.0002, 4); // ← much lower frequency
    return n; // still 0–1
}

int GetBiomeID(float3 p)
{
    float y = p.y;

    if (y < -15.0)
        return 0; // sand
    else if (y < 10.0)
        return 1; // grass
    else if (y < 40.0)
        return 2; // alpine
    else
        return 3; // mountain
}


inline float Union(float a, float b)
{
    return min(a, b);
}

float SmoothUnion(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

inline float Intersect(float a, float b)
{
    return max(a, b);
}

inline float Subtract(float a, float b)
{
    return max(a, -b);
}

float fbmRidged(float3 p)
{
    float r = 1.0 - abs(fbm3D(p, 4));
    return r * r * r;
}

inline float Turbulence(float3 p)
{
    return abs(N11(fbm3D(p, 5)));
}


float GroundVolumetric(float3 p)
{
    // Large solid ground volume around y=0
    float d = abs(p.y) - 20.0;

    // Low-frequency noise creates big rolling landforms
    float n = N11(fbm3D(p * 0.002, 5));
    d -= n * 8.0; // soften/strengthen as needed

    // High-frequency noise adds small detail but not too much
    float detail = N11(fbm3D(p * 0.02, 3));
    d -= detail * 1.5;

    return d;
}

float MountainShape(float3 p)
{
    float warp1 = N11(fbm3D(p * 0.01, 3));
    float warp2 = N11(fbm3D(p * 0.01 + 37.0, 3));
    p.x += warp1 * 20.0;
    p.z += warp2 * 20.0;
    
    float r = length(p.xz);
    
    float slope = 0.5; // steeper than 0.5
    float tipHeight = 220.0; // height of the tip

    float d = r * slope + (p.y - tipHeight);
    d += sin(p.y * 0.05) * 2.0;

    float heightFactor = saturate((p.y - tipHeight) * 0.01);
    d -= fbm3D(p * 0.02, 4) * (heightFactor * 30.0);
    
    // ridged noise gives spiky peaks
    float ridged = fbmRidged(p * 0.005);
    d -= ridged * 40.0;

    return d;
}





[noinline]
float GenerateNoiseValue(float3 p)
{
    float ground = GroundVolumetric(p);
    
    float mountain = MountainShape(p);
    //mountain = max(mountain, ground);

    float terrain = SmoothUnion(ground, mountain, 30.0);

    return terrain;
}

#endif