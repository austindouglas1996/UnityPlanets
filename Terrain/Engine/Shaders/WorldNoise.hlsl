#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "Lib/PerlinNoise.hlsl"
#include "ChunkFunctions.hlsl"

float SampleBiomeNoise(float3 p)
{
    float n = fbm3D(p * 0.0002, 4); 
    return n;
}

float GroundHeight(float2 xz)
{
    float base = 10.0;

    float n = fbm2D(xz * 0.1, 8);
    base += n * 3.0;

    float detail = N11(fbm2D(xz * 0.02, 8));
    base += detail * 1.5;

    return base;
}


float MountainHeight(float2 xz)
{
    float r = length(xz);

    // radial falloff
    float mountainMask = saturate(1.0 - r / 200.0);

    // vertical height contribution
    float height = mountainMask * 300.0;

    // breakup
    height += fbm2D(xz * 0.01, 3) * 10.0;

    return height;
}

float SampleHeight(float2 xz)
{
    float ground = GroundHeight(xz);
    float mountain = MountainHeight(xz);

    // smooth blend in height space
    float blend = saturate(mountain / 300.0);
    return lerp(ground, max(ground, mountain), blend);
}


int GetBiomeID(float3 p)
{
    float height = SampleHeight(p.xz);

    if (height < -15.0)
        return 0; // sand
    else if (height < 10.0)
        return 1; // grass
    else if (height < 40.0)
        return 2; // alpine
    else
        return 3; // mountain
}



[noinline]
float GenerateNoiseValue(float3 p)
{
    float height = SampleHeight(p.xz);

    // base terrain field
    float density = p.y - height;

    // OPTIONAL: caves (selective 3D noise)
    if (p.y > -20 && p.y < 50)
        density += fbm3D(p * 0.05, 4) * 3.0;

    return density;
}

float ComputeDensity(float3 worldPos)
{
    float height = SampleHeight(worldPos.xz);
    return worldPos.y - height;
}



#endif