#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

// Computes the flat index into the density map for a voxel within a chunk batch
int GetVoxelSampleIndexRaw(int3 pos, int KeyIndex, int3 totalSampleSize)
{
    int voxelCountPerChunk = totalSampleSize.x * totalSampleSize.y * totalSampleSize.z;
    int localIndex = mad(pos.z, totalSampleSize.x * totalSampleSize.y,
                     mad(pos.y, totalSampleSize.x, pos.x));
    
    return KeyIndex * voxelCountPerChunk + localIndex;
}

// Computes the flat index into the density map for a voxel within a chunk batch
int GetVoxelSampleIndex(int3 pos, int KeyIndex, int3 totalSampleSize)
{
    // Apply border offset so (0,0,0) maps to (Border,Border,Border)
    pos += int3(BorderSamplesPerAxis, BorderSamplesPerAxis, BorderSamplesPerAxis); 
    return GetVoxelSampleIndexRaw(pos, KeyIndex, totalSampleSize);
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
ChunkDispatchKeyInfo GetChunkAccessCubes(uint3 id, uint offset, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    uint keyCount, stride;
    keys.GetDimensions(keyCount, stride);

    // Compute chunk within this dispatch
    uint chunkLocal = id.x / CubesPerAxis;
    uint localX = id.x % CubesPerAxis;

    // Apply global offset for key lookup
    r.KeyIndex = chunkLocal + offset;

    // Safety check that is for some reason breaks if not here.
    // 11/2 - I don't remember why I added this, but if this is missing
    // chunks dont render correctly which seems like we are overdispatching.
    if (r.KeyIndex >= keyCount || id.y >= CubesPerAxis || id.z >= CubesPerAxis)
    {
        r.SampleIndex = -1;
        return r;
    }

    // local coordinates stay within 0..sampleSize.x-1
    r.LocalVoxelCoord = uint3(localX, id.y, id.z);
    
    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.KeyIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + 
                 float3(r.LocalVoxelCoord) * GetCubeSizeStep(r.chunk.LodIndex);

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
ChunkDispatchKeyInfo GetChunkAccessSamples(uint3 id, uint offset, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    uint3 sampleSize = GetSamplesPerChunk3();
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes);

    // Compute chunk within this dispatch
    uint chunkLocal = id.x / sampleSize.x;
    uint localX = id.x % sampleSize.x;

    // Apply global offset for key lookup
    r.KeyIndex = chunkLocal + offset;

    // Safety check that is for some reason breaks if not here.
    // 11/2 - I don't remember why I added this, but if this is missing
    // chunks dont render correctly which seems like we are overdispatching.
    if (r.KeyIndex >= keyCount || id.y >= sampleSize.y || id.z >= sampleSize.z)
    {
        r.SampleIndex = -1;
        return r;
    }

    // local coordinates stay within 0..sampleSize.x-1
    r.LocalVoxelCoord = uint3(localX, id.y, id.z);

    // This points to the exact position in the map.
    r.SampleIndex = GetVoxelSampleIndexRaw(r.LocalVoxelCoord, r.KeyIndex, sampleSize);

    // Set key data
    ChunkDispatchKey key = keys[r.KeyIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex)
               + float3(r.LocalVoxelCoord) * GetCubeSizeStep(r.chunk.LodIndex);

    return r;
}



#endif