using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : BaseMarchingCubeGenerator
{
    public HeightDensityMapGenerator(ComputeShader shader, DensityMapOptions options) : base(shader,options)
    {
    }

    public override DensityMapOptions Options { get; set; }

    public override float[,,] Generate(int chunkSize, Vector3Int chunkCoordinates)
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
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    float sampleFreq = Options.Frequency * Options.NoiseScale;

                    // 3D noise for caves and structure
                    float noise = Perlin.Fbm(
                        worldX * sampleFreq,
                        worldY * sampleFreq,
                        worldZ * sampleFreq,
                        Options.Octaves
                    ) * Options.Amplitude;

                    float surfaceY = 50f; // world Y where the ground should mostly sit
                    float value = (surfaceY - worldY) + (noise * Options.NoiseMultiplier);

                    // Scale to match Marching Cubes range
                    densityMap[x, y, z] = value * 0.5f;
                }
            }
        }


        return densityMap;
    }

    public override MeshData GenerateMeshData(float[,,] densityMap, Vector3 chunkOffset)
    {
        MeshData initialData = base.GenerateMeshData(densityMap, chunkOffset);
        Vector2[] uvs = new Vector2[initialData.Vertices.Count];

        for (int i = 0; i < initialData.Vertices.Count; i++)
        {
            Vector3 v = initialData.Vertices[i].normalized;
            uvs[i] = new Vector2(v.x,v.y);
        }

        // Set the UV with our modified data.
        initialData.UVs = uvs.ToList();

        return initialData;
    }

    public override Mesh GenerateMesh(MeshData data)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32; // In case large chunk
        mesh.vertices = data.Vertices.ToArray();
        mesh.triangles = data.Triangles.ToArray();
        mesh.uv = data.UVs.ToArray();

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}