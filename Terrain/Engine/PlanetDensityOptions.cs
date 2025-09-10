using System;
using UnityEngine;

[Serializable]
public struct PlanetDensityOptions
{
    public Vector3 PlanetCenter;
    public float PlanetRadius;

    public float Tilt;
    public Matrix4x4 Rotation;
}
