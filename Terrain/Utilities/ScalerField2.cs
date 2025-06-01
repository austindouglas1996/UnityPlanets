
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

public class ScalerField2
{
    private readonly float[] _values;

    public readonly int SizeX, SizeZ;
    public readonly int StepSize;

    public ScalerField2(int logicalSizeX, int logicalSizeZ, int lodIndex)
    {
        StepSize = 1 << lodIndex;

        // Calculate compressed storage sizes
        SizeX = (logicalSizeX / StepSize) + 1;
        SizeZ = (logicalSizeZ / StepSize) + 1;

        _values = new float[SizeX * SizeZ];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetIndex(int x, int z)
    {
        return x + SizeX * z;
    }

    /// <summary>
    /// Accesses a value using compressed grid coordinates.
    /// </summary>
    public float GetLocal(int xi, int zi)
    {
        return _values[GetIndex(xi, zi)];
    }

    /// <summary>
    /// Accesses a value using world-space coordinates (automatically mapped to compressed indices).
    /// </summary>
    public float GetWorld(int x, int z)
    {
        int xi = x / StepSize;
        int zi = z / StepSize;

        return _values[GetIndex(xi, zi)];
    }

    public void SetLocal(int xi, int zi, float value)
    {
        try
        {
            _values[GetIndex(xi, zi)] = value;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    public void SetWorld(int x, int z, float value)
    {
        int xi = x / StepSize;
        int zi = z / StepSize;

        try
        {
            _values[GetIndex(xi, zi)] = value;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Indexer for compressed access.
    /// </summary>
    public float this[int x, int z]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => GetLocal(x, z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => SetLocal(x, z, value);
    }

    public float[] Raw => _values;
}
