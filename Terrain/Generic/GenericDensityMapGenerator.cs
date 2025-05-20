using NUnit.Framework;
using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override DensityMap Generate(Vector3Int chunkCoordinates, int lodIndex)
    {
        int stepSize = 1 << lodIndex;
        int chunkSize = this.Options.ChunkSize << lodIndex;
        int limit = chunkSize + 1;

        DensityMap densityMap = CreateEmptyChunk(chunkSize, lodIndex);

        if (!ShouldGenerateChunk(chunkCoordinates))
        {
            return densityMap;
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
                for (int y = 0; y < limit; y += stepSize)
                {
                    int worldY = baseY + y;
                    for (int z = 0; z < limit; z += stepSize)
                    {
                        int worldZ = baseZ + z;

                        float height = heightCache[x, z];
                        float val = -(worldY - height); // same shape logic

                        densityMap.SetWorld(x, y, z, val);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }

        return densityMap;
    }

    private DensityMap CreateEmptyChunk(int size, int lodIndex)
    {
        return new DensityMap(size, size, size, lodIndex);
    }
}