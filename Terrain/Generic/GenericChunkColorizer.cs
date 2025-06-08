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

        // Normalized blend factor between the two biomes
        float blendFactor = Mathf.InverseLerp(lowerBiome.MinSurface, upperBiome.MinSurface, vertice.y);

        // Evaluate each biome’s gradient at height-relative blend position
        Color lowerColor = lowerBiome.SurfaceColorRange.Evaluate(blendFactor);
        Color upperColor = upperBiome.SurfaceColorRange.Evaluate(blendFactor);

        // Now blend the evaluated colors based on height difference (optional if you want cross-biome smoothing)
        float biomeBlend = Mathf.InverseLerp(lowerBiome.MaxSurface, upperBiome.MinSurface, vertice.y);
        return Color32.Lerp(lowerColor, upperColor, biomeBlend);
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