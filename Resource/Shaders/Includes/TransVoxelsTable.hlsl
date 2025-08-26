StructuredBuffer<uint> TransitionCellClass; // [512]
StructuredBuffer<uint> TransitionGeometryCounts; // [56]
StructuredBuffer<int> TransitionTriTable; // [56*36] flattened
StructuredBuffer<uint> TransitionCornerOffsets; // [13]
StructuredBuffer<uint> TransitionVertexData; // [512*12] flattened
StructuredBuffer<uint> TransitionFaceCornerRemap;

uint FaceCorner(uint face, uint localCorner)
{
    return TransitionFaceCornerRemap[face * 13 + localCorner];
}
