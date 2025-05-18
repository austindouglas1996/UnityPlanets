using NUnit.Framework;
using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override DensityMapData Generate(int baseChunkSize, Vector3Int chunkCoordinates, int lodIndex)
    {
        int stepSize = 1 << lodIndex;
        int chunkSize = baseChunkSize << lodIndex;
        int limit = chunkSize + 1;

        DensityMapData mapData = CreateEmptyChunk(chunkSize, lodIndex);
        var densityMap = mapData.DensityMap;

        if (!ShouldGenerateChunk(chunkCoordinates, chunkSize))
        {
            return mapData;
        }

        int baseX = chunkCoordinates.x * chunkSize;
        int baseY = chunkCoordinates.y * chunkSize;
        int baseZ = chunkCoordinates.z * chunkSize;

        try
        {
            float[,] heightCache = new float[limit, limit];

            // First pass: calculate height at each (x,z)
            for (int x = 0; x < limit; x += stepSize)
            {
                int worldX = baseX + x;
                for (int z = 0; z < limit; z += stepSize)
                {
                    int worldZ = baseZ + z;
                    heightCache[x, z] = GetHeightForWorldPosition(worldX, worldZ);
                }
            }

            // Second pass: fill the 3D density map
            for (int x = 0; x < limit; x += stepSize)
            {
                int worldX = baseX + x;
                for (int y = 0; y < limit; y += 1)
                {
                    int worldY = baseY + y;
                    for (int z = 0; z < limit; z += stepSize)
                    {
                        int worldZ = baseZ + z;

                        float height = heightCache[x, z];
                        float val = -(worldY - height); // same shape logic

                        densityMap.Set(x, y, z, val);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        return mapData;
    }

    private DensityMapData CreateEmptyChunk(int size, int lodIndex)
    {
        return new DensityMapData(new DensityMap(size+1, size + 1, size+1),lodIndex);
    }
}