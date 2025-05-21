using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEngine.Rendering.STP;

public abstract class GenericChunkColorizer : IChunkColorizer
{
    private List<Biome> biomes;
    private IChunkConfiguration configuration;

    protected GenericChunkColorizer(IChunkConfiguration configuration)
    {
        this.configuration = configuration;
    }

    public Color32 GetColorForVertice(Vector3 vertice)
    {
        if (biomes == null)
            SortBiomes();

        Biome lowerBiome = biomes[0];
        Biome upperBiome = biomes[1];

        for (int b = 0; b < biomes.Count - 1; b++)
        {
            if (vertice.y >= biomes[b].MinSurface && vertice.y < biomes[b + 1].MinSurface)
            {
                lowerBiome = biomes[b];
                upperBiome = biomes[b + 1];
                break;
            }
        }

        float blendFactor = Mathf.InverseLerp(lowerBiome.MaxSurface, upperBiome.MinSurface, vertice.y);

        Color32 lowerColor = lowerBiome.SurfaceColorRange.Evaluate(0f);
        Color32 upperColor = upperBiome.SurfaceColorRange.Evaluate(1f);

        // Blend between biome colors based on the height blend factor
        return Color32.Lerp(lowerColor, upperColor, blendFactor);
    }

    private void SortBiomes()
    {
        this.biomes = configuration.Biomes.OrderBy(b => b.MinSurface).ToList();
    }

    public void UpdateChunkColors(ChunkData chunk, Matrix4x4 localToWorld)
    {
        if (chunk.MeshData.Vertices.Count == 0)
            return;

        var colors = chunk.MeshData.Colors;

        // Modifications
        foreach (ITerrainModifier modifier in configuration.Modifiers)
        {
            if (modifier is IModifyColor colorMod)
                colorMod.ModifyColor(ref colors, chunk.MeshData, localToWorld);
        }

        chunk.MeshData.Colors = colors;
    }
}