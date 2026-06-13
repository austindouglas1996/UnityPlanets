
#ifndef CHUNK_COMMON_STRUCT_INCLUDED
#define CHUNK_COMMON_STRUCT_INCLUDED

// Must stay in sync with the C# side struct of each if using a structured buffer:
//   - Field order
//   - Data type sizes/alignment
// Key that identifies a chunk for dispatch.
// CoordPos = chunk origin in LOD0-space (global, authoritative)
// LodIndex = step size based on current LOD
struct ChunkWorkDescriptor
{
    uint GlobalIndex;
    int3 Origin;

    uint LodIndex;
    uint LodEdgeMask;
    
    uint SourceOffset;
    uint SourceCount;
    uint DestStart;
    
    uint EditStart;
    uint EditCount;
};

// Must stay in sync with the C# side struct if used in a structured buffer:
//   - Field order
//   - Data type sizes and alignment
//
// Represents a single terrain edit operation applied in world space.
// This data is consumed directly by density generation kernels.
//
// All values are world-space and LOD-agnostic. The GPU determines voxel
// influence by comparing voxel world positions against these fields.
struct ChunkWorkEdit
{
    float3 HitPointWP;
    
    float Radius;
    float Intensity;
    
    int Operation;
};

// Returned by GetChunkAccess(). Holds everything needed
// for compute dispatch: the chunk index, map index, voxel coord,
// world position, and the original chunk key.
struct ChunkCellContext
{
    uint ChunkKeyIndex; // Index of the key this belongs to.
    uint DensitySampleIndex; // Position in a flattened density map.
    
    uint3 CellCoord; // [0..15]^3 cell in chunk
    float3 CellWorldPos; // world-space origin
    
    ChunkWorkDescriptor Chunk;
};

// Triangle data generated during marching.
// Holds 3 vertex positions (world space), the LOD they came from,
// and the biome is attached to the triangle packed into a 8 bytes
struct TriangleData
{
    float3 a;
    float3 b;
    float3 c;
    float3 NormalA;
    float3 NormalB;
    float3 NormalC;
    uint LodIndex;
    uint ChunkKeyIndex;
};

// ChunkDetailData
// A simple data struct to hold references to data per triangle
// this way they can be filled out in another buffer as I ran
// into issues with trying to calculate normals in the March kernel.
struct ChunkDetailData
{
    uint Foliage;
    float4 ColorA;
    float4 ColorB;
    float4 ColorC;
};

#endif