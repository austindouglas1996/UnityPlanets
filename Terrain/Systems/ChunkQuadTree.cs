using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using UnityEngine;

/// <summary>
/// Tracks the lifecycle state of a quadtree chunk.
/// </summary>
public enum ChunkStatus
{
    Uninitialized,
    Loading,
    Finished,
    Subdivided
}

/// <summary>
/// Represents a node in the terrain quadtree structure. Each node covers a chunk of terrain at a specific LOD.
/// Nodes can subdivide into 4 children for higher detail as the player gets closer.
/// </summary>
public class ChunkQuadTree : IDisposable
{
    /// <summary>
    /// Take the amount of children you expect and minus 1.
    /// </summary>
    private const int EXPECTED_CHILDREN = 31;

    private int LODIndex;
    private Vector3Int coordinates;

    private IChunkServices services;
    private ChunkRenderer renderer;

    private bool generationCalled = false;
    private bool initialUpdateCalled = false;
    private bool isHidden = false;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkQuadTree"/> class. 
    /// </summary>
    /// <param name="services"></param>
    /// <param name="renderer"></param>
    /// <param name="bounds"></param>
    /// <param name="parent"></param>
    public ChunkQuadTree(IChunkServices services, ChunkRenderer renderer, Bounds bounds, ChunkQuadTree? parent = null)
    {
        this.services = services;
        this.renderer = renderer;
        this.Bounds = bounds;
        this.Parent = parent;

        this.Status = ChunkStatus.Uninitialized;

        this.LODIndex = parent == null ? 4 : Mathf.Max(0, parent.LODIndex - 1);
        this.coordinates = BoundsToCoordinate(bounds, LODIndex);
    }

    /// <summary>
    /// Current state of this chunk node (uninitialized, loading, ready, or subdivided).
    /// </summary>
    public ChunkStatus Status = ChunkStatus.Uninitialized;

    /// <summary>
    /// World-space bounding box for this chunk.
    /// </summary>
    public Bounds Bounds { get; private set; }

    /// <summary>
    /// Parent node in the tree (if any).
    /// </summary>
    public ChunkQuadTree? Parent { get; private set; }

    /// <summary>
    /// Child nodes (NE, NW, SE, SW) created if this node is subdivided.
    /// </summary>
    public ChunkQuadTree[] Children = new ChunkQuadTree[4];

    /// <summary>
    /// Child nodes based on verticle size.
    /// </summary>
    public Dictionary<Vector3Int, ChunkRenderData> VerticalChildren = new();

    /// <summary>
    /// The render data generated for this chunk, assigned after async generation finishes.
    /// </summary>
    public ChunkRenderData RenderData { get; private set; }

    /// <summary>
    /// Called by the renderer once chunk generation is done to assign the render data and update status.
    /// </summary>
    /// <param name="renderData"></param>
    public void SetRenderData(Vector3Int coordinates, ChunkRenderData renderData)
    {
        if (coordinates == this.coordinates)
        {
            this.RenderData = renderData;
            this.RequestVerticalChildrenGeneration();
        }
        else if (this.VerticalChildren.ContainsKey(coordinates))
        {
            if (this.VerticalChildren.Count == EXPECTED_CHILDREN)
                this.Status = ChunkStatus.Finished;

            if (renderData != null)
                VerticalChildren[coordinates] = renderData;
        }
    }

    /// <summary>
    /// Called once a frame to help with deciding when to subdivide.
    /// </summary>
    /// <param name="followerWorldPosition"></param>
    /// <param name="lodThresholds"></param>
    public void Update(Vector3 followerWorldPosition, float[] lodThresholds)
    {
        float distance = Vector3.Distance(followerWorldPosition, this.Bounds.center);

        // If we're subdivided, propagate update to children and possibly merge
        if (Status == ChunkStatus.Subdivided)
        {
            foreach (var child in Children)
                child?.Update(followerWorldPosition, lodThresholds);

            UpdateVisibility(false);

            if (distance > lodThresholds[LODIndex])
                Merge(followerWorldPosition);

            return;
        }

        // Handle first-time update initialization
        if (!initialUpdateCalled)
        {
            UpdateInitial(followerWorldPosition, distance, lodThresholds);
            return;
        }

        // Subdivide only if this chunk is finished, not LOD0, and close enough
        if (Status == ChunkStatus.Finished && LODIndex > 0 && distance < lodThresholds[LODIndex])
        {
            SubDivide(followerWorldPosition);
        }
    }

    /// <summary>
    /// Dispose of this node.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public void Dispose()
    {
        RenderData = null;
        Parent = null;
        Children = new ChunkQuadTree[4];
        VerticalChildren.Clear();
        Status = ChunkStatus.Uninitialized;
    }

    /// <summary>
    /// Initial update to help with handling if we should subdivide now, or request generation.
    /// </summary>
    /// <param name="followerWorldPosition"></param>
    /// <param name="distance"></param>
    /// <param name="lodThresholds"></param>
    private void UpdateInitial(Vector3 followerWorldPosition, float distance, float[] lodThresholds)
    {
        if (initialUpdateCalled || this.Status != ChunkStatus.Uninitialized) return;
        initialUpdateCalled = true;

        bool canSubdivide = this.LODIndex != 0 && distance < lodThresholds[this.LODIndex];
        if (canSubdivide)
        {
            this.SubDivide(followerWorldPosition);
        }
        else
        {
            this.RequestInitialGeneration();
        }
    }

