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

    // Per-vertex colors (RGBA, typically from biome gradients)
    float4 colorA;
    float4 colorB;
    float4 colorC;
};

#endif // CHUNK_TRIANGLE_DATA_INCLUDED
