    
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

uint GetIndex(RegularCellData cell, uint i)
{
    // Caller guarantees: i < IndicesCount
    return RegularCellIndices[cell.IndicesStart + i];
}

uint GetPackedVertexU16(uint classId, uint vertexIndex)
{
    // vertexIndex = local vertex index (0..VertexCount-1)
    uint baseIndex = RegularVertexRanges[classId].VertexStart;
    uint packed = RegularVertexData[baseIndex + vertexIndex];

    // Mask to original ushort
    return packed & 0xFFFF;
}

VertexDataUnpacked UnpackVertex(uint packed)
{
    VertexDataUnpacked v;
    packed &= 0xFFFF; // safety

    v.Corner1 = packed & 0x0F; // bits 0..3
    v.Corner0 = (packed >> 4) & 0x0F; // bits 4..7
    v.CacheIndex = (packed >> 8) & 0x0F; // bits 8..11
    v.CacheDir = (packed >> 12) & 0x07; // bits 12..15

    return v;
}