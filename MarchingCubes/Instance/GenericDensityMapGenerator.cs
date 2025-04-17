using System;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(DensityMapOptions options) : base(options)
    {
    }

    public override Tuple<float[,,], float[,]> Generate(int chunkSize, Vector3Int chunkCoordinates)
    {
        Vector3Int size = new Vector3Int(chunkSize, chunkSize, chunkSize);

        // Create a density map with an extra layer of padding for marching cubes
        float[,,] densityMap = new float[size.x + 3, size.y + 3, size.z + 3];

        // Create a surface map. Initially set it to -1.
        float[,] surfaceMap = new float[size.x + 1, size.z + 1];
        for (int x = 0; x < size.x + 1; x++)
            for (int z = 0; z < size.z + 1; z++)
                surfaceMap[x, z] = -1f; // initialize to invalid

        for (int x = 0; x < size.x + 3; x++)
        {
            for (int y = 0; y < size.y + 3; y++)
            {
                for (int z = 0; z < size.z + 3; z++)
                {
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    float val = GetValueForWorldPosition(worldX, worldY, worldZ);
                    densityMap[x, y, z] = val;

                    int ix = x - 1;
                    int iz = z - 1;
                    if (ix >= 0 && ix <= chunkSize && iz >= 0 && iz <= chunkSize)
                    {
                        if (surfaceMap[ix, iz] < 0f && val > Options.ISOLevel)
                            surfaceMap[ix, iz] = y - 1;
                    }
                }
            }
        }

        return new Tuple<float[,,], float[,]>(densityMap, surfaceMap);
    }

    protected abstract float GetValueForWorldPosition(float worldX, float worldY, float worldZ);
}