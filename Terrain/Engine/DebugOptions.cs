

using System;
using UnityEngine;

[Serializable]
public class DebugOptions
{
    [Tooltip("A debug tool to show the LOD of each chunk."), Range(0, 1)]
    public int LODHeatMap;
}