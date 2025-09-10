#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

// Computes the flat index into the density map for a voxel within a chunk batch
int GetVoxelMapIndex(int3 pos, int chunkIndex, int3 logicalSize)
{
    int voxelCountPerChunk = logicalSize.x * logicalSize.y * logicalSize.z;
    int localIndex = pos.x + pos.y * logicalSize.x + pos.z * logicalSize.x * logicalSize.y;
    return chunkIndex * voxelCountPerChunk + localIndex;
}

// ============================================================================
// GetChunkAccess()
// Maps a dispatch thread ID (id) into:
//   - which chunk it belongs to
//   - the voxel's coordinates within that chunk
//   - its corresponding world position
//   - its flat index in the voxel map buffer
//
// Parameters:
//   id     : Dispatch thread ID (x, y, z) from the compute shader
//   size.x, size.y, size.z : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
// ============================================================================
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int3 size, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    // Get number of chunks in this batch
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes); // batchSize = keyCount

    const int voxelCount = size.x * size.y * size.z;
    const uint logicalX = keyCount * (uint) size.x;

    // Guard extra threads from ceil() in Dispatch()
    if (id.x >= logicalX || id.y >= (uint) size.y || id.z >= (uint) size.z)
    {
        r.mapIndex = -1;
        return r;
    }

    // Map X → (chunkIndex, localX)
    r.chunkIndex = (int) (id.x / (uint) size.x);
    r.voxelCoord = int3((int) (id.x - (uint) (r.chunkIndex * size.x)), (int) id.y, (int) id.z);

    // Flat index into the packed voxel buffer
    r.mapIndex = GetVoxelMapIndex(r.voxelCoord, r.chunkIndex, size);

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.chunkIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + float3(r.voxelCoord) * GetChunkSizeStep(r.chunk.LodIndex);

    return r;
}

#endif