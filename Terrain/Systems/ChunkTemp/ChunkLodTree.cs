using System.Collections.Generic;
using UnityEngine;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }
public enum LodDecision { KeepLeaf, Subdivide, Merge }
public enum Transition { None, Subdivide, Merge }

public class ChunkLodTreeNode
{
    // Who I am
    public ChunkKey Key;
    public Bounds Bounds;

    // Relationships
    public int ParentIndex = -1;
    public int FirstChildIndex = -1;

    // State
    public ContentPhase Phase = ContentPhase.Unloaded;    // Unloaded, Loading, Ready, Subdivided
    public OccupancyState Occupancy = OccupancyState.Unknown; // Unknown, Loading, Empty, NonEmpty
    public Transition Transition = Transition.None; // None, Subdivide, Merge
    public int TransitionTicks = 0;

    // Helpers
    public bool IsAlive = false;
    public bool HasChildren => FirstChildIndex != -1;
    public bool IsLeaf => !HasChildren;
    public int LODIndex => Key.LODIndex;

    // Functions.
    public void Free()
    {
        this.ParentIndex = -1;
        this.FirstChildIndex = -1;
        this.Phase = ContentPhase.Unloaded;
        this.Occupancy = OccupancyState.Unknown;
        this.Transition = Transition.None;
        this.IsAlive = false;
    }
    public void StartSubdivide()
    {
        this.Transition = Transition.Subdivide;
        this.TransitionTicks = 3;
    }
    public void StartMerge()
    {
        this.Transition = Transition.Merge;
        this.TransitionTicks = 4;
    }

    public void RequestSurface(ChunkOctreeService service)
    {

    }
}

public class ChunkLodTree
{
    private const int RootLOD = 4;

    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;

    private List<ChunkLodTreeNode> Nodes = new();
    private Dictionary<ChunkKey, int> IndexByKey = new();

    private readonly Stack<int> FreeChildBlocks = new();
    private readonly Stack<int> FreeSingleBlocks = new();

    public ChunkLodTree(IChunkServices services, ChunkGenerationProcessor processor)
    {
        this.services = services;
        this.processor = processor;
    }

    public void AddRoot(Bounds bounds)
    {
        CreateSingleNode(bounds);
    }

    public void Update()
    {
        for (int i = 0; i < Nodes.Count; i++)
            if (Nodes[i].IsAlive)
                UpdateNode(i);
    }

    private void UpdateNode(int index)
    {
        var n = Nodes[index];

        if (n.Transition != Transition.None)
        {
            if (--n.TransitionTicks <= 0)
            {
                if (n.Transition == Transition.Subdivide) FinalizeSubdivide(n);
                else if (n.Transition == Transition.Merge) FinalizeMerge(n);

                n.Transition = Transition.None;
                n.TransitionTicks = 0;
            }
            return;
        }

        if (n.Phase == ContentPhase.Loading) return;
        if (n.Occupancy == OccupancyState.Empty || n.Occupancy == OccupancyState.Unknown) return;

        var decision = EvaluateLod(n);
        if (decision == LodDecision.Subdivide) PerformSubdivide(index);
        else if (decision == LodDecision.Merge) PerformMerge(index);

        if (n.IsLeaf && n.Phase == ContentPhase.Unloaded)
            RequestGeneration(n);
    }

    private int AllocSingleBlock()
    {
        if (FreeSingleBlocks.Count > 0) return FreeSingleBlocks.Pop();
        Nodes.Add(new ChunkLodTreeNode());
        return Nodes.Count - 1;
    }

    private int AllocChildBlock()
    {
        if (FreeChildBlocks.Count > 0) return FreeChildBlocks.Pop();
        int start = Nodes.Count;
        for (int i = 0; i < 8; i++) Nodes.Add(new ChunkLodTreeNode());
        return start;
    }

    private void FreeSingleBlock(int index)
    {
        var n = Nodes[index];

        // Remove children.
        if (n.FirstChildIndex != -1)
            FreeChildrenBlock(index);

        if (n.IsAlive)
            IndexByKey.Remove(n.Key);

        n.Free();

        FreeSingleBlocks.Push(index);
    }

    private void FreeChildrenBlock(int parentIndex)
    {
        var parent = Nodes[parentIndex];
        int startIndex = parent.FirstChildIndex;
        if (startIndex == -1) return;

        for (int i = 0; i < 8; i++)
        {
            int index = startIndex + i;
            var n = Nodes[index];

            if (n.ParentIndex != parentIndex)
            {
                Debug.LogWarning("OctTree parent tried to destroy child not owned by it.");
                continue;
            }

            if (!n.IsAlive)
            {
                continue;
            }

            // Destroy.
            this.processor.RemoveChunk(n.Key);

            IndexByKey.Remove(n.Key);
            n.Free();
        }

        // Reset parent.
        Nodes[parentIndex].FirstChildIndex = -1;
        FreeChildBlocks.Push(startIndex);
    }

    private void PerformSubdivide(int index)
    {
        ChunkLodTreeNode node = Nodes[index];

        if (node.HasChildren || node.LODIndex == 0 || node.Transition != Transition.None) return;
        node.Phase = ContentPhase.Loading;

        CreateChildNodes(index);

        node.StartSubdivide();
    }

