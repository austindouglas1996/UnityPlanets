using System.Collections.Generic;
using UnityEngine;
using VHierarchy.Libs;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }
public enum LodDecision { KeepLeaf, Subdivide, Merge }
public enum Transition { None, Subdivide, Merge }

/// <summary>
/// Represents a node in the terrain quadtree structure. Each node covers a chunk of terrain at a specific LOD.
/// Nodes can subdivide into 4 children for higher detail as the player gets closer.
/// </summary>
public class ChunkOctTreeNode
{
    /// <summary>
    /// The parent this node belongs to.
    /// </summary>
    private ChunkOctreeService treeService;

    /// <summary>
    /// The current phase of the content. This will help to know if we should skip.
    /// </summary>
    public ContentPhase CurrentContentPhase = ContentPhase.Unloaded;

    /// <summary>
    /// The current state of the node if it contains surface data
    /// </summary>
    public OccupancyState CurrentOccupancyState = OccupancyState.Unknown;

    /// <summary>
    /// The current transition to help subdivide/merge.
    /// </summary>
    public Transition CurrentTransition = Transition.None;

    /// <summary>
    /// The remaining ticks of a transition before executing an action.
    /// </summary>
    private int TransitionTicks = 0;

    /// <summary>
    /// The amount of children checked in a subdivide. Helps in case the children fail to render.
    /// </summary>
    private int childrenChecked = 0;

    /// <summary>
    /// A simple debug cube to visualize the node items.
    /// </summary>
    private GameObject DebugCube;

    /// <summary>
    /// Initialize a new instance of the <see cref="ChunkOctTree"/> class. 
    /// </summary>
    /// <param name="tree"></param>
    /// <param name="bounds"></param>
    /// <param name="parent"></param>
    public ChunkOctTreeNode(ChunkOctreeService tree, Bounds bounds, ChunkOctTreeNode? parent = null)
    {
        this.treeService = tree;
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

        var decision = this.treeService.EvaluateLod(this);

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
            if (this.CurrentOccupancyState == OccupancyState.NonEmpty &&
                this.CurrentContentPhase != ContentPhase.Subdivided)
            {
                if (this.Key.LODIndex != 0)
                {
                    string but = "";
                }

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
        this.TransitionTicks = 3;
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
        this.TransitionTicks = 3;
    }

    /// <summary>
    /// Request the node check for surface before rendering.
    /// </summary>
    private void RequestSurfaceCheck()
    {
        if (CurrentOccupancyState == OccupancyState.Loading) return;
        CurrentOccupancyState = OccupancyState.Loading;

        this.treeService.RequestSurfaceCheck(this.Key, (bool result) =>
        {
            if (result)
            {
                this.CurrentOccupancyState = OccupancyState.NonEmpty;
            }
            else
            {
                this.CurrentOccupancyState = OccupancyState.Empty;
            }
        });
    }

    /// <summary>
    /// Request children 
    /// </summary>
    /// <param name="coordinate"></param>
    private void RequestChildSurfaceCheck(Vector3Int coordinate)
    {
        ChunkKey ck = new ChunkKey(coordinate, this.Key.LODIndex - 1);

        this.treeService.RequestSurfaceCheck(ck, (bool result) =>
        {
            this.childrenChecked++;

            if (result)
            {
                ChunkOctTreeNode newNode = treeService.CreateChild(this, coordinate);
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

        treeService.RequestGeneration(Key, success =>
        {
            if (success)
            {
                CurrentOccupancyState = OccupancyState.NonEmpty;
                CurrentContentPhase = ContentPhase.Ready;

                // Create the debug cube.
                //this.CreateDebugCube();
            }
            else
            {
                CurrentOccupancyState = OccupancyState.Empty;
                CurrentContentPhase = ContentPhase.Unloaded;
                treeService.RemoveChild(this);
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
        this.treeService.RemoveChild(this);

        // Delete debug cube.
        this.DebugCube.Destroy();
    }

    /// <summary>
    /// Finalize the merge by removing the children now that the parent has had a chance to render itself at least once.
    /// </summary>
    private void FinalizeMerge()
    {
        this.DestroyChildren();
        this.CurrentContentPhase = ContentPhase.Ready;
    }

    /// <summary>
    /// Destroy the children part of this object (Recursive).
    /// </summary>
    private void DestroyChildren()
    {
        if (!this.HasChildren)
            return;

        foreach (var child in Children)
        {
            if (child == null)
                continue;

            child.DestroyChildren();
            this.treeService.RemoveChild(child);
        }

        this.Children.Clear();
    }

    /// <summary>
    /// Create a simple debug cube so we can see more about the nodes.
    /// </summary>
    private void CreateDebugCube()
    {
        DebugCube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Name the object using LOD and coordinates
        DebugCube.name = $"Chunk_Lod{Key.LODIndex}_({Key.Coordinates.x},{Key.Coordinates.y},{Key.Coordinates.z})";
        DebugCube.transform.position = Bounds.center;
        DebugCube.transform.localScale = Bounds.size;

        var renderer = DebugCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = LodColor(Key.LODIndex);
        }

        if (this.treeService.DebugCubeVisibility != OctTreeCubeVisibility.Active)
        {
            DebugCube.SetActive(true);
        }
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