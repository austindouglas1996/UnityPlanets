using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class MarchingCubes
{
    public static float[,,] GenerateSphereMap(int chunkSize, Vector3Int chunkPos, Vector3 centerPos, float radius, float noiseScale, float noiseMultiplier, float frequency, float amplitude, int octaves)
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

                    float sampleFreq = frequency * noiseScale;

                    float noiseValue = Perlin.Fbm(
                        worldX * sampleFreq,
                        worldY * sampleFreq,
                        worldZ * sampleFreq, 
                        octaves) * amplitude;

                    float bumpyRadius = radius
                        + (sphericalNoise - 0.5f) * 5f
                        + (noiseValue) * noiseMultiplier;

                    densityMap[x, y, z] = (bumpyRadius - dist) * 0.05f;
                }
            }
        }


        return densityMap;
    }

    public static Mesh GenerateSphereMesh(MarchingCube cube)
    {
        // Build final mesh
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // In case large chunk
        mesh.vertices = cube.vertices.ToArray();
        mesh.triangles = cube.triangles.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 v = vertices[i].normalized;

            float u = 0.5f + Mathf.Atan2(v.z, v.x) / (2f * Mathf.PI);
            float vCoord = 0.5f - Mathf.Asin(v.y) / Mathf.PI;

            uvs[i] = new Vector2(u, vCoord);
        }

        mesh.uv = uvs;

        float minRadius = float.MaxValue;
        float maxRadius = float.MinValue;

        foreach (Vector3 vertex in vertices)
        {
            float distance = vertex.magnitude;

            if (distance < minRadius) minRadius = distance;
            if (distance > maxRadius) maxRadius = distance;
        }

        return mesh;
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