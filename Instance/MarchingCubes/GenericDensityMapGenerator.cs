using NUnit.Framework;
using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override DensityMapData Generate(int chunkSize, Vector3Int chunkCoordinates, int lodIndex)
    {
        int stepSize = 1 << lodIndex;

        Vector3Int size = new Vector3Int(chunkSize, chunkSize, chunkSize);
        DensityMapData mapData = CreateEmptyChunk(chunkSize, lodIndex);

        if (!ShouldGenerateChunk(chunkCoordinates, chunkSize))
        {
            return mapData;
        }

        for (int x = 0; x < size.x + 1; x += stepSize)
        {
            for (int y = 0; y < size.y + 1; y += stepSize)
            {
                for (int z = 0; z < size.z + 1; z += stepSize)
                {
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    float val = GetValueForWorldPosition(worldX, worldY, worldZ);
                    mapData.DensityMap[x, y, z] = val;

                    if (mapData.SurfaceMap[x, z] < 0f && val > Options.ISOLevel)
                        mapData.SurfaceMap[x, z] = y - 1;
                }
            }
        }

        return mapData;
    }

    private DensityMapData CreateEmptyChunk(int size, int lodIndex)
    {
        float[,,] densityMap = new float[size + 1, size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z <= size; z++)
                    densityMap[x, y, z] = 0; // fully empty

        float[,] surfaceMap = new float[size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int z = 0; z <= size; z++)
                surfaceMap[x, z] = -1f;

        float[,,] foliageMask = new float[size + 1, size + 1, size + 1];
        for (int x = 0; x <= size; x++)
            for (int y = 0; y <= size; y++)
                for (int z = 0; z <= size; z++)
                    foliageMask[x, y, z] = 1f; // still allow trees, etc.

        return new DensityMapData(densityMap, surfaceMap, foliageMask, lodIndex);
    }
}