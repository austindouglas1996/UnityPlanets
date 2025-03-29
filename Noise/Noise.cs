using UnityEngine;
using System.Collections;
using UnityEngine.Rendering;

public static class Noise
{
    public enum NormalizeMode { Local, Global };

    public static float[,] GenerateNoiseMap(int mapWidth, int mapHeight, int seed, float scale, int octaves, float persistance, float lacunarity, Vector2 offset, NormalizeMode normalizeMode)
    {
        // Define the border size
        int borderSize = 2;
        int borderedMapWidth = mapWidth + borderSize * 2;
        int borderedMapHeight = mapHeight + borderSize * 2;

        float[,] noiseMap = new float[borderedMapWidth, borderedMapHeight];

        System.Random prng = new System.Random(seed);
        Vector2[] octaveOffsets = new Vector2[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            octaveOffsets[i] = new Vector2(offsetX, offsetY);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        if (scale <= 0)
        {
            scale = 0.0001f;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = borderedMapWidth / 2f;
        float halfHeight = borderedMapHeight / 2f;

        for (int y = 0; y < borderedMapHeight; y++)
        {
            for (int x = 0; x < borderedMapWidth; x++)
            {
                amplitude = 1;
                frequency = 1;
                float noiseHeight = 0;

                for (int i = 0; i < octaves; i++)
                {
                    float sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                    float sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;

                    float perlinValue = Mathf.PerlinNoise(sampleX, sampleY) * 2 - 1;
                    noiseHeight += perlinValue * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                if (noiseHeight > maxLocalNoiseHeight)
                {
                    maxLocalNoiseHeight = noiseHeight;
                }
                else if (noiseHeight < minLocalNoiseHeight)
                {
                    minLocalNoiseHeight = noiseHeight;
                }

                noiseMap[x, y] = noiseHeight;
            }
        }

        // Normalize the noise map
        for (int y = 0; y < borderedMapHeight; y++)
        {
            for (int x = 0; x < borderedMapWidth; x++)
            {
                if (normalizeMode == NormalizeMode.Local)
                {
                    noiseMap[x, y] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y]);
                }
                else
                {
                    float normalizedHeight = (noiseMap[x, y] + 1) / (maxPossibleHeight / 0.9f);
                    noiseMap[x, y] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                }
            }
        }

        // Extract the central part of the noise map that corresponds to the actual chunk
        float[,] finalNoiseMap = new float[mapWidth, mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                finalNoiseMap[x, y] = noiseMap[x + borderSize, y + borderSize];
            }
        }

        return finalNoiseMap;
    }

    public static float[,,] GenerateNoiseMap3D(int mapWidth, int mapHeight, int mapDepth, int seed, float scale, int octaves, float persistance, float lacunarity, Vector3 offset, NormalizeMode normalizeMode)
    {
        // Define the border size
        int borderSize = 2;
        int borderedMapWidth = mapWidth + borderSize * 2;
        int borderedMapHeight = mapHeight + borderSize * 2;
        int borderedMapDepth = mapDepth + borderSize * 2;

        float[,,] noiseMap = new float[borderedMapWidth, borderedMapHeight, borderedMapDepth];

        System.Random prng = new System.Random(seed);
        Vector3[] octaveOffsets = new Vector3[octaves];

        float maxPossibleHeight = 0;
        float amplitude = 1;
        float frequency = 1;

        for (int i = 0; i < octaves; i++)
        {
            float offsetX = prng.Next(-100000, 100000) + offset.x;
            float offsetY = prng.Next(-100000, 100000) - offset.y;
            float offsetZ = prng.Next(-100000, 100000) - offset.z;
            octaveOffsets[i] = new Vector3(offsetX, offsetY, offsetZ);

            maxPossibleHeight += amplitude;
            amplitude *= persistance;
        }

        if (scale <= 0)
        {
            scale = 0.0001f;
        }

        float maxLocalNoiseHeight = float.MinValue;
        float minLocalNoiseHeight = float.MaxValue;

        float halfWidth = borderedMapWidth / 2f;
        float halfHeight = borderedMapHeight / 2f;
        float halfDepth = borderedMapDepth / 2f;

        for (int z = 0; z < borderedMapDepth; z++)
        {
            for (int y = 0; y < borderedMapHeight; y++)
            {
                for (int x = 0; x < borderedMapWidth; x++)
                {
                    amplitude = 1;
                    frequency = 1;
                    float noiseHeight = 0;

                    for (int i = 0; i < octaves; i++)
                    {
                        float sampleX = (x - halfWidth + octaveOffsets[i].x) / scale * frequency;
                        float sampleY = (y - halfHeight + octaveOffsets[i].y) / scale * frequency;
                        float sampleZ = (z - halfDepth + octaveOffsets[i].z) / scale * frequency;

                        float perlinValue = Perlin.Noise(sampleX, sampleY, sampleZ) * 2 - 1;
                        noiseHeight += perlinValue * amplitude;

                        amplitude *= persistance;
                        frequency *= lacunarity;
                    }

                    if (noiseHeight > maxLocalNoiseHeight)
                    {
                        maxLocalNoiseHeight = noiseHeight;
                    }
                    else if (noiseHeight < minLocalNoiseHeight)
                    {
                        minLocalNoiseHeight = noiseHeight;
                    }

                    noiseMap[x, y, z] = noiseHeight;
                }
            }
        }

        // Normalize the noise map
        for (int z = 0; z < borderedMapDepth; z++)
        {            
            for (int y = 0; y < borderedMapHeight; y++)
            {
                for (int x = 0; x < borderedMapWidth; x++)
                {
                    if (normalizeMode == NormalizeMode.Local)
                    {
                        noiseMap[x, y, z] = Mathf.InverseLerp(minLocalNoiseHeight, maxLocalNoiseHeight, noiseMap[x, y, z]);
                    }
                    else
                    {
                        float normalizedHeight = (noiseMap[x, y, z] + 1) / (maxPossibleHeight / 0.9f);
                        noiseMap[x, y, z] = Mathf.Clamp(normalizedHeight, 0, int.MaxValue);
                    }
                }
            }
        }

        // Extract the central part of the noise map that corresponds to the actual chunk
        float[,,] finalNoiseMap = new float[mapWidth, mapHeight, mapDepth];
        for (int z = 0; z < mapDepth; z++)
        {            
            for (int y = 0; y < mapHeight; y++)
            {
                for (int x = 0; x < mapWidth; x++)
                {
                    finalNoiseMap[x, y, z] = noiseMap[x + borderSize, y + borderSize, z + borderSize];
                }
            }
        }

        return finalNoiseMap;
    }
}