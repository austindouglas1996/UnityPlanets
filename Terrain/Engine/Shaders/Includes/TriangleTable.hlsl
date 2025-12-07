#ifndef LOOKUP_TABLES_INCLUDED
#define LOOKUP_TABLES_INCLUDED

StructuredBuffer<float3> CornerOffsetsBuffer; // [8]
StructuredBuffer<int2> EdgeConnectionsBuffer; // [12]
StructuredBuffer<int> TriangleTableBuffer; // [16 * 256]

// Get the i-th corner offset
float3 GetCornerOffset(int i)
{
    return CornerOffsetsBuffer[i];
}

// Get the i-th edge connection (int2 with indices into corner list)
int2 GetEdgeConnection(int i)
{
    return EdgeConnectionsBuffer[i];
}

// Get the i-th triangle edge index for a given cube configuration
int GetTriangleEdgeIndex(int cubeIndex, int triangleVertexIndex)
{
    // Each cubeIndex has 16 possible edge indices (-1 if not used)
    return TriangleTableBuffer[cubeIndex * 16 + triangleVertexIndex];
}

#endif
