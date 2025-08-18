#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

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
//   sizeX, sizeY, sizeZ : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
// ============================================================================
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int sizeX, int sizeY, int sizeZ, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    // Get number of chunks in this batch
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes); // batchSize = keyCount

    const int voxelCount = sizeX * sizeY * sizeZ;
    const uint logicalX = keyCount * (uint) sizeX;

    // Guard extra threads from ceil() in Dispatch()
    if (id.x >= logicalX || id.y >= (uint) sizeY || id.z >= (uint) sizeZ)
    {
        r.mapIndex = -1;
        return r;
    }

    // Map X → (chunkIndex, localX)
    r.chunkIndex = (int) (id.x / (uint) sizeX);
    r.voxelCoord = int3((int) (id.x - (uint) (r.chunkIndex * sizeX)), (int) id.y, (int) id.z);

    // Flat index into the packed voxel buffer
    r.mapIndex =
        r.chunkIndex * voxelCount +
        (r.voxelCoord.x + r.voxelCoord.y * sizeX + r.voxelCoord.z * sizeX * sizeY);

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.chunkIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key) + float3(r.voxelCoord) * (1 << key.LodIndex);

    return r;
}

#endif