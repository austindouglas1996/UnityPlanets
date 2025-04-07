using System;
using UnityEngine;

[Serializable]
public class LandMassChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private int chunkViewDistance = 6;
    [SerializeField] private DensityMapOptions mapOptions;

    public int ChunkSize => chunkSize;
    public int ChunkViewDistance => chunkViewDistance;
    public DensityMapOptions MapOptions => mapOptions;
}