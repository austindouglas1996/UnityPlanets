#ifndef CHUNK_CBUFFER_STRUCT_INCLUDED
#define CHUNK_CBUFFER_STRUCT_INCLUDED

// ============================================================================
// DensityMapOptions
// Must match C# struct `TerrainDensityOptions` exactly in:
//   - Field order
//   - Data type size/alignment
//   - Purpose
// This is the parameter block used in density generation kernels.
// ============================================================================

cbuffer TerrainDensityOptions
{ 
    // Logical voxel width of a chunk (before LOD).
    int ChunkSize;

    // Noise seed. Use this to derive offsets so worlds are stable per seed.
    int Seed;
    
    // HLSL does not support enum, this is just for the subvarient to help with coding.
    int SubVariant;

    // Iso threshold used by marching. Keep if your meshing kernel reads it.
    float ISOLevel;

    // How wide the continents are. Lower = bigger continents.
    float BaseFreq;

    // How much the base layer lifts terrain up.
    float BaseGain;
    
    // The amount of sea vs land.
    float SeaLevel;
    
    // How wide coasts tend to be.
    float CoastWidth;
    
    // The depth of the ocean.
    float OceanDepth;

    // Size of large-scale bumps on top of the base.
    float DetailFreq;

    // Strength of those broad details.
    float DetailGain;

    // How large the flat regions run across the map.
    float FlatMaskFreq;

    // Higher -> stronger flattening in masked zones.
    float FlatMaskPower;

    // Global height scale for this layer after normalization.
    float ElevationScale;
    
    // XYZ offset for the entire generation.
    float3 PositionOffset;

    // XY offset for base landmass noise domain.
    float3 BaseOffset;

    // XY offset for detail domain (replaces +1234).
    float3 DetailOffset;

    // XY offset for flatness domain (replaces +5555).
    float3 FlatMaskOffset;
    
    // Padding needed for some buffers.
    float3 _Padding;
};

cbuffer PlanetDensityOptions
{
    float3 PlanetCenter;
    float PlanetRadius;
    
    float _Padding0;
    float3 _Padding1;
};

#endif