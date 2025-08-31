using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tells the current content phase of a <see cref="ChunkLodTreeNode"/> to 
/// helper better understand what may be happening in the background so
/// actions are not duplicated.
/// </summary>
public enum ContentPhase { Unloaded, Loading, Ready, Subdivided }

/// <summary>
/// The LOD descision given to a <see cref="ChunkLodTreeNode"/> to help
/// with making the render decisions a bit simpler.
/// </summary>
public enum LodDecision { KeepLeaf, Subdivide, Merge }

/// <summary>
/// A transition happening with a <see cref="ChunkLodTreeNode"/> to help with
/// background processes to know what may be happening to not duplicate jobs.
/// </summary>
public enum Transition { None, Subdivide, Merge }

/// <summary>
/// Represents a single node in a <see cref="ChunkLodTree"/> holds information on the 
/// chunks current position and state.
/// </summary>
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
    public Transition Transition = Transition.None; // None, Subdivide, Merge

    // Helpers
    public bool IsAlive = false;
    public bool HasChildren => Children.Count != 0;
    public bool IsLeaf => !HasChildren;
    public Vector3Int Coordinates => Key.Coordinates;
    public int LODIndex => Key.LODIndex;

    /// <summary>
    /// Returns whether a <see cref="ChunkLodTreeNode"/> can safely subdivide based on its current state.
    /// </summary>
    /// <param name="desired"></param>
    /// <returns></returns>
    public bool CanSubdivide(int desired)
    {
        return Key.LODIndex > desired
            && Key.LODIndex != 0
            && IsLeaf
            && Phase != ContentPhase.Subdivided;
    }

    /// <summary>
    /// Returns whether a <see cref="ChunkLodTreeNode"/> can safely merge based on its current state.
    /// </summary>
    /// <param name="desired"></param>
    /// <returns></returns>
    public bool CanMerge(int desired)
    {
        return Key.LODIndex < desired && HasChildren;
    }

    /// <summary>
    /// Reset the current <see cref="ChunkLodTreeNode"/> back to zero so it may be safely reused.
    /// </summary>
    public void Free()
    {
        this.ParentIndex = -1;
        this.Children.Clear();
        this.ChildrenChecked = 0;
        this.Phase = ContentPhase.Unloaded;
        this.Transition = Transition.None;
        this.IsAlive = false;
    }

    /// <summary>
    /// Start a new <see cref="Transition"/> on this node telling whether background operations are in progress.
    /// </summary>
    /// <param name="newTransition"></param>
    /// <returns></returns>
    public bool StartTransition(Transition newTransition)
    {
        if (this.Transition != Transition.None)
            return false;

        this.Phase = ContentPhase.Loading;
        this.Transition = newTransition;

        return true;
    }

    /// <summary>
    /// Finalize a <see cref="Transition"/>, this is a helper function to make the code a bit cleaner to understand.
    /// </summary>
    /// <returns></returns>
    public bool FinishTransition()
    {
        if (this.Transition == Transition.None) 
            return false;

        switch (Transition)
        {
            case Transition.Merge:
                Phase = ContentPhase.Ready;
                break;

            case Transition.Subdivide:
                Phase = ContentPhase.Subdivided; // parent becomes internal
                break;
        }

        this.Transition = Transition.None;

        return true;
    }
}

/// <summary>
/// A chunk based tree structure to help with managing chunks in the game world by controlling detail by dividing the
/// chunks by 8 to create higher detailed chunks in its place.
/// </summary>
public class ChunkLodTree
{
    /// <summary>
    /// The lowest (I swear to god im going to change it so high is better detail) detail that can be used for a chunk.
    /// </summary>
    private const int RootLOD = 4;

    /// <summary>
    /// The amount of <see cref="ChunkLodTreeNode"/> that is updated per frame.
    /// </summary>
    private const int UpdatePerTick = 500;

    private readonly IChunkServices services;
    private readonly ChunkGenerationProcessor processor;

    private readonly List<ChunkLodTreeNode> Nodes = new();
    private readonly Dictionary<ChunkKey, int> IndexByKey = new();
    private readonly Stack<int> FreeSingleBlocks = new();

    /// <summary>
    /// The current index of <see cref="Update"/> as we limit the amount of nodes updated during an
    /// update to help streamline the process.
    /// </summary>
    private int CurrentUpdateIndex = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChunkLodTree"/> class.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="processor"></param>
    public ChunkLodTree(IChunkServices services, ChunkGenerationProcessor processor)
    {
        this.services = services;
        this.processor = processor;
    }

    /// <summary>
    /// Add a new root node into the LOD system. After adding the root the system will automatically manage
    /// the chunk and its children. The root coordinates should be sized to fit a <see cref="RootLOD"/> size.
    /// </summary>
    /// <param name="coord"></param>
    public void AddRoot(Vector3Int coord)
    {
        TryCreateSingleNode(new ChunkKey(coord, RootLOD));
    }

