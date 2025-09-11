#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

// Computes the flat index into the density map for a voxel within a chunk batch
int GetVoxelSampleIndex(int3 pos, int KeyIndex, int3 sampleSize)
{
    int voxelCountPerChunk = sampleSize.x * sampleSize.y * sampleSize.z;
    int localIndex = pos.x + pos.y * sampleSize.x + pos.z * sampleSize.x * sampleSize.y;
    return KeyIndex * voxelCountPerChunk + localIndex;
}

// GetChunkAccess()
// Maps a dispatch thread ID (id) into:
//   - which chunk it belongs to
//   - the voxel's coordinates within that chunk
//   - its corresponding world position
//   - its flat index in the voxel map buffer
//
// Parameters:
//   id     : Dispatch thread ID (x, y, z) from the compute shader
//   sampleSize.x, sampleSize.y, sampleSize.z : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    uint keyCount, stride;
    keys.GetDimensions(keyCount, stride);

    if (id.x >= keyCount * CubesPerAxis || id.y >= CubesPerAxis || id.z >= CubesPerAxis)
    {
        r.KeyIndex = -1;
        return r;
    }

    // Map X → (KeyIndex, localX)
    r.KeyIndex = (int) (id.x / CubesPerAxis);
    r.LocalVoxelCoord = int3((int) (id.x - r.KeyIndex * CubesPerAxis), (int) id.y, (int) id.z);
    
    // A second guard check.
    if (r.LocalVoxelCoord.x >= CubesPerAxis ||
        r.LocalVoxelCoord.y >= CubesPerAxis ||
        r.LocalVoxelCoord.z >= CubesPerAxis)
    {
        r.KeyIndex = -1;
        return r;
    }

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.KeyIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + float3(r.LocalVoxelCoord) * GetCubeSizeStep(r.chunk.LodIndex);

    return r;
}

// GetChunkAccess()
// Maps a dispatch thread ID (id) into:
//   - which chunk it belongs to
//   - the voxel's coordinates within that chunk
//   - its corresponding world position
//   - its flat index in the voxel map buffer
//
// Parameters:
//   id     : Dispatch thread ID (x, y, z) from the compute shader
//   sampleSize.x, sampleSize.y, sampleSize.z : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int3 sampleSize, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    // Get number of chunks in this batch
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes); // batchSize = keyCount

    const int voxelCount = sampleSize.x * sampleSize.y * sampleSize.z;
    const uint logicalX = keyCount * (uint) sampleSize.x;

    // Guard extra threads from ceil() in Dispatch()
    if (id.x >= logicalX || id.y >= (uint) sampleSize.y || id.z >= (uint) sampleSize.z)
    {
        r.SampleIndex = -1;
        return r;
    }

    // Map X → (KeyIndex, localX)
    r.KeyIndex = (int) (id.x / (uint) sampleSize.x);
    r.LocalVoxelCoord = int3((int) (id.x - (uint) (r.KeyIndex * sampleSize.x)), (int) id.y, (int) id.z);

    // Flat index into the packed voxel buffer
    r.SampleIndex = GetVoxelSampleIndex(r.LocalVoxelCoord, r.KeyIndex, sampleSize);

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.KeyIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + float3(r.LocalVoxelCoord) * GetCubeSizeStep(r.chunk.LodIndex);

    return r;
}

#endif