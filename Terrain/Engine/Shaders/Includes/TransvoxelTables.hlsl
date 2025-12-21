
struct RegularCellData
{
    uint VertexCount;
    uint TriangleCount;

    uint IndicesStart;
    uint IndicesCount;
};

struct VertexData
{
    uint VertexStart;
    uint VertexCount;
};

struct VertexDataUnpacked
{
    uint Corner0;
    uint Corner1;
    uint CacheIndex;
    uint CacheDir;
};

StructuredBuffer<int3> RegularCornerOffset;
StructuredBuffer<int3> TransitionCornerOffset;
StructuredBuffer<uint> RegularCellClass;

// RegularCellData
StructuredBuffer<RegularCellData> RegularCellTable;
StructuredBuffer<uint> RegularCellIndices;

// RegularVertexData
StructuredBuffer<VertexData> RegularVertexRanges;
StructuredBuffer<uint> RegularVertexData;

uint GetPackedVertexU16(uint classId, uint vertexIndex)
{
    uint baseIndex = RegularVertexRanges[classId].VertexStart;
    return RegularVertexData[baseIndex + vertexIndex];
}

VertexDataUnpacked UnpackVertex(uint packed)
{
    VertexDataUnpacked v;
    packed &= 0xFFFF;

    v.Corner1 = packed & 0x0F;
    v.Corner0 = (packed >> 4) & 0x0F;
    v.CacheIndex = (packed >> 8) & 0x0F;
    v.CacheDir = (packed >> 12) & 0x07;

    return v;
}