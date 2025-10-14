using System;
using UnityEngine;

/// <summary>
/// Defines how many chunks to render around the player.
/// X/Z are symmetric (+-).
/// </summary>
[Serializable]
public class ChunkRenderDistance
{
    [Tooltip("Chunks visible in the X axis (left/right).")]
    public int X = 4;

    [Tooltip("Chunks visible in the Z axis (forward/back).")]
    public int Z = 4;

    [Tooltip("Chunks visible downward in the Y axis.")]
    public int Down = 0;

    [Tooltip("Chunks visible upward in the Y axis.")]
    public int Up = 2;

    /// <summary>
    /// Total span in chunks along each axis.
    /// </summary>
    public Vector3Int Span => new Vector3Int(
        X * 2 + 1,
        Up + Down + 1,
        Z * 2 + 1
    );
}
