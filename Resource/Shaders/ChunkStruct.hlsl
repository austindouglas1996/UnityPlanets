#ifndef CHUNK_COMMON_STRUCT_INCLUDED
#define CHUNK_COMMON_STRUCT_INCLUDED

// ============================================================================
// ChunkBiomeData
// Holds the color gradient and height thresholds for a biome.
// Must match the C# struct `BiomeData` exactly in:
//   - Field order
//   - Data type size/alignment
// ============================================================================

struct ChunkBiomeData
{
    // Minimum surface height for this biome (inclusive).
    float minSurface;

    // Maximum surface height for this biome (exclusive).
    float maxSurface;

    // Gradient start color for the biome (usually lower height color).
    float4 gradientStart;

    // Gradient end color for the biome (usually higher height color).
    float4 gradientEnd;
};

struct ChunkDispatchKey
{
    // The logical coodinates of the key.
    float3 CoordPos;
    
    // The LOD converted into a step size.
    int LodIndex;
};

// Helper struct returned by GetChunkAccess() to provide both chunk-level
// and voxel-level access data for compute shader dispatches.
struct ChunkDispatchKeyInfo
{
    int chunkIndex;
    int mapIndex;
    int3 voxelCoord;
    float3 WorldPos;
    ChunkDispatchKey chunk;
};

struct ChunkTriangleData
{
    // Triangle vertex positions (world space)
    float3 a;
    float3 b;
    float3 c;
};

// ============================================================================
// DensityMapOptions
// Must match C# struct `TerrainDensityOptions` exactly in:
//   - Field order
//   - Data type size/alignment
//   - Purpose
// This is the parameter block used in density generation kernels.
// ============================================================================

struct TerrainDensityOptions
{
    // Logical voxel width of a chunk (before LOD).
    int ChunkSize;

    // Noise seed. Use this to derive offsets so worlds are stable per seed.
    int Seed;
    
    // A LOD heat map to see what LOD the chunks are.
    int LODHeatMap;
    
    // HLSL does not support enum, this is just for the subvarient to help with coding.
    int SubVariant;

    // Iso threshold used by marching. Keep if your meshing kernel reads it.
    float ISOLevel;

    // How wide the continents are. Lower = bigger continents.
    float BaseFreq;

    // How much the base layer lifts terrain up.
    float BaseGain;

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

    // XY offset for base landmass noise domain.
    float2 BaseOffset;

    // XY offset for detail domain (replaces +1234).
    float2 DetailOffset;

    // XY offset for flatness domain (replaces +5555).
    float2 FlatMaskOffset;
};

struct PlanetDensityOptions
{
    float3 Center;
    float Radius;
};


#endif