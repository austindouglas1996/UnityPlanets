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
    
    int Face; // -1 if not a transition job
    int NeighborLOD; // Only valid if Face != -1
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
    int LodKey;
};

#endif