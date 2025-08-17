// ============================================================================
// ChunkTriangleData.hlslinc
// Defines the vertex + color layout for marching cubes triangle output.
// Must match the C# struct `ChunkVertexTriangleData` exactly.
// ============================================================================

#ifndef CHUNK_TRIANGLE_DATA_INCLUDED
#define CHUNK_TRIANGLE_DATA_INCLUDED

struct ChunkTriangleData
{
    // Triangle vertex positions (world space)
    float3 a;
    float3 b;
    float3 c;
};

#endif // CHUNK_TRIANGLE_DATA_INCLUDED
