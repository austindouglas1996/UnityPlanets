using System.Collections.Generic;
using UnityEngine;
using VHierarchy.Libs;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }
public enum LodDecision { KeepLeaf, Subdivide, Merge }
public enum Transition { None, Subdivide, Merge }

/// <summary>
/// One node in my octree. Handles one chunk of terrain at a certain LOD,
/// and can split into 8 smaller chunks or merge back up. 
/// Basically the unit that drives what gets generated or shown.
/// </summary>
public class ChunkOctTreeNode
{
    /// <summary>
    /// Shortcut back to the tree service so I can request surface checks and generation
    /// without filling this class with extra logic.
    /// </summary>
    private ChunkOctreeService treeService;

    /// <summary>
    /// What stage this chunk is in (not loaded, loading, ready, or subdivided).
    /// Helps me know whether to skip work on this node this tick.
    /// </summary>
    public ContentPhase CurrentContentPhase = ContentPhase.Unloaded;

    /// <summary>
    /// Whether this chunk actually has terrain in it. Unknown until I check,
    /// then it’s either Empty or NonEmpty.
    /// </summary>
    public OccupancyState CurrentOccupancyState = OccupancyState.Unknown;

    /// <summary>
    /// If I’m currently in the middle of a split or merge transition.
    /// </summary>
    public Transition CurrentTransition = Transition.None;

    /// <summary>
    /// Simple counter so transitions wait a few frames before finalizing,
    /// gives children time to come online or parent time to render.
    /// </summary>
    private int TransitionTicks = 0;

    /// <summary>
    /// How many children I’ve checked during a subdivide. 
    /// Prevents getting stuck if some fail.
    /// </summary>
    private int childrenChecked = 0;

    /// <summary>
    /// Debug cube I spawn just to visualize bounds and LOD while testing.
    /// </summary>
    private GameObject DebugCube;

    /// <summary>
    /// Create a new node. If I have a parent, this is one level deeper,
    /// otherwise I’m the root at max LOD. Kick off a surface check right away.
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
    /// Link back to my parent node (null if root).
    /// </summary>
    public ChunkOctTreeNode? Parent { get; private set; }

    /// <summary>
    /// My 8 children if I’ve been split. Only populated when subdivided.
    /// </summary>
    public List<ChunkOctTreeNode> Children = new List<ChunkOctTreeNode>(8);

    /// <summary>
    /// True if I’ve split into children.
    /// </summary>
    public bool HasChildren => this.Children.Count > 0;

    /// <summary>
    /// True if I don’t have children (leaf node).
    /// </summary>
    public bool IsLeaf => !HasChildren;

    /// <summary>
    /// Called on updates (not every Unity frame). Handles transitions,
    /// checks LOD, splits/merges, or requests generation.
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
    /// Kick off a subdivide. Request all 8 child surface checks, then wait a few ticks.
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
    /// Kick off a merge. Children will get cleared after a few ticks.
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
    /// Ask the service to see if this chunk has terrain in it.
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
    /// Ask the service to check one child coordinate during a subdivide.
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
    /// Tell the service to actually generate this chunk’s data.
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

                if (this.treeService.DebugCubeVisibility == OctTreeCubeVisibility.Active)
                {
                    // Create the debug cube.
                    this.CreateDebugCube();
                }
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
    /// Finish a subdivide: mark this node as replaced by children and clear my debug cube.
    /// </summary>
    private void FinalizeSubdivide()
    {
        this.CurrentContentPhase = ContentPhase.Subdivided;
        this.treeService.RemoveChild(this);

        // Delete debug cube.
        this.DebugCube.Destroy();
    }

    /// <summary>
    /// Finish a merge: remove my children and mark myself ready again.
    /// </summary>
    private void FinalizeMerge()
    {
        this.DestroyChildren();
        this.CurrentContentPhase = ContentPhase.Ready;
    }

    /// <summary>
    /// Recursively kill all children and unregister them from the tree service.
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
        Vector3Int lod0Coord = Key.Coordinates * (1 << Key.LODIndex);
        DebugCube.name = $"Chunk_Lod{Key.LODIndex}_LOD0({lod0Coord.x},{lod0Coord.y},{lod0Coord.z})";

        DebugCube.transform.position = Bounds.center;
        DebugCube.transform.localScale = Bounds.size;

        var renderer = DebugCube.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = LodColor(Key.LODIndex);
        }

        if (this.treeService.DebugCubeVisibility != OctTreeCubeVisibility.Active)
        {
            DebugCube.SetActive(true);
        }
    }

    /// <summary>
    /// Helper: pick a debug color based on LOD (green = far, red = near).
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