    private void PerformMerge(int index)
    {
        ChunkLodTreeNode node = Nodes[index];

        if (!node.HasChildren || node.LODIndex == RootLOD || node.Transition != Transition.None) return;
        node.Phase = ContentPhase.Loading;

        FreeChildrenBlock(index);

        node.StartMerge();
    }

    private void RequestSurfaceCheck(ChunkLodTreeNode node)
    {
        this.processor.RequestSurfaceCheck(node.Key, (bool hasSurface) =>
        {
            if (hasSurface)
            {
                node.Occupancy = OccupancyState.NonEmpty;
            }
            else
            {
                node.Occupancy = OccupancyState.Empty;
                this.FreeSingleBlock(IndexByKey[node.Key]);
            }
        });
    }

    private void RequestGeneration(ChunkLodTreeNode node)
    {
        node.Phase = ContentPhase.Loading;
        this.processor.RequestChunkGeneration(node.Key, (bool success) =>
        {
            if (success)
            {
                node.Occupancy = OccupancyState.NonEmpty;
                node.Phase = ContentPhase.Ready;
            }
            else
            {
                node.Occupancy = OccupancyState.Empty;
                node.Phase = ContentPhase.Unloaded;
                FreeSingleBlock(IndexByKey[node.Key]);
            }
        });
    }

    private void FinalizeSubdivide(ChunkLodTreeNode node)
    {
        node.Phase = ContentPhase.Subdivided;
        this.processor.RemoveChunk(node.Key);
    }

    private void FinalizeMerge(ChunkLodTreeNode node)
    {
        node.Phase = ContentPhase.Ready;
    }

    private ChunkLodTreeNode CreateSingleNode(Bounds bounds, int parentIndex = -1)
    {
        int index = AllocSingleBlock();
        ChunkLodTreeNode node = Nodes[index];

        node.IsAlive = true;
        node.ParentIndex = parentIndex;
        node.Bounds = bounds;

        var LODIndex = parentIndex == -1 ? RootLOD : Nodes[parentIndex].LODIndex - 1;
        var coordinates = BoundsToCoordinate(bounds, LODIndex);
        node.Key = new ChunkKey(coordinates, LODIndex);

        // Add to entry.
        Nodes[index] = node;
        IndexByKey.TryAdd(node.Key, index);

        // Request a surface check before leaving.
        RequestSurfaceCheck(node);

        return node;
    }

    private bool CreateChildNodes(int parentIndex)
    {
        var parentNode = Nodes[parentIndex];
        if (parentNode.FirstChildIndex != -1 || parentNode.LODIndex == 0) return false;

        int start = AllocChildBlock();
        Nodes[parentIndex].FirstChildIndex = start;

        for (int i = 0; i < 8; i++)
        {
            int childIndex = i + start;
            var child = Nodes[childIndex];

            child.IsAlive = true;
            child.ParentIndex = parentIndex;

            var coordinates = GetChildOffset(i, parentNode.Key.Coordinates * 2);
            var lodIndex = parentNode.LODIndex - 1;
            child.Key = new ChunkKey(coordinates, lodIndex);
            child.Bounds = GetBounds(child.Key);

            IndexByKey.TryAdd(child.Key, childIndex);
            Nodes[childIndex] = child;

            // Request a surface check before leaving.
            RequestSurfaceCheck(child);
        }

        return true;
    }

    private Vector3Int GetChildOffset(int index, Vector3Int baseOffset)
    {
        int cx = baseOffset.x;
        int cy = baseOffset.y;
        int cz = baseOffset.z;

        switch(index)
        {
            case 0:
                return new Vector3Int(cx + 0, cy + 0, cz + 0);
            case 1:
                return new Vector3Int(cx + 1, cy + 0, cz + 0);
            case 2:
                return new Vector3Int(cx + 0, cy + 0, cz + 1);
            case 3:
                return new Vector3Int(cx + 1, cy + 0, cz + 1);
            case 4:
                return new Vector3Int(cx + 0, cy + 1, cz + 0);
            case 5:
                return new Vector3Int(cx + 1, cy + 1, cz + 0);
            case 6:
                return new Vector3Int(cx + 0, cy + 1, cz + 1);
            case 7:
                return new Vector3Int(cx + 1, cy + 1, cz + 1);
        }

        throw new System.IndexOutOfRangeException();
    }

    /// <summary>
    /// Evaluate the LOD for a given tree node to determine the best LOD.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public LodDecision EvaluateLod(ChunkLodTreeNode node)
    {
        // Don't touch while transitioning. 
        if (node.Transition != Transition.None)
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
    private bool ShouldSubdivide(ChunkLodTreeNode node, int desired)
    {
        return node.Key.LODIndex > desired
            && node.IsLeaf
            && node.Phase != ContentPhase.Subdivided
            && node.Occupancy == OccupancyState.NonEmpty;
    }

    private bool ShouldMerge(ChunkLodTreeNode node, int desired)
    {
        return node.Key.LODIndex < desired && node.HasChildren;
    }

    public Bounds GetBounds(ChunkKey key)
    {
        return this.services.Layout.GetBounds(key);
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