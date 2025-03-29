using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class MarchingCubes
{
    public static float[,,] GenerateRoundMap(int chunkSize, Vector3Int chunkPos, Vector3 centerPos, float radius)
    {
        Vector3Int size = new Vector3Int(chunkSize, chunkSize, chunkSize);

        // Create a density map with an extra layer of padding for marching cubes
        float[,,] densityMap = new float[size.x + 1, size.y + 1, size.z + 1];

        for (int x = 0; x < size.x + 1; x++)
        {
            for (int y = 0; y < size.y + 1; y++)
            {
                for (int z = 0; z < size.z + 1; z++)
                {
                    // Convert local chunk coordinates to world coordinates
                    int worldX = chunkPos.x * size.x + x;
                    int worldY = chunkPos.y * size.y + y;
                    int worldZ = chunkPos.z * size.z + z;

                    // Distance from center of the planet
                    Vector3 worldPos = new Vector3(worldX, worldY, worldZ);
                    float dist = Vector3.Distance(worldPos, centerPos);

                    // Give the planet some roughness.
                    float sphericalNoise = Perlin.Fbm(worldX * 0.06f, worldY * 0.06f, worldZ * 0.06f, 5);
                    float bumpyRadius = radius + (sphericalNoise - 0.5f) * 2f;

                    float density = (bumpyRadius - dist) * 0.05f;

                    densityMap[x, y, z] = density;
                }
            }
        }


        return densityMap;
    }

    public static float[,,] GenerateSquareMap(Vector3Int size, Vector3Int chunkPos, float noise, int octaves)
    {
        // Create a density map with an extra layer of padding for marching cubes
        float[,,] densityMap = new float[size.x + 1, size.y + 1, size.z + 1];

        for (int x = 0; x < size.x + 1; x++)
        {
            for (int y = 0; y < size.y + 1; y++)
            {
                for (int z = 0; z < size.z + 1; z++)
                {
                    // Convert local chunk coordinates to world coordinates
                    int worldX = chunkPos.x * size.x + x;
                    int worldY = chunkPos.y * size.y + y;
                    int worldZ = chunkPos.z * size.z + z;

                    // Sample 3D Perlin noise at the world coordinates
                    float noiseValue = Perlin.Fbm(worldX * noise, worldY * noise, worldZ * noise, octaves);
                    noiseValue *= Mathf.Abs(Perlin.Fbm(worldX * 0.02f, worldY * 0.02f, worldZ * 0.02f, octaves));

                    densityMap[x, y, z] = noiseValue;
                }
            }
        }

        return densityMap;
    }

    public static void ModifyMapWithBrush(ref float[,,] densityMap, Vector3Int chunkPos, Vector3 hitPoint, float radius, float intensity, bool add)
    {
        // IMPORTANT:
        // The size of the collection will be +1 due to how marching cubes work.
        int width = densityMap.GetLength(0)-1;
        int height = densityMap.GetLength(1)-1;
        int depth = densityMap.GetLength(2)-1;

        Vector3 chunkWorldOrigin = new Vector3(
            chunkPos.x * width,
            chunkPos.y * height,
            chunkPos.z * depth);

        for (int x = 0; x <= width; x++)
        {
            for (int y = 0; y <= height; y++)
            {
                for (int z = 0; z <= depth; z++)
                {
                    Vector3 voxelWorldPos = chunkWorldOrigin + new Vector3(x, y, z);
                    float dist = Vector3.Distance(voxelWorldPos, hitPoint);
                    if (dist > radius) continue;

                    float falloff = 1 - (dist / radius);
                    float mod = intensity * falloff;

                    if (add)
                        densityMap[x, y, z] += mod;
                    else
                        densityMap[x, y, z] -= mod;

                    densityMap[x, y, z] = Mathf.Clamp(densityMap[x, y, z], 0f, 1f);
                }
            }
        }
    }
}