    /// <summary>
    /// Update the nodes part of this system. Updates will be staggered for the elements so this should be called
    /// every Unity update frame.
    /// </summary>
    public void Update()
    {
        int count = Nodes.Count;
        int processed = 0;

        while (processed < UpdatePerTick && count > 0)
        {
            if (CurrentUpdateIndex >= count)
                CurrentUpdateIndex = 0;

            if (Nodes[CurrentUpdateIndex].IsAlive)
                UpdateNode(CurrentUpdateIndex);

            CurrentUpdateIndex++;
            processed++;
        }
    }

    /// <summary>
    /// Update an instance of <see cref="ChunkLodTreeNode"/> element.
    /// </summary>
    /// <param name="index"></param>
    private void UpdateNode(int index)
    {
        var n = Nodes[index];

        if (n.Phase == ContentPhase.Loading) 
            return;

        var decision = GetLODDecision(n);
        if (decision == LodDecision.Subdivide) PerformSubdivide(index);
        else if (decision == LodDecision.Merge) PerformMerge(index);

        if (n.IsLeaf && n.Phase == ContentPhase.Unloaded)
            RequestGeneration(n);
    }

    /// <summary>
    /// Create a single block to be used for generation purposes.
    /// </summary>
    /// <returns></returns>
    private int AllocSingleBlock()
    {
        if (FreeSingleBlocks.Count > 0) return FreeSingleBlocks.Pop();
        Nodes.Add(new ChunkLodTreeNode());
        return Nodes.Count - 1;
    }

    /// <summary>
    /// Free a see <see cref="ChunkLodTreeNode"/> that can be sent back into collections.
    /// </summary>
    /// <param name="index"></param>
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

    /// <summary>
    /// Return the <see cref="LodDecision"/> based on a <see cref="ChunkLodTreeNode"/> current state.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public LodDecision GetLODDecision(ChunkLodTreeNode node)
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

    /// <summary>
    /// Perform a subdivide operation on a <see cref="ChunkLodTreeNode"/> based on index, dividing it by 8 
    /// and creating new children (if possible)
    /// </summary>
    /// <param name="index"></param>
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

    /// <summary>
    /// Perform a merge operation on a <see cref="ChunkLodTreeNode"/> based on index. Deleting its 8 children
    /// and making this the leaf again.
    /// </summary>
    /// <param name="index"></param>
    private void PerformMerge(int index)
    {
        ChunkLodTreeNode node = Nodes[index];
        node.StartTransition(Transition.Merge);

        // Request gen.
        this.RequestGeneration(node);
    }

    /// <summary>
    /// Try to create a single <see cref="ChunkLodTreeNode"/>. We will check if the chunk will have surface before
    /// rendering this system. We also take into account the parents state and if any additions should be made.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="parentIndex"></param>
    private void TryCreateSingleNode(ChunkKey key, int parentIndex = -1)
    {
        this.processor.RequestSurfaceCheck(key, (bool hasSurface) =>
        {
            ChunkLodTreeNode parent = null;

            if (parentIndex != -1)
            {
                parent = Nodes[parentIndex];
                parent.ChildrenChecked++;

                if (parent.ChildrenChecked == 8)
                {
                    parent.FinishTransition();
                    processor.RemoveChunk(parent.Key);
                }
            }

            if (hasSurface)
            {
                int index = AllocSingleBlock();
                ChunkLodTreeNode node = Nodes[index];

                node.Key = key;
                node.IsAlive = true;
                node.ParentIndex = parentIndex;

                if (parentIndex != -1 && parent != null)
                {
                    parent.Children.Add(index);
                }

                // Add to entry.
                Nodes[index] = node;
                IndexByKey.TryAdd(node.Key, index);
            }
        });
    }

    /// <summary>
    /// Request a <see cref="ChunkLodTreeNode"/> to be generated, this would mean a chunk has passed surface
    /// checks and is now ready to be seen in the world.
    /// </summary>
    /// <param name="node"></param>
    /// <exception cref="System.ArgumentException"></exception>
    private void RequestGeneration(ChunkLodTreeNode node)
    {
        node.Phase = ContentPhase.Loading;
        this.processor.RequestChunkGeneration(node.Key, (bool success) =>
        {
            if (success)
            {
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
                        FreeSingleBlock(child);
                    }

                    node.Children.Clear();
                    node.ChildrenChecked = 0;
                }

                node.FinishTransition();
            }
        });
    }

    /// <summary>
    /// Returns the child offset based on position in the <see cref="ChunkLodTree"/>.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="baseOffset"></param>
    /// <returns></returns>
    /// <exception cref="System.IndexOutOfRangeException"></exception>
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
}