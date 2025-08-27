

using System;
using UnityEngine;

/// <summary>
/// Oct tree debug cube visibility.
/// </summary>
public enum OctTreeCubeVisibility
{
    None,
    Active
}

[Serializable]
public class DebugOptions
{
    [Tooltip("When using OctTrees this will help visualize the chunks.")]
    public OctTreeCubeVisibility OctTreeCubes = OctTreeCubeVisibility.None;
}