
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class DensityMap
{
    private readonly float[] _values;

    public readonly int SizeX, SizeY, SizeZ;
    public readonly int StepSize;

    public DensityMap(int logicalSizeX, int logicalSizeY, int logicalSizeZ, int lodIndex)
    {
        StepSize = 1 << lodIndex;

        // Calculate compressed storage sizes
        SizeX = (logicalSizeX / StepSize) + 1;
        SizeY = (logicalSizeY / StepSize) + 1;
        SizeZ = (logicalSizeZ / StepSize) + 1;

        _values = new float[SizeX * SizeY * SizeZ];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int x, int y, int z)
    {
        return x + SizeX * (y + SizeY * z);
    }

    /// <summary>
    /// Accesses a value using compressed grid coordinates.
    /// </summary>
    public float GetLocal(int xi, int yi, int zi)
    {
        return _values[GetIndex(xi, yi, zi)];
    }

    /// <summary>
    /// Accesses a value using world-space coordinates (automatically mapped to compressed indices).
    /// </summary>
    public float GetWorld(int x, int y, int z)
    {
        int xi = x / StepSize;
        int yi = y / StepSize;
        int zi = z / StepSize;

        return _values[GetIndex(xi, yi, zi)];
    }

    public void SetLocal(int xi, int yi, int zi, float value)
    {
        try
        {
            _values[GetIndex(xi, yi, zi)] = value;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void SetWorld(int x, int y, int z, float value)
    {
        int xi = x / StepSize;
        int yi = y / StepSize;
        int zi = z / StepSize;

        try
        {
            _values[GetIndex(xi, yi, zi)] = value;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Indexer for compressed access.
    /// </summary>
    public float this[int x, int y, int z]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetLocal(x, y, z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetLocal(x, y, z, value);
    }

    public float[] Raw => _values;
}
