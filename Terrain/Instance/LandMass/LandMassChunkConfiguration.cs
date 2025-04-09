using System;
using UnityEngine;

[Serializable]
public class LandMassChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private int chunkViewDistance = 6;
    [SerializeField] private DensityMapOptions mapOptions;
    [SerializeField] private int surfaceMin = 0;
    [SerializeField] private int surfaceMax = 0;

    public int ChunkSize => chunkSize;
    public int ChunkViewDistance => chunkViewDistance;
    public int SurfaceMin => surfaceMin;
    public int SurfaceMax => surfaceMax;
    public DensityMapOptions MapOptions => mapOptions;
}