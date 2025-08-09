using System;
using UnityEngine;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready }
public enum LodDecision { KeepLeaf, Subdivide, Merge }

public class ChunkOctTreeMan
{
    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;
    private readonly float[] lodThresholds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkOctTreeMan"/> class.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="processor"></param>
    /// <param name="lodThresholds"></param>
    public ChunkOctTreeMan(IChunkServices services, ChunkGenerationProcessor processor, float[] lodThresholds)
    {
        this.services = services;
        this.processor = processor;
        this.lodThresholds = lodThresholds;
    }

    /// <summary>
    /// Evaluate the LOD of a given node to see if changes should be made.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public LodDecision EvaluateLod(ChunkOctTreeNode node)
    {
        Vector3 followerpos = this.services.Layout.FollowerWorldPosition;

        // XZ distance only
        var d = Vector2.Distance(
            new Vector2(followerpos.x, followerpos.z),
            new Vector2(node.Bounds.center.x, node.Bounds.center.z));

        float t = lodThresholds[node.Key.LODIndex];

        // Close enough? Go down (if there’s a lower LOD to go to)
        if (node.Key.LODIndex > 0 && d < t)
            return LodDecision.Subdivide;

        // Too far and we already have children? Collapse up
        if (node.Children != null && d > t)
            return LodDecision.Merge;

        // Otherwise keep as-is
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
        int chunkSize = services.Layout.GetChunkSize(parent.Key.LODIndex - 1);

        Vector3 worldMin = new Vector3(
            childCoord.x * chunkSize,
            childCoord.y * chunkSize,
            childCoord.z * chunkSize
        );

        Vector3 boundsCenter = worldMin + Vector3.one * (chunkSize / 2f);
        Bounds bounds = new Bounds(boundsCenter, Vector3.one * chunkSize);

        return new ChunkOctTreeNode(this, bounds, parent);
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
        int chunkSize = services.Layout.GetChunkSize(lodIndex);
        Vector3 pos = bounds.min;

        return new Vector3Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.y / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize)
        );
    }
}

/// <summary>
/// Represents a node in the terrain quadtree structure. Each node covers a chunk of terrain at a specific LOD.
/// Nodes can subdivide into 4 children for higher detail as the player gets closer.
/// </summary>
public class ChunkOctTreeNode
{
    /// <summary>
    /// The parent this node belongs to.
    /// </summary>
    private ChunkOctTreeMan Tree;

    /// <summary>
    /// The current phase of the content. This will help to know if we should skip.
    /// </summary>
    private ContentPhase CurrentContentPhase = ContentPhase.Unloaded;

    /// <summary>
    /// The current state of the node if it contains surface data
    /// </summary>
    private OccupancyState CurrentOccupancyState = OccupancyState.Unknown;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkOctTree"/> class. 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="renderer"></param>
    /// <param name="bounds"></param>
    /// <param name="parent"></param>
    public ChunkOctTreeNode(ChunkOctTreeMan tree, Bounds bounds, ChunkOctTreeNode? parent = null)
    {
        this.Tree = tree;
        this.Bounds = bounds;
        this.Parent = parent;

        var LODIndex = parent == null ? 4 : Mathf.Max(0, parent.Key.LODIndex - 1);
        var coordinates = tree.BoundsToCoordinate(bounds, LODIndex);
        this.Key = new ChunkKey(coordinates, LODIndex);
    }

    /// <summary>
    /// The key giving details about this node.
    /// </summary>
    public ChunkKey Key;

    /// <summary>
    /// World-space bounding box for this chunk.
    /// </summary>
    public Bounds Bounds { get; private set; }

    /// <summary>
    /// Parent node in the tree (if any).
    /// </summary>
    public ChunkOctTreeNode? Parent { get; private set; }

    /// <summary>
    /// Child nodes (NE, NW, SE, SW) created if this node is subdivided.
    /// </summary>
    public ChunkOctTreeNode[] Children = null;

    /// <summary>
    /// Returns whether this node has any children.
    /// </summary>
    public bool HasChildren => this.Children != null;

    /// <summary>
    /// Returns whether this node has no children.
    /// </summary>
    public bool IsLeaf => this.Children == null;

