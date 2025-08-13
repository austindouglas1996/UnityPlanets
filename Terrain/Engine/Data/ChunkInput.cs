using System.Runtime.InteropServices;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkInput
{
    public Vector3 CoordPos;
    public Vector3 WorldPos;
    public int stepSize;
}