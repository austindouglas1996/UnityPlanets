using System;
using System.Linq;
using UnityEngine;

public class SphereDensityMapGenerator : HeightDensityMapGenerator
{
    private Vector3 PlanetCenter;
    private float PlanetRadius;

    public SphereDensityMapGenerator(PlanetChunkColorizer color, Vector3 planetCenter, float planetRadius, DensityMapOptions mapOptions)
        : base(color,mapOptions)
    {
        this.PlanetCenter = planetCenter;
        this.PlanetRadius = planetRadius;
    }

    protected override float GetValueForWorldPosition(float worldX, float worldY, float worldZ)
    {
        float dist = Vector3.Distance(new Vector3(worldX,worldY,worldZ), PlanetCenter);
        float baseValue = base.GetValueForWorldPosition(worldX, worldY, worldZ);
        float bumpyRadius = PlanetRadius + baseValue;

        return (bumpyRadius - dist) * 0.05f;
    }

    public override MeshData GenerateMeshData(DensityMap densityMap, Vector3 chunkOffset, int lodIndex = 6)
    {
        MeshData initialData = base.GenerateMeshData(densityMap, chunkOffset, lodIndex);
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