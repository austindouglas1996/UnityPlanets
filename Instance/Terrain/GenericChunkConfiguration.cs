using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private ChunkRenderRange renderRange = new ChunkRenderRange();
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private Material vertexColorMaterial;

    public int ChunkSize => chunkSize;
    public ChunkRenderRange RenderDistanceInChunks => renderRange;
    public DensityMapOptions MapOptions => densityMapOptions;

    /// <summary>
    /// A collection of biomes to use throughout the generation.
    /// </summary>
    public List<Biome> Biomes => biomes;
    [SerializeField] private List<Biome> biomes = new();

    /// <summary>
    /// Runtime only variable.
    /// </summary>
    public List<ITerrainModifier> Modifiers => modifiers; 
    [SerializeField] private List<ITerrainModifier> modifiers = new()
    {
        new PathModifer()
    };
}