    /// <summary>
    /// Update the visiblity of this node.
    /// </summary>
    /// <param name="visible"></param>
    private void UpdateVisibility(bool visible)
    {
        if (!isHidden && !visible)
        {
            bool childrenReady = Children.All(c => c != null && c.Status == ChunkStatus.Finished);
            if (childrenReady)
            {
                if (this.RenderData != null)
                    this.RenderData.IsActive = false;

                foreach (var child in VerticalChildren.Values)
                    if (child != null)
                        child.IsActive = false;
            }
        }

        if (isHidden && visible)
        {
            if (this.RenderData != null)
                this.RenderData.IsActive = true;

            foreach (var child in VerticalChildren.Values)
                if (child != null)
                    child.IsActive = true;
        }

        this.isHidden = !visible;
    }

    /// <summary>
    /// Request this node generation.
    /// </summary>
    private void RequestInitialGeneration()
    {
        this.Status = ChunkStatus.Loading;

        // Renderer will automatically update the RenderData once generation is complete.
        this.renderer.RequestGeneration(new ChunkContext(coordinates, LODIndex, services), this);

        this.generationCalled = true;
    }

    /// <summary>
    /// Request the vertical chunks part of this root node be generated.
    /// </summary>
    private void RequestVerticalChildrenGeneration()
    {
        try
        {
            for (int y = 1; y < EXPECTED_CHILDREN +1; y++)
            {
                var coord = new Vector3Int(coordinates.x, coordinates.y + y, coordinates.z);

                if (coord == this.coordinates)
                    continue;

                var context = new ChunkContext(coord, LODIndex, services);
                VerticalChildren.Add(coord, null);

                renderer.RequestGeneration(context, this); // Still pass this as quadNode, we will handle it later.
            }
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Create a new child object at a set position.
    /// </summary>
    /// <param name="chunkCoord"></param>
    /// <returns></returns>
    private ChunkQuadTree CreateChild(Vector3Int chunkCoord)
    {
        int lod = this.LODIndex - 1;
        int chunkSize = services.Configuration.DensityOptions.ChunkSize << lod;

        Vector3 worldPos = new Vector3(
            chunkCoord.x * chunkSize,
            chunkCoord.y * chunkSize,
            chunkCoord.z * chunkSize);

        Bounds bounds = new Bounds(
            worldPos + new Vector3(chunkSize / 2f, chunkSize / 2f, chunkSize / 2f),
            Vector3.one * chunkSize);

        return new ChunkQuadTree(services, renderer, bounds, this);
    }

    /// <summary>
    /// Subdivides this node into 4 children with a lower LOD (more detail). 
    /// Will do nothing if already subdivided, not ready, or this node is already LOD0.
    /// </summary>
    private void SubDivide(Vector3 followerWorldPosition)
    {
        try
        {
            if (this.LODIndex == 0 || this.Status == ChunkStatus.Loading || this.Status == ChunkStatus.Subdivided)
                return;

            Vector3 size = Bounds.size / 2f;
            Vector3 center = Bounds.center;
            Vector3Int baseCoord = this.coordinates;

            int cx = baseCoord.x * 2;
            int cy = baseCoord.y;
            int cz = baseCoord.z * 2;

            if (this.RenderData != null)
                this.renderer.RemoveChunk(this.RenderData);

            foreach (var vchild in VerticalChildren)
            {
                if (vchild.Value == null)
                    continue;

                this.renderer.RemoveChunk(vchild.Value);
            }

            this.VerticalChildren.Clear();

            Children[0] = CreateChild(new Vector3Int(cx + 1, cy, cz + 1)); // NE
            Children[1] = CreateChild(new Vector3Int(cx + 0, cy, cz + 1)); // NW
            Children[2] = CreateChild(new Vector3Int(cx + 1, cy, cz + 0)); // SE
            Children[3] = CreateChild(new Vector3Int(cx + 0, cy, cz + 0)); // SW

            this.Status = ChunkStatus.Subdivided;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    /// <summary>
    /// Merges this node restoring it back as the follower must have walked too far away.
    /// </summary>
    /// <param name="followerWorldPosition"></param>
    private void Merge(Vector3 followerWorldPosition)
    {
        this.DisposeChildren();
        this.UpdateVisibility(true);

        if (!this.generationCalled)
        {
            this.RequestInitialGeneration();
        }
        else
            this.Status = ChunkStatus.Finished;
    }

    /// <summary>
    /// Destroy the children nodes belonging to this node as a merge is in progress.
    /// </summary>
    private void DisposeChildren()
    {
        // Kill the children.
        foreach (var child in Children)
        {
            if (child == null)
                continue;

            foreach (var vchild in child.VerticalChildren)
            {
                if (vchild.Value == null)
                    continue;

                this.renderer.RemoveChunk(vchild.Value);
            }

            child.DisposeChildren();

            if (child.RenderData != null)
                this.renderer.RemoveChunk(child.RenderData);

            child.Dispose();
        }

        // Set to null.
        Children[0] = null;
        Children[1] = null;
        Children[2] = null;
        Children[3] = null;
    }

    /// <summary>
    /// Converts world-space bounds to chunk grid coordinates at the specified LOD.
    /// </summary>
    /// <param name="bounds"></param>
    /// <param name="lodIndex"></param>
    /// <returns></returns>
    private Vector3Int BoundsToCoordinate(Bounds bounds, int lodIndex)
    {
        int chunkSize = services.Configuration.DensityOptions.ChunkSize << lodIndex;
        Vector3 pos = bounds.min;

        return new Vector3Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.y / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize)
        );
    }
}