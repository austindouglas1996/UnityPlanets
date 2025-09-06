using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
struct ChunkBiomeData
{
    public float MinSurface;
    public float MaxSurface;
    public float MinTemp;
    public float MaxTemp;

    public Vector4 Highlight;
    public Vector4 Light;
    public Vector4 MidLight;
    public Vector4 Mid;
    public Vector4 Dark;
    public Vector4 Shadow;
}
