using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OccupancyState { Unknown, Loading, Empty, NonEmpty }
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }
public enum LodDecision { KeepLeaf, Subdivide, Merge }
public enum Transition { None, Subdivide, Merge }

public class ChunkLodTreeNode
{
    // Who I am
    public ChunkKey Key;

    // Relationships
    public int ParentIndex = -1;
    public int[] Children = new int[8]; 
    public int ChildCount = 0;

    // State
    public ContentPhase Phase = ContentPhase.Unloaded;    // Unloaded, Loading, Ready, Subdivided
    public OccupancyState Occupancy = OccupancyState.Unknown; // Unknown, Loading, Empty, NonEmpty
    public Transition Transition = Transition.None; // None, Subdivide, Merge
    public int TransitionTicks = 0;

    // Helpers
    public bool IsAlive = false;
    public bool HasChildren => ChildCount != 0;
    public bool IsLeaf => !HasChildren;
    public int LODIndex => Key.LODIndex;

    public void AddChild(int idx)
    {
        for (int i = 0; i < 8; i++)
        {
            if (Children[i] == 0)
            {
                Children[i] = idx;
                ChildCount++;
                return;
            }
        }
    }
    public void RemoveChild(int idx)
    {
        for (int i = 0; i < 8; i++)
        {
            if (Children[i] == idx)
            {
                Children[i] = 0;
                ChildCount--;
                return;
            }
        }
    }
    public void ClearChildren()
    {
        for (int i = 0; i < 8; i++) Children[i] = 0;
        ChildCount = 0;
    }
    public IEnumerable<int> GetChildren()
    {
        for (int i = 0; i < 8; i++)
            if (Children[i] != 0)
                yield return Children[i];
    }

    // Functions.
    public void Free()
    {
        this.ParentIndex = -1;
        this.ClearChildren();
        this.ChildCount = 0;
        this.Phase = ContentPhase.Unloaded;
        this.Occupancy = OccupancyState.Unknown;
        this.Transition = Transition.None;
        this.TransitionTicks = 0;
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
}

public class ChunkLodTree
{
    private const int RootLOD = 4;

    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;

    private List<ChunkLodTreeNode> Nodes = new();
    private Dictionary<ChunkKey, int> IndexByKey = new();

    private readonly Stack<int> FreeSingleBlocks = new();

    public ChunkLodTree(IChunkServices services, ChunkGenerationProcessor processor)
    {
        this.services = services;
        this.processor = processor;
    }

    public void AddRoot(Vector3Int coord)
    {
        TryCreateSingleNode(new ChunkKey(coord, RootLOD));
    }

    public void Update()
    {
        for (int i = 0; i < Nodes.Count; i++)
            if (Nodes[i].IsAlive)
                UpdateNode(i);

        DumpLodHistogram(this.Nodes);
    }

    void DumpLodHistogram(List<ChunkLodTreeNode> nodes)
    {
        int[] h = new int[8];
        int alive = 0;
        foreach (var n in nodes) if (n.IsAlive) { h[Mathf.Clamp(n.LODIndex, 0, 7)]++; alive++; }
        Debug.Log($"Alive={alive}  LODs: " +
                  string.Join(" ", System.Linq.Enumerable.Range(0, h.Length).Select(i => $"{i}:{h[i]}")));
    }


    private void UpdateNode(int index)
    {
        var n = Nodes[index];

        if (n.Transition != Transition.None)
        {
            if (--n.TransitionTicks <= 0)
            {
                if (n.Transition == Transition.Subdivide) FinalizeSubdivide(n);
                else if (n.Transition == Transition.Merge) FinalizeMerge(index);

                n.Transition = Transition.None;
                n.TransitionTicks = 0;
            }
            return;
        }

        if (n.Phase == ContentPhase.Loading) 
            return;

        if (n.Occupancy == OccupancyState.Empty) return;

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

    private void FreeSingleBlock(int index)
    {
        var n = Nodes[index];
        if (n.IsAlive)
        {
            IndexByKey.Remove(n.Key);
            processor.RemoveChunk(n.Key);
        }
        n.Free();
        FreeSingleBlocks.Push(index);
    }

    private void FreeChildrenBlock(int parentIndex)
    {
        var parent = Nodes[parentIndex];

        foreach (var child in parent.Children)
        {
            FreeSingleBlock(child);
        }

        // Reset parent.
        Nodes[parentIndex].ClearChildren();
    }

    private void PerformSubdivide(int index)
    {
        ChunkLodTreeNode node = Nodes[index];

        if (node.HasChildren || node.LODIndex == 0 || node.Transition != Transition.None) return;
        node.Phase = ContentPhase.Loading;

        CreateChildNodes(index);
    }

    private void PerformMerge(int index)
    {
        ChunkLodTreeNode node = Nodes[index];

        if (!node.HasChildren || node.LODIndex == RootLOD || node.Transition != Transition.None) return;
        node.Phase = ContentPhase.Loading;

        // Request gen.
        this.RequestGeneration(node);
        node.StartMerge();
    }

    private void TryCreateSingleNode(ChunkKey key, int parentIndex = -1)
    {
        this.processor.RequestSurfaceCheck(key, (bool hasSurface) =>
        {
            if (!hasSurface)
                return;

            int index = AllocSingleBlock();
            ChunkLodTreeNode node = Nodes[index];

            node.Key = key;
            node.IsAlive = true;
            node.ParentIndex = parentIndex;
            node.Occupancy = OccupancyState.NonEmpty;

            if (parentIndex != -1)
            {
                var pNode = Nodes[parentIndex];
                pNode.AddChild(index);

                // Start to free the parent.
                pNode.StartSubdivide();
            }

            // Add to entry.
            Nodes[index] = node;
            IndexByKey.TryAdd(node.Key, index);
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

    private void FinalizeMerge(int index)
    {
        FreeChildrenBlock(index);
        Nodes[index].Phase = ContentPhase.Ready;
    }

    private bool CreateChildNodes(int parentIndex)
    {
        var parentNode = Nodes[parentIndex];
        if (parentNode.HasChildren || parentNode.LODIndex == 0) return false;

        for (int i = 0; i < 8; i++)
        {
            var coordinates = GetChildOffset(i, parentNode.Key.Coordinates * 2);
            var lodIndex = parentNode.LODIndex - 1;
            var chunkKey = new ChunkKey(coordinates, lodIndex);
            TryCreateSingleNode(chunkKey, parentIndex);
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
}