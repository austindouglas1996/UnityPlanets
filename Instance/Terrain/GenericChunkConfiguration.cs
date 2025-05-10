using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    // Inspector variables.
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private ChunkRenderRange renderRange = new ChunkRenderRange();
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private List<BiomeOptions> biomeOptions = new List<BiomeOptions>();

    // Runtime only.
    private List<ITerrainModifier> modifiers = new();
    private List<IBiome> biomes = new List<IBiome>();

    // Properties.
    public int ChunkSize => chunkSize;
    public ChunkRenderRange RenderDistanceInChunks => renderRange;
    public DensityMapOptions MapOptions => densityMapOptions;
    public List<IBiome> Biomes => biomes;
    public List<ITerrainModifier> Modifiers => modifiers;

    /// <summary>
    /// Setup the configuration with runtime variables.
    /// </summary>
    /// <exception cref="ArgumentNullException"></exception>
    public void Setup()
    {
        if (biomeOptions.Count == 0)
            throw new ArgumentNullException("No biomes.");

        IBiomeFactory biomeFactory = new GenericBiomeFactory(densityMapOptions, biomeOptions);
        foreach (var biome in biomeOptions)
        {
            Biomes.Add(biomeFactory.CreateInstance(biome.Name));
        }

        this.modifiers.Add(new PathModifer());
    }
}