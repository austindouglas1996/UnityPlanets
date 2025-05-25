using System.Drawing;
using UnityEngine;

/// <summary>
/// Tracks the lifecycle state of a quadtree chunk.
/// </summary>
public enum ChunkStatus
{
    Uninitialized,
    Loading,
    Ready,
    Subdivided
}

/// <summary>
/// Represents a node in the terrain quadtree structure. Each node covers a chunk of terrain at a specific LOD.
/// Nodes can subdivide into 4 children for higher detail as the player gets closer.
/// </summary>
public class ChunkQuadTree
{
    private IChunkServices services;
    private ChunkRenderer renderer;

    public ChunkQuadTree(IChunkServices services, ChunkRenderer renderer, Bounds bounds, ChunkQuadTree? parent = null)
    {
        this.services = services;
        this.renderer = renderer;
        this.Bounds = bounds;
        this.Parent = parent;

        Status = ChunkStatus.Loading;

        int lodIndex = parent == null ? 5 : Mathf.Max(0, parent.RenderData.LOD - 1);
        Vector3Int coord = BoundsToCoordinate(bounds, lodIndex);

        // Renderer will automatically update the RenderData once generation is complete.
        this.renderer.RequestGeneration(new ChunkContext(coord, lodIndex, services), this);
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
    /// The render data generated for this chunk, assigned after async generation finishes.
    /// </summary>
    public ChunkRenderData RenderData { get; private set; }

    /// <summary>
    /// Called by the renderer once chunk generation is done to assign the render data and update status.
    /// </summary>
    /// <param name="renderData"></param>
    public void SetRenderData(ChunkRenderData renderData)
    {
        this.RenderData = renderData;
        this.Status = ChunkStatus.Ready;
    }

    /// <summary>
    /// Subdivides this node into 4 children with a lower LOD (more detail). 
    /// Will do nothing if already subdivided, not ready, or this node is already LOD0.
    /// </summary>
    public void SubDivide()
    {
        if (this.Status != ChunkStatus.Ready || this.RenderData.LOD == 0)
            return;

        Vector3 size = Bounds.size / 2f;
        Vector3 center = Bounds.center;

        Bounds ne = new Bounds(center + new Vector3(+size.x / 2f, 0, +size.z / 2f), size);
        Bounds nw = new Bounds(center + new Vector3(-size.x / 2f, 0, +size.z / 2f), size);
        Bounds se = new Bounds(center + new Vector3(+size.x / 2f, 0, -size.z / 2f), size);
        Bounds sw = new Bounds(center + new Vector3(-size.x / 2f, 0, -size.z / 2f), size);

        int childrenLOD = this.RenderData.LOD - 1;

        Children[0] = new ChunkQuadTree(services, renderer, ne, this);
        Children[1] = new ChunkQuadTree(services, renderer, nw, this);
        Children[2] = new ChunkQuadTree(services, renderer, se, this);
        Children[3] = new ChunkQuadTree(services, renderer, sw, this);

        this.Status = ChunkStatus.Subdivided;
        this.RenderData.IsActive = false;
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