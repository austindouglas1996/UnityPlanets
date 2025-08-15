using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
struct ChunkBiomeData
{
    public float MinSurface;
    public float MaxSurface;
    public Vector4 GradientStart;
    public Vector4 GradientEnd;
}
