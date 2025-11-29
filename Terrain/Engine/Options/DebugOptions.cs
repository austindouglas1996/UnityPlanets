namespace GingerVoxelSystem.Engine
{
    using System;
    using UnityEngine;
    public enum ChunkOverlay
    {
        None = 0,
        LOD = 1,
        Height = 2,
        Temperature = 3,
        Humidity = 4,
        Foliage = 5,
        Direction = 6,
        Debug = 7
    }

    [Serializable]
    public class DebugOptions
    {
        [Tooltip("A debug tool to help with seeing wtf is going on with the noise.")]
        public ChunkOverlay Overlay;
    }
}