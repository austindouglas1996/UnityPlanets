using System;
using UnityEngine;

/// <summary>
/// Handles high-level logic for controlling the octree LOD behavior — break, merge, etc.
/// Acts as the "manager" layer that talks to layout and processing systems.
/// </summary>
public class ChunkOctreeService
{
    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkOctreeService"/> class.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="processor"></param>
    /// <param name="lodThresholds"></param>
    public ChunkOctreeService(IChunkServices services, ChunkGenerationProcessor processor)
    {
        this.services = services;
        this.processor = processor;
    }

    /// <summary>
    /// Returns the preference for debug cube visibility.
    /// </summary>
    public OctTreeCubeVisibility DebugCubeVisibility => services.Configuration.DebugOptions.OctTreeCubes;

    /// <summary>
    /// Evaluate the LOD for a given tree node to determine the best LOD.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public LodDecision EvaluateLod(ChunkOctTreeNode node)
    {
        // Don't touch while transitioning. 
        if (node.CurrentTransition != Transition.None)
            return LodDecision.KeepLeaf;

        int desired = services.Layout.GetLODForChunk(node.Key.Coordinates);

        if (ShouldSubdivide(node, desired))
            return LodDecision.Subdivide;

        if (ShouldMerge(node, desired))
            return LodDecision.Merge;

        return LodDecision.KeepLeaf;
    }

    /// <summary>
    /// Should the <see cref="ChunkOctTreeNode"/> subdivide from its current state.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="desired"></param>
    /// <returns></returns>
    private bool ShouldSubdivide(ChunkOctTreeNode node, int desired)
    {
        return node.Key.LODIndex > desired
            && node.IsLeaf
            && node.CurrentContentPhase != ContentPhase.Subdivided
            && node.CurrentOccupancyState == OccupancyState.NonEmpty;
    }

    /// <summary>
    /// Should the <see cref="ChunkOctTreeNode"/> merge from its current state.
    /// </summary>
    /// <param name="node"></param>
    /// <param name="desired"></param>
    /// <returns></returns>
    private bool ShouldMerge(ChunkOctTreeNode node, int desired)
    {
        return node.Key.LODIndex < desired && node.HasChildren;
    }

    /// <summary>
    /// Request a chunk be sent for surface checking to make sure we don't waste processing power
    /// on an empty chunk. Along with an action that is executed once it has completed.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="action"></param>
    public void RequestSurfaceCheck(ChunkKey key, Action<bool> action)
    {
        processor.RequestSurfaceCheck(key, action);
    }

    /// <summary>
    /// Request a chunk be sent to generation, along with an action that is executed
    /// once it has completed.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="action"></param>
    public void RequestGeneration(ChunkKey key, Action<bool> action)
    {
        processor.RequestChunkGeneration(key, action);
    }

    /// <summary>
    /// Create a new child object at a set position.
    /// </summary>
    /// <param name="chunkCoord"></param>
    /// <returns></returns>
    public ChunkOctTreeNode CreateChild(ChunkOctTreeNode parent, Vector3Int childCoord)
    {
        Bounds childBounds = this.services.Layout.GetBounds(childCoord, parent.Key.LODIndex - 1);
        return new ChunkOctTreeNode(this, childBounds, parent);
    }

    /// <summary>
    /// Request a chunk be removed from existing systems.
    /// </summary>
    /// <param name="node"></param>
    public void RemoveChild(ChunkOctTreeNode node)
    {
        this.processor.RemoveChunk(node.Key);
    }

    /// <summary>
    /// Converts world-space bounds to chunk grid coordinates at the specified LOD.
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="lodIndex"></param>
    /// <returns></returns>
    public Vector3Int BoundsToCoordinate(Bounds bounds, int lodIndex)
    {
        return this.services.Layout.BoundsToCoordinates(bounds, lodIndex);
    }
}
