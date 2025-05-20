using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public abstract class GenericChunkColorizer : IChunkColorizer
{
    public Color32[] GenerateVertexColors(ChunkData chunk, Matrix4x4 localToWorld, IChunkConfiguration configuration)
    {
        MeshData meshData = chunk.MeshData;

        Color32[] colors = new Color32[meshData.Vertices.Count];
        var sortedBiomes = configuration.Biomes.OrderBy(b => b.MinSurface).ToList();

        bool isActiveChunk = chunk.LOD == 0;

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

            Color32 lowerColor = lowerBiome.SurfaceColorRange.Evaluate(0f);
            Color32 upperColor = upperBiome.SurfaceColorRange.Evaluate(1f);

            // Blend between biome colors based on the height blend factor
            colors[i] = Color32.Lerp(lowerColor, upperColor, blendFactor);
        }

        return colors;
    }

    public void UpdateChunkColors(ChunkData chunk, Matrix4x4 localToWorld, IChunkConfiguration config)
    {
        if (chunk.MeshData.Vertices.Count == 0)
            return;

        // Base color.
        var colors = GenerateVertexColors(chunk, localToWorld, config);

        // Modifications
        foreach (ITerrainModifier modifier in config.Modifiers)
        {
            if (modifier is IModifyColor colorMod)
                colorMod.ModifyColor(ref colors, chunk.MeshData, localToWorld, config);
        }

        chunk.MeshData.Colors = colors;
    }
}