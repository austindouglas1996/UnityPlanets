using DistantLands.Cozy;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Rendering;
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
    public List<int> Children = new List<int>();
    public int ChildrenChecked = 0;

    // State
    public ContentPhase Phase = ContentPhase.Unloaded;    // Unloaded, Loading, Ready, Subdivided
    public OccupancyState Occupancy = OccupancyState.Unknown; // Unknown, Loading, Empty, NonEmpty
    public Transition Transition = Transition.None; // None, Subdivide, Merge

    // Helpers
    public bool IsAlive = false;
    public bool MarkedForRemoval = false;
    public bool HasChildren => Children.Count != 0;
    public bool IsLeaf => !HasChildren;
    public int LODIndex => Key.LODIndex;

    public bool CanSubdivide(int desired)
    {
        return Key.LODIndex > desired
            && Key.LODIndex != 0
            && IsLeaf
            && Phase != ContentPhase.Subdivided
            && Occupancy == OccupancyState.NonEmpty;
    }
    public bool CanMerge(int desired)
    {
        return Key.LODIndex < desired && HasChildren;
    }

    // Functions.
    public void Free()
    {
        this.ParentIndex = -1;
        this.Children.Clear();
        this.ChildrenChecked = 0;
        this.Phase = ContentPhase.Unloaded;
        this.Occupancy = OccupancyState.Unknown;
        this.Transition = Transition.None;
        this.IsAlive = false;
    }

    public bool StartTransition(Transition newTransition)
    {
        if (this.Transition != Transition.None)
            return false;

        this.Phase = ContentPhase.Loading;
        this.Transition = newTransition;

        return true;
    }

    public bool FinishTransition()
    {
        if (this.Transition == Transition.None) 
            return false;

        switch (Transition)
        {
            case Transition.Merge:
                Phase = ContentPhase.Ready;
                Occupancy = OccupancyState.NonEmpty;         // parent visible again
                break;

            case Transition.Subdivide:
                Phase = ContentPhase.Subdivided;             // parent becomes internal
                break;
        }

        this.Transition = Transition.None;

        return true;
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
    }

    private void UpdateNode(int index)
    {
        var n = Nodes[index];

        if (n.MarkedForRemoval)
        {
            this.processor.RemoveChunk(n.Key);
            FreeSingleBlock(index);
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

    private void PerformSubdivide(int index)
    {
        ChunkLodTreeNode node = Nodes[index];
        node.StartTransition(Transition.Subdivide);

        for (int i = 0; i < 8; i++)
        {
            var coordinates = GetChildOffset(i, node.Key.Coordinates * 2);
            var lodIndex = node.LODIndex - 1;
            var chunkKey = new ChunkKey(coordinates, lodIndex);
            TryCreateSingleNode(chunkKey, index);
        }
    }

    private void PerformMerge(int index)
    {
        ChunkLodTreeNode node = Nodes[index];
        node.StartTransition(Transition.Merge);

        // Request gen.
        this.RequestGeneration(node);
    }

    private void TryCreateSingleNode(ChunkKey key, int parentIndex = -1)
    {
        this.processor.RequestSurfaceCheck(key, (bool hasSurface) =>
        {
            ChunkLodTreeNode parent = null;

            if (parentIndex != -1)
            {
                parent = Nodes[parentIndex];
                parent.ChildrenChecked++;
            }

            if (hasSurface)
            {
                int index = AllocSingleBlock();
                ChunkLodTreeNode node = Nodes[index];

                node.Key = key;
                node.IsAlive = true;
                node.ParentIndex = parentIndex;
                node.Occupancy = OccupancyState.NonEmpty;

                if (parentIndex != -1 && parent != null)
                {
                    parent.Children.Add(index);

                    if (parent.ChildrenChecked == 8)
                    {
                        parent.FinishTransition();
                        processor.RemoveChunk(parent.Key);
                    }
                }

                // Add to entry.
                Nodes[index] = node;
                IndexByKey.TryAdd(node.Key, index);
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
                throw new System.ArgumentException("Chunk generation failed.");
            }

            // Was this a merge request?
            if (node.Transition == Transition.Merge)
            {
                if (success)
                {
                    // The children can be safely removed now.
                    foreach (var child in node.Children)
                    {
                        ChunkLodTreeNode cnode = Nodes[child];
                        cnode.MarkedForRemoval = true;
                    }
                }

                node.FinishTransition();
            }
        });
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

    public LodDecision EvaluateLod(ChunkLodTreeNode node)
    {
        // Don't touch while transitioning. 
        if (node.Transition != Transition.None)
            return LodDecision.KeepLeaf;

        int desired = services.Layout.GetLODForChunk(node.Key.Global);

        if (node.CanSubdivide(desired))
            return LodDecision.Subdivide;

        if (node.CanMerge(desired))
            return LodDecision.Merge;

        return LodDecision.KeepLeaf;
    }

    void DumpLodHistogram(List<ChunkLodTreeNode> nodes)
    {
        int[] h = new int[8];
        int alive = 0;
        foreach (var n in nodes) if (n.IsAlive) { h[Mathf.Clamp(n.LODIndex, 0, 7)]++; alive++; }
        Debug.Log($"Alive={alive}  LODs: " +
                  string.Join(" ", System.Linq.Enumerable.Range(0, h.Length).Select(i => $"{i}:{h[i]}")));
    }
}