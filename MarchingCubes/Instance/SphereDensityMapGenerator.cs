using System;
using System.Linq;
using UnityEngine;

public class SphereDensityMapGenerator : GenericDensityMapGenerator
{
    private Vector3 PlanetCenter;
    private float PlanetRadius;

    public SphereDensityMapGenerator(Vector3 planetCenter, float planetRadius, DensityMapOptions mapOptions)
        : base(mapOptions)
    {
        this.PlanetCenter = planetCenter;
        this.PlanetRadius = planetRadius;
    }

    protected override float GetValueForWorldPosition(float worldX, float worldY, float worldZ)
    {
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

        return (bumpyRadius - dist) * 0.05f;
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
}