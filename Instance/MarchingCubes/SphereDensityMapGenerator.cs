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
        Vector3 worldPos = new Vector3(worldX, worldY, worldZ);
        float dist = Vector3.Distance(worldPos, PlanetCenter);

        // --- World Sculpting layers ---

        // 1. Continents/Oceans (very low frequency)
        float continentNoise = Perlin.Fbm(
            (worldX + Options.Seed) * Options.ContinentFrequency,
            (worldY + Options.Seed) * Options.ContinentFrequency,
            (worldZ + Options.Seed) * Options.ContinentFrequency,
            3
        );
        continentNoise = Mathf.Clamp01((continentNoise + 1f) * 0.5f);
        continentNoise = Mathf.SmoothStep(0f, 1f, continentNoise);

        // 2. Mountain Ridges (medium frequency)
        float mountainNoise = Perlin.Fbm(
            (worldX + Options.Seed + 999) * Options.MountainFrequency,
            (worldY + Options.Seed + 999) * Options.MountainFrequency,
            (worldZ + Options.Seed + 999) * Options.MountainFrequency,
            5
        );
        mountainNoise = Mathf.Pow(mountainNoise, Options.MountainSharpness); // sharpen peaks

        // 3. Small bumps/details (higher frequency)
        float detailNoise = Perlin.Fbm(
            (worldX + Options.Seed + 1234) * Options.DetailFrequency,
            (worldY + Options.Seed + 1234) * Options.DetailFrequency,
            (worldZ + Options.Seed + 1234) * Options.DetailFrequency,
            6
        );

        // --- Combine into a bumpy radius ---
        float bumpHeight =
            continentNoise * Options.ContinentAmplitude +
            mountainNoise * Options.MountainAmplitude +
            detailNoise * Options.DetailAmplitude;

        // Planet's surface with bumpiness added
        float bumpyRadius = PlanetRadius + bumpHeight;

        // --- Final density ---
        // Positive: inside planet (solid)
        // Negative: outside planet (empty)
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