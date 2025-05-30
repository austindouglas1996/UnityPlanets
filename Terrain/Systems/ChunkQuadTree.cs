using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
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
[System.Serializable]
public class ChunkQuadTree
{
    public int LODIndex;
    private Vector3Int coordinates;

    private IChunkServices services;
    private ChunkRenderer renderer;

    private bool isGenerated = false;
    private bool isVisible = true;
    private bool mergeRequested = false;

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
    public ChunkQuadTree[] Children = new ChunkQuadTree[8];

    /// <summary>
    /// The render data generated for this chunk, assigned after async generation finishes.
    /// </summary>
    public ChunkRenderData RenderData { get; private set; }

    /// <summary>
    /// Set this node to active.
    /// </summary>
    /// <param name="val"></param>
    public void SetActive(bool val)
    {
        if (this.RenderData != null)
            this.RenderData.IsActive = val;

        foreach (var child in Children)
        {
            if (child != null)
                child.SetActive(val);
        }

        isVisible = val;
    }

    /// <summary>
    /// Dispose of this object.
    /// </summary>
    public void Dispose()
    {
        if (this.RenderData != null)
            this.renderer.RemoveChunk(this.RenderData);
    }

    /// <summary>
    /// Called by the renderer once chunk generation is done to assign the render data and update status.
    /// </summary>
    /// <param name="renderData"></param>
    public void SetRenderData(Vector3Int coordinates, ChunkRenderData renderData)
    {
        if (renderData != null)
        {
            this.RenderData = renderData;
            this.isGenerated = true;
        }

        this.Status = ChunkStatus.Finished;
    }

    /// <summary>
    /// Called once a frame to help with deciding when to subdivide.
    /// </summary>
    /// <param name="followerWorldPosition"></param>
    /// <param name="lodThresholds"></param>
    public void Update(Vector3 followerWorldPosition, float[] lodThresholds)
    {
        if (this.mergeRequested)
        {
            this.TryFinalizeMerge();
        }

        float distance = Vector3.Distance(followerWorldPosition, this.Bounds.center);
        float threshold = lodThresholds[LODIndex];

        if (this.Status == ChunkStatus.Uninitialized)
        {
            this.UpdateInitial(followerWorldPosition, distance, lodThresholds);
        }

        if (this.Status == ChunkStatus.Subdivided)
        {
            foreach (var child in Children)
                child?.Update(followerWorldPosition, lodThresholds);

            if (distance > threshold)
            {
                this.Merge(followerWorldPosition);
            }

            if (this.isVisible)
            {
                if (this.Children.All(r => r != null && r.Status == ChunkStatus.Finished))
                {
                    if (this.RenderData != null)
                        this.RenderData.IsActive = false;
                }
            }
        }

        if (this.Status == ChunkStatus.Finished && distance < threshold)
        {
            this.SubDivide(followerWorldPosition);
        }
    }

    /// <summary>
    /// Initial update to help with handling if we should subdivide now, or request generation.
    /// </summary>
    /// <param name="followerWorldPosition"></param>
    /// <param name="distance"></param>
    /// <param name="lodThresholds"></param>
    private void UpdateInitial(Vector3 followerWorldPosition, float distance, float[] lodThresholds)
    {
        if (this.Status != ChunkStatus.Uninitialized) return;

        bool canSubdivide = this.LODIndex != 0 && distance < lodThresholds[this.LODIndex];
        if (canSubdivide)
        {
            this.Status = ChunkStatus.Finished;
            this.SubDivide(followerWorldPosition, true);
        }
        else
        {
            this.RequestInitialGeneration();
        }
    }

    /// <summary>
    /// Request this node generation.
    /// </summary>
    private void RequestInitialGeneration()
    {
        this.Status = ChunkStatus.Loading;

        // Renderer will automatically update the RenderData once generation is complete.
        this.renderer.RequestGeneration(new ChunkContext(coordinates, LODIndex, services), this);
    }

    /// <summary>
    /// Subdivides this node into 4 children with a lower LOD (more detail). 
    /// Will do nothing if already subdivided, not ready, or this node is already LOD0.
    /// </summary>
    private void SubDivide(Vector3 followerWorldPosition, bool initial = false)
    {
        try
        {
            if (this.LODIndex == 0 || this.Status != ChunkStatus.Finished)
            {
                return;
            }

            Vector3 size = Bounds.size / 2f;
            Vector3 center = Bounds.center;
            Vector3Int baseCoord = this.coordinates;

            int cx = baseCoord.x * 2;
            int cy = baseCoord.y * 2;
            int cz = baseCoord.z * 2;

            Children[0] = CreateChild(new Vector3Int(cx + 0, cy + 0, cz + 0)); // Bottom SW
            Children[1] = CreateChild(new Vector3Int(cx + 1, cy + 0, cz + 0)); // Bottom SE
            Children[2] = CreateChild(new Vector3Int(cx + 0, cy + 0, cz + 1)); // Bottom NW
            Children[3] = CreateChild(new Vector3Int(cx + 1, cy + 0, cz + 1)); // Bottom NE
            Children[4] = CreateChild(new Vector3Int(cx + 0, cy + 1, cz + 0)); // Top SW
            Children[5] = CreateChild(new Vector3Int(cx + 1, cy + 1, cz + 0)); // Top SE
            Children[6] = CreateChild(new Vector3Int(cx + 0, cy + 1, cz + 1)); // Top NW
            Children[7] = CreateChild(new Vector3Int(cx + 1, cy + 1, cz + 1)); // Top NE
 
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
        if (this.Status != ChunkStatus.Subdivided)
            return;

        this.mergeRequested = true;

        this.TryFinalizeMerge();
    }

    /// <summary>
    /// Try and finalize the merge process.
    /// </summary>
    private void TryFinalizeMerge()
    {
        if (!this.mergeRequested || this.Status != ChunkStatus.Subdivided)
            return;

        foreach (var child in Children)
        {
            if (child == null || child.Status != ChunkStatus.Finished)
                return; 
        }

        // All children are done and safe to destroy.
        foreach (var child in Children)
        {
            child.Dispose();
        }

        for (int i = 0; i < 4; i++)
            Children[i] = null;

        this.mergeRequested = false;
        this.Status = this.isGenerated ? ChunkStatus.Finished : ChunkStatus.Uninitialized;
        this.SetActive(true);
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

        Vector3 chunkOffset = Vector3.one * (chunkSize / 2f);
        Bounds bounds = new Bounds(worldPos + chunkOffset, Vector3.one * chunkSize);

        return new ChunkQuadTree(services, renderer, bounds, this);
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