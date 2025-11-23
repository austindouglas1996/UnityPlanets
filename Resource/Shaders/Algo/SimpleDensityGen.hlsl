#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "../Lib/PerlinNoise.hlsl"
#include "../ChunkFunctions.hlsl"

// Convert world pos to noise domain (flat or planet mode)
[noinline]
float3 GetSamplePos3D(float3 world)
{
    float3 samplePos3D;

    if (TerrainType == TYPE_PLANET)
    {
        // For planet, use normalized direction from center for consistent wrapping
        float3 wp = float3(world.x, world.y, world.z);
        float3 dir = normalize(wp - PlanetCenter);

        // Offset so noise moves with BaseOffset/DetailOffset in 3D
        samplePos3D = dir * PlanetRadius;
    }
    else
    {
        // For landmass (and cave for now), just use flat XZ mapping
        samplePos3D = float3(world.x, 0, world.z);
    }
    
    return samplePos3D;
}

// Return the ocean contribution used on lands.
[noinline]
float GetOceanContribution(float landMask)
{
    return (1.0 - landMask) * OceanDepth;
}

// For planets apply an extra bit of flavor for latitude.
[noinline]
float ApplyLatitudeFalloff(float3 world, float base)
{
    float latitude = abs(normalize(world - PlanetCenter).y);
    return base * (1.0 - latitude);
}

// Return the sample for the continent space.
[noinline]
float SampleContinentMask(float3 samplePos3D, float3 world)
{
    if (SeaLevelBias == -4)
        return 1;
    
    float3 basePos = (samplePos3D + BaseOffset) * ContinentFreq;
    float base01 = N01(fbm3D(basePos, ContinentOctaves));
    
    float effectiveCoastWidth = min(CoastWidth, CoastWidth * (1.0 + ContinentAmp * 0.01));

    float coastLo = (SeaLevel + GetSeaLevelBias() - 0.5 * effectiveCoastWidth);
    float coastHi = (SeaLevel + GetSeaLevelBias() + 0.5 * effectiveCoastWidth);

    // This returns 0 near ocean, 1 inland, 0.5 at shore
    return smoothstep(coastLo, coastHi, base01);
}

// Samples the height and features of the terrain based on world position.
[noinline]
float SampleBaseHeight(float3 world, int lod)
{
    // Position to feed into FBM functions. Handles coordinate transforms
    // for different terrain types (planet, flat terrain, caves).
    float3 samplePos3D = GetSamplePos3D(world);

    // Land mask in [0..1]: 0 = ocean, 0.5 = shore, 1 = inland.
    float landMask = SampleContinentMask(samplePos3D, world);
    
    // Continents.

    // Low-frequency noise controls local amplitude variation.
    // Use a reduced frequency so it decorrelates from ContinentFreq.
    float continentAmpNoise = N01(fbm3D(samplePos3D * (ContinentAmpFreq * 0.1), 4));
    float continentAmpLocal = continentAmpNoise * ContinentAmp;
    
    // Signed height field for landmasses.
    float continentHeightNoise = fbm3D(samplePos3D * ContinentFreq, 4);
    float baseHeight = landMask * continentHeightNoise * continentAmpLocal;

    // Blend: inland rises with noise, ocean falls below sea level.
    baseHeight = lerp(-continentAmpLocal, baseHeight, landMask);
  
    // Flatten jagged spikes by applying a smoothing mask.
    // (This will remove the constant large bumps perlin likes to make)
    float3 flatPos = (samplePos3D + FlatMaskOffset) * FlatMaskFreq;
    float flatNoise = pow(saturate(1.0 - N01(fbm3D(flatPos, 4))), FlatMaskAmp);

    // Small-scale detail (bumps, ripples).
    // (This adds some bumps back and makes them a bit more noisy)
    float3 detailPos = (samplePos3D + DetailOffset) * DetailFreq;
    float detailNoise = fbm3D(detailPos, ContinentOctaves);
    float detail = landMask * (detailNoise * DetailAmp) * flatNoise;

    // Combine the above noise values.
    float rawHeight = baseHeight + detail - GetOceanContribution(landMask);

    // Sanitize the output so we do not get NAN.
    // (Note this will only happen if something went very bad in the generation
    // but also if it happens the function will just quit. Where zero we will see
    // the generation has stopped working)
    return Sanitize(rawHeight * ContinentAmp);
}

// Climate #1. Height: 0 = ocean, 1 = land, 0.5 = shore.
[noinline]
float SampleHeight(float3 world)
{
    return SampleContinentMask(GetSamplePos3D(world), world);
}

// Climate #2. Humidity: distance from ocean + bias + noise.
[noinline]
float SampleHumidity(float3 world, float height01, float temperature01)
{
    // Oceans are humid, land is dry
    float baseHumidity = 1.0 - height01;
    baseHumidity += HumidityBias;

    // Introduce variation
    float humidityNoise = N01(fbm3D(world * 0.01, 6));

    // Slight randomness
    baseHumidity = lerp(baseHumidity, humidityNoise, 0.2);
    baseHumidity *= lerp(0.8, 1.2, temperature01); // Slightly boost humidity if warm

    return saturate(baseHumidity);
}

// Climate #3. Temperature: land/ocean + bias + latitude + noise.
[noinline]
float SampleTemperature(float3 world, float height01)
{
    float tempNoise = N01(fbm3D(world * 0.0025, 8)); // Coarse variation

    // Ocean = 0, Land = 1
    float baseTemp = height01;

    // Apply TemperatureBias (-1 to +1, mapped from your enum or float)
    baseTemp += TemperatureBias;

    // If planet, apply polar falloff based on world.z (or latitude logic)
    if (TerrainType == TYPE_PLANET) // Assuming 1 = Planet
    {
        baseTemp = ApplyLatitudeFalloff(world, baseTemp);
    }

    // Final mix: mostly logical, some chaos
    float temperature = lerp(baseTemp, tempNoise, 0.3);

    return saturate(temperature);
}

// Climate #4. Foliage: temp × humidity × noise. Final tiebreaker for biome selection.
[noinline]
float SampleFoliage(float3 world, float height01, float temperature01, float humidity01)
{
    // Use height01 as inverse landmask again (0 = ocean, 1 = inland)
    float landMask = height01;

    // Base foliage = warmth × humidity (classic vegetation requirement)
    float idealGrowth = temperature01 * humidity01;

    // Add patchy noise to break up uniformity
    float foliageNoise = N01(fbm3D(world * 0.02, 4)); // tighter blobs

    // Mix in the noise: 30% noise, 70% climate-based logic
    float foliage = lerp(idealGrowth, foliageNoise, 0.3);

    // Fade out sharply in ocean: allow rare blobs but mostly 0
    foliage *= saturate((landMask - 0.15) * 6.0); // soft step: 0→~0.9

    return saturate(foliage);
}

// Generate a noise value based on the world position and LOD level.
[noinline]
float GenerateNoiseValue(float3 world, int lod)
{
    float baseNoise;
    
    if (TerrainType == TYPE_TERRAIN)
    {
        float v = SampleBaseHeight(world, lod);
        float result = -(world.y - v);
        
        baseNoise = Sanitize(result);

    }
    else if (TerrainType == TYPE_PLANET)
    {
        float elev = SampleBaseHeight(world, lod);
        float dist = length(world - PlanetCenter);
        
        baseNoise = (PlanetRadius + elev) - dist;
    }
    
    return baseNoise;
}

#endif