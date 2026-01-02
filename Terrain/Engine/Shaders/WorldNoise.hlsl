#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "Lib/PerlinNoise.hlsl"
#include "ChunkFunctions.hlsl"

float SampleBiomeNoise(float3 p)
{
    float n = fbm3D(p * 0.0002, 4); 
    return n;
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

float GroundVolumetric(float3 p)
{
    // Large solid ground volume around y=0
    float d = abs(p.y) - 10;

    // Low-frequency noise creates big rolling landforms
    float n = fbm3D(p * 0.1, 8);
    d -= n * 3; // soften/strengthen as needed

    // High-frequency noise adds small detail but not too much
    float detail = N11(fbm3D(p * 0.02, 8));
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
    float tipHeight = 320.0; // height of the tip

    float d = r * slope + (p.y - tipHeight);
    d += sin(p.y * 0.05) * 3.0;

    float heightFactor = saturate((p.y - tipHeight) * 0.01);
    d -= fbm3D(p * 0.02, 4) * (heightFactor * 50.0);
    
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
    float terrain = SmoothUnion(ground, mountain, 30.0);

    // Remember to change back to terrain:
    return terrain;
}

#endif