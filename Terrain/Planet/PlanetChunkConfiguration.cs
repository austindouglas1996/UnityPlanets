using System;
using UnityEngine;

[Serializable]
public class PlanetChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private int maxLoadRadius = 128;
    [SerializeField] private DensityMapOptions mapOptions;

    public ChunkType ChunkType => ChunkType.Planet;

    public int ChunkSize => chunkSize;
    public int MaxLoadRadius => maxLoadRadius;
    public DensityMapOptions MapOptions => mapOptions;
}
