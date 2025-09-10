using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkBiomeData
{
    public uint Height;
    public uint Temperature;
    public uint Humidity;
    public uint Foliage;

    public Vector4 Highlight;
    public Vector4 Light;
    public Vector4 MidLight;
    public Vector4 Mid;
    public Vector4 Dark;
    public Vector4 Shadow;
}
