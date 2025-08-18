#include "../Includes/TriangleTable.hlsl"
#include "../ChunkFunctions.hlsl"


void March(ChunkDispatchKeyInfo key, AppendStructuredBuffer<ChunkTriangleData> TriangleBuffer, TerrainDensityOptions densityOptions)
{
    int chunkSize = densityOptions.ChunkSize;
    int chunkLogicalSize = densityOptions.ChunkSize + 1;
}