using System.Linq;
using UnityEngine;

public class SphereDensityMapGenerator : BaseMarchingCubeGenerator
{
    private Vector3 PlanetCenter;
    private float PlanetRadius;

    public SphereDensityMapGenerator(Vector3 planetCenter, float planetRadius, DensityMapOptions mapOptions)
        : base(mapOptions)
    {
        this.PlanetCenter = planetCenter;
        this.PlanetRadius = planetRadius;
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
                    // Convert local chunk coordinates to world coordinates
                    int worldX = chunkCoordinates.x * size.x + x;
                    int worldY = chunkCoordinates.y * size.y + y;
                    int worldZ = chunkCoordinates.z * size.z + z;

                    // Distance from center of the planet
                    Vector3 worldPos = new Vector3(worldX, worldY, worldZ);
                    float dist = Vector3.Distance(worldPos, PlanetCenter);

                    // Give the planet some roughness.
                    float sphericalNoise = Perlin.Fbm(worldX * 0.06f, worldY * 0.06f, worldZ * 0.06f, 5);

                    float sampleFreq = Options.Frequency * Options.NoiseScale;

                    float noiseValue = Perlin.Fbm(
                        worldX * sampleFreq,
                        worldY * sampleFreq,
                        worldZ * sampleFreq,
                        Options.Octaves) * Options.Amplitude;

                    float bumpyRadius = PlanetRadius
                        + (sphericalNoise - 0.5f) * 5f
                        + (noiseValue) * Options.NoiseMultiplier;

                    densityMap[x, y, z] = (bumpyRadius - dist) * 0.05f;
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

            float u = 0.5f + Mathf.Atan2(v.z, v.x) / (2f * Mathf.PI);
            float vCoord = 0.5f - Mathf.Asin(v.y) / Mathf.PI;

            uvs[i] = new Vector2(u, vCoord);
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