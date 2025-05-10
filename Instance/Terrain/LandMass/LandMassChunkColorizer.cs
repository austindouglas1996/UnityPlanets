using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public class LandMassChunkColorizer : IChunkColorizer
{

    public Color[] ApplyColors(MeshData meshData, Matrix4x4 localToWorld, float[,] surfaceMap, IChunkConfiguration config)
    {
        Color[] colors = new Color[meshData.Vertices.Count];

        // Pre-sort biomes (optional if not already sorted)
        var sortedBiomes = config.Biomes.OrderBy(b => b.MinSurface).ToList();

        for (int i = 0; i < meshData.Vertices.Count; i++)
        {
            Vector3 worldPos = localToWorld.MultiplyPoint3x4(meshData.Vertices[i]);
            float height = worldPos.y;

            Biome lowerBiome = sortedBiomes[0];
            Biome upperBiome = sortedBiomes[1];

            for (int b = 0; b < sortedBiomes.Count - 1; b++)
            {
                if (height >= sortedBiomes[b].MinSurface && height < sortedBiomes[b + 1].MinSurface)
                {
                    lowerBiome = sortedBiomes[b];
                    upperBiome = sortedBiomes[b + 1];
                    break;
                }
            }

            float blendFactor = Mathf.InverseLerp(lowerBiome.MinSurface, upperBiome.MinSurface, height);

            Color lowerColor = lowerBiome.SurfaceColorRange.Evaluate(0f);
            Color upperColor = upperBiome.SurfaceColorRange.Evaluate(1f);

            // Blend between biome colors based on the height blend factor
            colors[i] = Color.Lerp(lowerColor, upperColor, blendFactor);
        }

        return colors;
    }

}