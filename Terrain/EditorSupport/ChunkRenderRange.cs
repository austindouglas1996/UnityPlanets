using System;
using System.Collections.Generic;

/// <summary>
/// Defines a min-max inclusive range along a single axis (X, Y, or Z),
/// primarily used for editor-driven chunk rendering filters.
/// 
/// June 4th:
/// I pulled this from an OLD push thinking it could be useful. I have no idea
/// why I made this so complicated for a simple range slider.
/// </summary>
[Serializable]
public class ChunkDistanceAxisRange
{
    public ChunkDistanceAxisRange() { }
    public ChunkDistanceAxisRange(int min, int max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Inclusive minimum value on the axis.</summary>
    public int Min;

    /// <summary>Inclusive maximum value on the axis.</summary>
    public int Max;

    /// <summary>
    /// Total size of the range, accounting for inclusive boundaries.
    /// </summary>
    public int Size => Max - Min + 1;

    /// <summary>
    /// Returns all integer values within this range.
    /// </summary>
    public IEnumerable<int> Values()
    {
        for (int i = Min; i <= Max; i++)
            yield return i;
    }

    /// <summary>
    /// Checks whether the given value falls within this range.
    /// </summary>
    public bool Contains(int value) => value >= Min && value <= Max;
}

/// <summary>
/// Represents a 3D range for chunk rendering visibility in the editor.
/// Used to restrict which chunks are displayed along each axis.
/// </summary>
[Serializable]
public class ChunkRenderRange
{
    public ChunkRenderRange() { }
    public ChunkRenderRange(int minX, int maxX, int minY, int maxY, int minZ, int maxZ)
    {
        X = new ChunkDistanceAxisRange(minX, maxX);
        Y = new ChunkDistanceAxisRange(minY, maxY);
        Z = new ChunkDistanceAxisRange(minZ, maxZ);
    }

    /// <summary>Range along the X axis.</summary>
    public ChunkDistanceAxisRange X;

    /// <summary>Range along the Y axis.</summary>
    public ChunkDistanceAxisRange Y;

    /// <summary>Range along the Z axis.</summary>
    public ChunkDistanceAxisRange Z;
}
