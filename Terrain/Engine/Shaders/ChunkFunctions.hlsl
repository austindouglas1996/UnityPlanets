#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

// Computes the flat index into the density map for a voxel within a chunk batch
int GetDensitySampleIndexLocal(int3 sampleCoord, int ChunkKeyIndex, int3 totalSampleSize)
{
    int voxelCountPerChunk = totalSampleSize.x * totalSampleSize.y * totalSampleSize.z;
    int localIndex = mad(sampleCoord.z, totalSampleSize.x * totalSampleSize.y,
                     mad(sampleCoord.y, totalSampleSize.x, sampleCoord.x));
    
    return ChunkKeyIndex * voxelCountPerChunk + localIndex;
}

// Computes the flat index into the density map for a voxel within a chunk batch
int GetDensitySampleIndexPadded(int3 sampleCoord, int ChunkKeyIndex, int3 totalSampleSize)
{
    // Apply border offset so (0,0,0) maps to (Border,Border,Border)
    sampleCoord += int3(BorderSamplesPerAxis, BorderSamplesPerAxis, BorderSamplesPerAxis);
    return GetDensitySampleIndexLocal(sampleCoord, ChunkKeyIndex, totalSampleSize);
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
ChunkCellContext GetChunkCellCubes(uint3 id, uint offset, StructuredBuffer<ChunkWorkDescriptor> keys)
{
    ChunkCellContext r;

    uint keyCount, stride;
    keys.GetDimensions(keyCount, stride);

    // Compute chunk within this dispatch
    uint chunkLocal = id.x / CellsPerAxis;
    uint localX = id.x % CellsPerAxis;

    // Apply global offset for key lookup
    r.ChunkKeyIndex = chunkLocal + offset;

    // Safety check that is for some reason breaks if not here.
    // 11/2 - I don't remember why I added this, but if this is missing
    // chunks dont render correctly which seems like we are overdispatching.
    if (r.ChunkKeyIndex >= keyCount || id.y >= CellsPerAxis || id.z >= CellsPerAxis)
    {
        r.DensitySampleIndex = -1;
        return r;
    }

    // local coordinates stay within 0..sampleSize.x-1
    r.CellCoord = uint3(localX, id.y, id.z);
    
    // Fetch key and compute world position
    ChunkWorkDescriptor key = keys[r.ChunkKeyIndex];
    r.Chunk = key;   
    r.CellWorldPos = ChunkOriginToWorld(key) + (float3(r.CellCoord) * GetCellStep(r.Chunk.LodIndex));

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
ChunkCellContext GetChunkCellSamples(uint3 id, uint offset, StructuredBuffer<ChunkWorkDescriptor> keys)
{
    ChunkCellContext r;

    uint3 sampleSize = GetPaddedSamplesGridSize();
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes);

    // Compute chunk within this dispatch
    uint chunkLocal = id.x / sampleSize.x;
    uint localX = id.x % sampleSize.x;

    // Apply global offset for key lookup
    r.ChunkKeyIndex = chunkLocal + offset;

    // Safety check that is for some reason breaks if not here.
    // 11/2 - I don't remember why I added this, but if this is missing
    // chunks dont render correctly which seems like we are overdispatching.
    if (r.ChunkKeyIndex >= keyCount || id.y >= sampleSize.y || id.z >= sampleSize.z)
    {
        r.DensitySampleIndex = -1;
        return r;
    }

    // local coordinates stay within 0..sampleSize.x-1
    r.CellCoord = uint3(localX, id.y, id.z);

    // This points to the exact position in the map.
    r.DensitySampleIndex = GetDensitySampleIndexLocal(r.CellCoord, r.ChunkKeyIndex, sampleSize);

    // Set key data
    ChunkWorkDescriptor key = keys[r.ChunkKeyIndex];
    r.Chunk = key;
    r.CellWorldPos = ChunkOriginToWorld(key) + (float3(r.CellCoord) * GetCellStep(key.LodIndex));

    return r;
}

#endif