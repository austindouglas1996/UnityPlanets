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
    float minSurface;
    float maxSurface;
    
    float minTemp;
    float maxTemp;
    
    float4 Highlight;
    float4 Light;
    float4 MidLight;
    float4 Mid;
    float4 Dark;
    float4 Shadow;
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
    uint LodKey;
};

#endif