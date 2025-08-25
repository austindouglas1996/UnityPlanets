using System;
using UnityEngine;

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
    /// Evaluate the LOD for a given tree node to determine the best LOD.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public LodDecision EvaluateLod(ChunkOctTreeNode node)
    {
        // Don't touch while transitioning. 
        if (node.CurrentTransition != Transition.None)
            return LodDecision.KeepLeaf;

        int desired = services.Layout.GetLODForChunk(node.Key);

        int L = node.Key.LODIndex;
        if (L > desired
            && node.IsLeaf
            && node.CurrentContentPhase != ContentPhase.Subdivided
            && node.CurrentOccupancyState == OccupancyState.NonEmpty)
        {
            return LodDecision.Subdivide;
        }

        if (L < desired && node.HasChildren)
            return LodDecision.Merge;

        return LodDecision.KeepLeaf;
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
