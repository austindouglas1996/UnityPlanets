using System;
using System.Collections.Generic;
using UnityEngine;

[Flags]
public enum EdgeDirection
{
    None = 0,
    Left = 1 << 0,  // -X
    Right = 1 << 1,  // +X
    Back = 1 << 2,  // -Z
    Forward = 1 << 3,  // +Z
    //BackLeft = 1 << 4,  // -X, -Z
    //BackRight = 1 << 5,  // +X, -Z
    //ForwardLeft = 1 << 6,  // -X, +Z
    //ForwardRight = 1 << 7   // +X, +Z
}

public static class EdgeDirectionHelper
{
    public static readonly Dictionary<EdgeDirection, Vector3Int> DirectionOffsets = new()
    {
        { EdgeDirection.Left,         new Vector3Int(-1, 0, 0) },
        { EdgeDirection.Right,        new Vector3Int(1, 0, 0) },
        { EdgeDirection.Back,         new Vector3Int(0, 0, -1) },
        { EdgeDirection.Forward,      new Vector3Int(0, 0, 1) }
        //{ EdgeDirection.BackLeft,     new Vector3Int(-1, 0, -1) },
        //{ EdgeDirection.BackRight,    new Vector3Int(1, 0, -1) },
        //{ EdgeDirection.ForwardLeft,  new Vector3Int(-1, 0, 1) },
        //{ EdgeDirection.ForwardRight, new Vector3Int(1, 0, 1) }
    };
}