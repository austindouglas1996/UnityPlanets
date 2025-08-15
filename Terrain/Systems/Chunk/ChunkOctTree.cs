using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }
public enum LodDecision { KeepLeaf, Subdivide, Merge }
public enum Transition { None, Subdivide, Merge }

public class ChunkOctTreeMan
{
    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;
    private int[] lodThresholds;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkOctTreeMan"/> class.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="processor"></param>
    /// <param name="lodThresholds"></param>
    public ChunkOctTreeMan(IChunkServices services, ChunkGenerationProcessor processor, int[] lodThresholds)
    {
        this.services = services;
        this.processor = processor;
        this.lodThresholds = lodThresholds;
    }

    public int[] LODThresholds
    {
        get { return this.lodThresholds; }
        set { this.lodThresholds = value; }
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

        int[] ringsInChunks0 = this.lodThresholds.ToArray();

        int dChunks0 = ChebDistanceChunks0(services.Layout.FollowerWorldPosition, node.Bounds, 16);
        int desired = DesiredLodFromRings(dChunks0, ringsInChunks0);

        int L = node.Key.LODIndex;
        if (L > desired && node.IsLeaf && node.CurrentContentPhase != ContentPhase.Subdivided) return LodDecision.Subdivide;
        if (L < desired && node.HasChildren) return LodDecision.Merge;
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

    /// <summary>
    /// I found this in some StackOverflow thing, I dont really understand too much how it works
    /// https://en.wikipedia.org/wiki/Chebyshev_distance
    /// </summary>
    /// <param name="playerWorld"></param>
    /// <param name="b"></param>
    /// <param name="baseChunkSize"></param>
    /// <returns></returns>
    private static int ChebDistanceChunks0(Vector3 playerWorld, Bounds b, int baseChunkSize = 16)
    {
        // XZ only
        int dx = DistToInterval(playerWorld.x, b.min.x, b.max.x);
        int dz = DistToInterval(playerWorld.z, b.min.z, b.max.z);

        return Mathf.CeilToInt(Mathf.Max(dx, dz) / (float)baseChunkSize);
    }

    /// <summary>
    /// Determine the best LOD ring to use based on the distance.
    /// </summary>
    /// <param name="dChunks0"></param>
    /// <param name="rings"></param>
    /// <returns></returns>
    private static int DesiredLodFromRings(int dChunks0, int[] rings)
    {
        // rings[L] = max distance (in LOD0 chunks) where LOD == L
        for (int L = 0; L < rings.Length; L++)
            if (dChunks0 <= rings[L]) return L;
        return rings.Length - 1;
    }

    /// <summary>
    /// Returns the distance between two variables.
    /// </summary>
    /// <param name="p"></param>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    private static int DistToInterval(float p, float a, float b)
    {
        if (p < a) return Mathf.CeilToInt(a - p);
        if (p > b) return Mathf.CeilToInt(p - b);
        return 0;
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
    public ContentPhase CurrentContentPhase = ContentPhase.Unloaded;

    /// <summary>
    /// The current state of the node if it contains surface data
    /// </summary>
    private OccupancyState CurrentOccupancyState = OccupancyState.Unknown;

    /// <summary>
    /// The current transition to help subdivide/merge.
    /// </summary>
    public Transition CurrentTransition = Transition.None;

    /// <summary>
    /// The remaining ticks of a transition before executing an action.
    /// </summary>
    private int TransitionTicks = 0;

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

        this.RequestSurfaceCheck();
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
    public List<ChunkOctTreeNode> Children = new List<ChunkOctTreeNode>(8);

    /// <summary>
    /// Returns whether this node has any children.
    /// </summary>
    public bool HasChildren => this.Children.Count > 0;

    /// <summary>
    /// Returns whether this node has no children.
    /// </summary>
    public bool IsLeaf => !HasChildren;

    /// <summary>
    /// An update method for the node, but this method will not be called every Update() called in Unity.
    /// </summary>
    /// <param name="followerPosition"></param>
    public void Tick()
    {
        if (HasChildren)
            foreach (var child in Children) child?.Tick();

        // Handle transitions.
        if (this.CurrentTransition != Transition.None)
        {
            if (--TransitionTicks <= 0)
            {
                if (this.CurrentTransition == Transition.Subdivide) this.FinalizeSubdivide();
                if (this.CurrentTransition == Transition.Merge) this.FinalizeMerge();

                this.CurrentTransition = Transition.None;
                this.TransitionTicks = 0;
            }

            return;
        }

        // Wait for the current phase to complete.
        if (CurrentContentPhase == ContentPhase.Loading)
            return;

        if (this.CurrentOccupancyState == OccupancyState.Empty)
            return;

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
    /// Draw some debug gizmos to better understand placement.
    /// </summary>
    public void DrawDebugGizmo()
    {
        if (this.IsLeaf)
        {
            if (this.CurrentOccupancyState != OccupancyState.Empty)
            {
                Color c = LodColor(this.Key.LODIndex, 4, 0.85f);

                Gizmos.color = c;
                Gizmos.DrawWireCube(Bounds.center, Bounds.size);
            }
        }
        else
        {
            foreach (var child in this.Children)
            {
                child.DrawDebugGizmo();
            }
        }
    }

    /// <summary>
    /// Subdivide the node into further parts.
    /// </summary>
    private void Subdivide()
    {
        if (this.Key.LODIndex == 0 || this.HasChildren) return;
        this.CurrentContentPhase = ContentPhase.Loading;

        Vector3 size = Bounds.size / 2f;
        Vector3 center = Bounds.center;
        Vector3Int baseCoord = this.Key.Coordinates;

        int cx = baseCoord.x * 2;
        int cy = baseCoord.y * 2;
        int cz = baseCoord.z * 2;

        this.RequestChildSurfaceCheck(new Vector3Int(cx + 0, cy + 0, cz + 0)); // Bottom SW
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 1, cy + 0, cz + 0)); // Bottom SE
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 0, cy + 0, cz + 1)); // Bottom NW
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 1, cy + 0, cz + 1)); // Bottom NE
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 0, cy + 1, cz + 0)); // Top SW
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 1, cy + 1, cz + 0)); // Top SE
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 0, cy + 1, cz + 1)); // Top NW
        this.RequestChildSurfaceCheck(new Vector3Int(cx + 1, cy + 1, cz + 1)); // Top NE

        this.CurrentTransition = Transition.Subdivide;
        this.TransitionTicks = 30;
    }

    /// <summary>
    /// Merge this node, deleting its children.
    /// </summary>
    private void Merge()
    {
        // We already had children, so this most likely had surface.
        if (this.CurrentOccupancyState == OccupancyState.NonEmpty)
            this.RequestGeneration();

        this.CurrentContentPhase = ContentPhase.Loading;
        this.CurrentTransition = Transition.Merge;
        this.TransitionTicks = 30;
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
            {
                this.CurrentOccupancyState = OccupancyState.NonEmpty;
            }
            else
                this.CurrentOccupancyState = OccupancyState.Empty;
        });
    }

    /// <summary>
    /// Request children 
    /// </summary>
    /// <param name="coordinate"></param>
    private void RequestChildSurfaceCheck(Vector3Int coordinate)
    {
        ChunkKey ck = new ChunkKey(coordinate, this.Key.LODIndex - 1);

        this.Tree.RequestSurfaceCheck(ck, (bool result) =>
        {
            if (result)
            {
                ChunkOctTreeNode newNode = Tree.CreateChild(this, coordinate);
                newNode.CurrentOccupancyState = OccupancyState.NonEmpty;

                this.Children.Add(newNode);
            }
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

    /// <summary>
    /// Finalize the subdivide by removing this parent object. This is important because if not the parent will be removed before the children have
    /// had a chance to be rendered.
    /// </summary>
    private void FinalizeSubdivide()
    {
        this.CurrentContentPhase = ContentPhase.Subdivided;
        this.Tree.RemoveChild(this);
    }

    /// <summary>
    /// Finalize the merge by removing the children now that the parent has had a chance to render itself at least once.
    /// </summary>
    private void FinalizeMerge()
    {
        if (this.HasChildren)
        {
            foreach (var child in Children)
            {
                if (child == null)
                    continue;

                child.Merge();
                this.Tree.RemoveChild(child);
            }

            this.Children.Clear();
        }

        this.CurrentContentPhase = ContentPhase.Ready;
    }

    /// <summary>
    /// A simple function to get a color based on LOD.
    /// </summary>
    /// <param name="lod"></param>
    /// <param name="maxLod"></param>
    /// <param name="alpha"></param>
    /// <returns></returns>
    private static Color LodColor(int lod, int maxLod = 4, float alpha = 0.9f)
    {
        // t = 0 at farthest (maxLod), 1 at nearest (0)
        float t = Mathf.InverseLerp(maxLod, 0, lod);

        // Hue: green→red (0.33 green, 0.0 = red)
        float hue = Mathf.Lerp(0.33f, 0.0f, t);
        Color c = Color.HSVToRGB(hue, 0.95f, 1.0f);
        c.a = alpha;
        return c;
    }
}