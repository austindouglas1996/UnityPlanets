#ifndef DENSITYFIELD_INCLUDED
#define DENSITYFIELD_INCLUDED

#include "WorldNoise.hlsl"

StructuredBuffer<ChunkWorkEdit> ChunkEdits;

float ApplyEdits(float3 wp, int editStart, int editCount, float density)
{
    [loop]
    for (int i = editStart; i < editStart + editCount; i++)
    {
        ChunkWorkEdit edit = ChunkEdits[i];

        float dist = distance(wp, edit.HitPointWP);
        if (dist < edit.Radius)
        {
            float sphere = dist - edit.Radius;

            if (edit.Operation == 0)
            {
                // Dig (subtract solid)
                density = max(density, -sphere);
            }
            else
            {
                // Fill (union solid)
                density = min(density, sphere);
            }
        }
    }

    return density;
}

float SampleDensity(float3 wp, int editStart, int editCount)
{
    float d = GenerateNoiseValue(wp);
    d = ApplyEdits(wp, editStart, editCount, d);
    
    return d;
}

#endif