    /// <summary>
    /// An update method for the node, but this method will not be called every Update() called in Unity.
    /// </summary>
    /// <param name="followerPosition"></param>
    public void Tick()
    {
        if (HasChildren)
            foreach (var child in Children) child?.Tick();

        // Wait for the current phase to complete.
        if (CurrentContentPhase == ContentPhase.Loading)
            return;

        if (this.CurrentOccupancyState == OccupancyState.Empty)
            return;

        if (this.CurrentOccupancyState == OccupancyState.Unknown)
        {
            this.RequestSurfaceCheck();
            return;
        }

        var decision = this.Tree.EvaluateLod(this);

        if (decision == LodDecision.Subdivide)
            this.Subdivide();

        if (decision == LodDecision.Merge)
            this.Merge();

        if (IsLeaf && this.CurrentContentPhase == ContentPhase.Unloaded)
        {
            this.RequestGeneration();
        }
    }

    /// <summary>
    /// Subdivide the node into further parts.
    /// </summary>
    private void Subdivide()
    {
        if (this.Key.LODIndex == 0 || this.Children != null) return;

        Vector3 size = Bounds.size / 2f;
        Vector3 center = Bounds.center;
        Vector3Int baseCoord = this.Key.Coordinates;

        int cx = baseCoord.x * 2;
        int cy = baseCoord.y * 2;
        int cz = baseCoord.z * 2;

        Children = new ChunkOctTreeNode[8];
        Children[0] = Tree.CreateChild(this, new Vector3Int(cx + 0, cy + 0, cz + 0)); // Bottom SW
        Children[1] = Tree.CreateChild(this, new Vector3Int(cx + 1, cy + 0, cz + 0)); // Bottom SE
        Children[2] = Tree.CreateChild(this, new Vector3Int(cx + 0, cy + 0, cz + 1)); // Bottom NW
        Children[3] = Tree.CreateChild(this, new Vector3Int(cx + 1, cy + 0, cz + 1)); // Bottom NE
        Children[4] = Tree.CreateChild(this, new Vector3Int(cx + 0, cy + 1, cz + 0)); // Top SW
        Children[5] = Tree.CreateChild(this, new Vector3Int(cx + 1, cy + 1, cz + 0)); // Top SE
        Children[6] = Tree.CreateChild(this, new Vector3Int(cx + 0, cy + 1, cz + 1)); // Top NW
        Children[7] = Tree.CreateChild(this, new Vector3Int(cx + 1, cy + 1, cz + 1)); // Top NE

        // Remove this mesh.
        this.Tree.RemoveChild(this);
        this.CurrentContentPhase = ContentPhase.Unloaded;
    }

    /// <summary>
    /// Merge this node, deleting its children.
    /// </summary>
    private void Merge()
    {
        if (this.Children != null)
        {
            foreach (var child in Children)
            {
                if (child == null)
                    continue;

                child.Merge();
                this.Tree.RemoveChild(child);
            }

            this.Children = null;
        }

        // We will need to regenerate.
        this.CurrentContentPhase = ContentPhase.Unloaded;
    }

    /// <summary>
    /// Request the node check for surface before rendering.
    /// </summary>
    private void RequestSurfaceCheck()
    {
        if (CurrentOccupancyState == OccupancyState.Loading) return;
        CurrentOccupancyState = OccupancyState.Loading;

        this.Tree.RequestSurfaceCheck(this.Key, (bool result) =>
        {
            if (result)
                this.CurrentOccupancyState = OccupancyState.NonEmpty;
            else
                this.CurrentOccupancyState = OccupancyState.Empty;
        });
    }

    /// <summary>
    /// Request the chunk to generate.
    /// </summary>
    private void RequestGeneration()
    {
        this.CurrentContentPhase = ContentPhase.Loading;

        Tree.RequestGeneration(Key, success =>
        {
            if (success)
            {
                CurrentOccupancyState = OccupancyState.NonEmpty;
                CurrentContentPhase = ContentPhase.Ready;
            }
            else
            {
                CurrentOccupancyState = OccupancyState.Empty;
                CurrentContentPhase = ContentPhase.Unloaded;
                Tree.RemoveChild(this);
            }
        });
    }
}