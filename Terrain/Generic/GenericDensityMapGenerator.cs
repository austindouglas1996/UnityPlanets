using NUnit.Framework;
using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(IChunkColorizer color, DensityMapOptions options) : base(color, options)
    {
    }

    public override ScalerField3 Generate(ChunkContext context)
    {
        int lodIndex = context.LODIndex;
        Vector3Int chunkCoordinates = context.Coordinates;

        int stepSize = 1 << lodIndex;
        int chunkSize = context.Services.Layout.GetChunkSize(lodIndex);
        int limit = chunkSize + 1;

        ScalerField3 densityMap = CreateEmptyChunk(chunkSize, lodIndex);

        if (!ShouldGenerateChunk(context))
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

    private ScalerField3 CreateEmptyChunk(int size, int lodIndex)
    {
        return new ScalerField3(size, size, size, lodIndex);
    }
}