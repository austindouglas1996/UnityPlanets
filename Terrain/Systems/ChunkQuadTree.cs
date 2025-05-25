using System.Buffers.Text;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

/// <summary>
/// Tracks the lifecycle state of a quadtree chunk.
/// </summary>
public enum ChunkStatus
{
    Uninitialized,
    Loading,
    FirstStage,
    SecondStage,
    Subdivided
}

/// <summary>
/// Represents a node in the terrain quadtree structure. Each node covers a chunk of terrain at a specific LOD.
/// Nodes can subdivide into 4 children for higher detail as the player gets closer.
/// </summary>
public class ChunkQuadTree
{
    private int LODIndex;
    private Vector3Int coordinates;

    private IChunkServices services;
    private ChunkRenderer renderer;

    private int verticalChunksReady = 0;
    private int verticalChunksExpected = 0;

    public ChunkQuadTree(IChunkServices services, ChunkRenderer renderer, Bounds bounds, ChunkQuadTree? parent = null)
    {
        this.services = services;
        this.renderer = renderer;
        this.Bounds = bounds;
        this.Parent = parent;

        Status = ChunkStatus.Loading;

        LODIndex = parent == null ? 4 : Mathf.Max(0, parent.LODIndex - 1);
        coordinates = BoundsToCoordinate(bounds, LODIndex);

        // Renderer will automatically update the RenderData once generation is complete.
        this.renderer.RequestGeneration(new ChunkContext(coordinates, LODIndex, services), this);
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
            this.Status = ChunkStatus.FirstStage; 
            this.RequestVerticalSliceGeneration();
        }
        else if (this.VerticalChildren.ContainsKey(coordinates))
        {
            this.verticalChunksReady++;
            if (this.verticalChunksReady == verticalChunksExpected)
                this.Status = ChunkStatus.SecondStage;

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

        if (this.Status == ChunkStatus.Subdivided)
        {
            foreach (var child in Children)
                child?.Update(followerWorldPosition, lodThresholds);
        }
        else if (distance < lodThresholds[this.LODIndex])
        {
            this.SubDivide();
        }
    }

    /// <summary>
    /// Retrieve a copy of a child at a set position.
    /// </summary>
    /// <param name="worldY"></param>
    /// <returns></returns>
    public ChunkRenderData? GetVerticalChild(float worldY)
    {
        int sectionHeight = 16;
        int sliceIndex = Mathf.FloorToInt(worldY / sectionHeight);

        var key = new Vector3Int(coordinates.x, sliceIndex, coordinates.z);
        return VerticalChildren.TryGetValue(key, out var renderData) ? renderData : null;
    }

    /// <summary>
    /// Subdivides this node into 4 children with a lower LOD (more detail). 
    /// Will do nothing if already subdivided, not ready, or this node is already LOD0.
    /// </summary>
    public void SubDivide()
    {
        try
        {
            if (this.LODIndex == 0 || this.Status != ChunkStatus.SecondStage)
                return;

            Vector3 size = Bounds.size / 2f;
            Vector3 center = Bounds.center;
            Vector3Int baseCoord = this.coordinates;

            int cx = baseCoord.x * 2;
            int cy = baseCoord.y;
            int cz = baseCoord.z * 2;

            if (this.RenderData != null)
                this.RenderData.IsActive = false;

            foreach (var child in VerticalChildren.Values)
                if (child != null)
                    child.IsActive = false;

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
    /// Returns true if this chunk fully contains the provided bounds.
    /// </summary>
    /// <param name="otherBounds"></param>
    /// <returns></returns>
    public bool Contains(Bounds otherBounds)
    {
        return this.Bounds.Contains(otherBounds.min) && this.Bounds.Contains(otherBounds.max);
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
    /// Request the vertical chunks part of this root node be generated.
    /// </summary>
    private void RequestVerticalSliceGeneration()
    {
        try
        {
            // Set global variable.
            this.verticalChunksExpected = 31;

            for (int y = -16; y < 16; y++)
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