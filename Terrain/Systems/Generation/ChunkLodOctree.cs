namespace GingerVoxelSystem.Systems.Generation
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine.Generation;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using static UnityEngine.UI.Image;

    /// <summary>
    /// A chunk-based octree that manages world detail through Level of Detail (LOD).
    /// 
    /// This structure decides which chunks should exist in the world at any given time,
    /// keeping far-away areas coarse (few, large chunks) and nearby areas detailed
    /// (many, small chunks). Each node in the tree represents a chunk of the world
    /// and can either:
    ///   • Stay as a leaf (one chunk),
    ///   • Subdivide into 8 smaller child chunks (higher detail),
    ///   • Merge its children back into a single parent chunk (lower detail).
    /// 
    /// Why it matters:
    ///   • Chooses what chunks to render in the in-game world by logic only. No rendering.
    ///   • Dynamically adapts detail based on distance to the player.
    ///   • Ensures transitions between LOD levels without overlaps. 
    /// 
    /// In short: this class is the brain of the chunk system. It decides which chunks
    /// exist, when they should split apart for more detail, and when they can safely
    /// collapse back together to save performance.
    /// 
    /// Important: The logic here is delicate. Small changes can easily break LOD
    /// transitions or cause missing chunks. Read the comments carefully before editing.
    /// </summary>
    public class ChunkLodOctree
    {
        /// <summary>
        /// LOD decision outcome when evaluating a node:
        /// - KeepLeaf: no change
        /// - Subdivide: split into children
        /// - Merge: collapse children back to parent
        /// </summary>
        internal enum LodDecision
        {
            NoChange, // Do not change the node this time.
            Subdivide, // Divide the node into 8 pieces.
            Merge // Merge the node destroying its children.
        }

        /// <summary>
        /// Node state.
        /// </summary>
        internal enum NodeState
        {
            InActive,        // Element is freed awaiting to be set.
            IdleLeaf,        // Leaf, mesh ready
            AwaitGeneration, // Generation has been called, but awaiting confirmation.
            AwaitSubdivide,  // Subdivide requested, surface checks pending
            Subdivided,      // Children exist, parent hidden
            AwaitMerge,      // Merge requested, waiting on mesh gen
        }

        /// <summary>
        /// Represents a single node in the LOD tree.
        /// Stores chunk identity, parent/children links, and current state.
        /// </summary>
        internal class ChunkLodTreeNode
        {
            // Who I am
            public ChunkKey Key;

            // Relationships
            public int ParentIndex = -1;
            public List<int> Children = new List<int>(8);
            public int ChildrenChecked = 0;
            public int ChildrenExpected = 0;
            public int ChildrenReady = 0;

            // State
            public NodeState State;
            public uint LodEdgeMask = 0;
            public bool MeshReady = false;

            public bool IsLeaf => Children.Count == 0;

            // Helpers
            public int LODIndex => Key.LODIndex;

            /// <summary>
            /// Reset all fields so the node can be reused.
            /// </summary>
            public void Free()
            {
                this.MeshReady = false;
                this.ParentIndex = -1;
                this.Children.Clear();
                this.ChildrenExpected = 0;
                this.ChildrenChecked = 0;
                this.ChildrenReady = 0;
                this.State = NodeState.InActive;
                this.Key = ChunkKey.Invalid;
            }
        }

        /// <summary>
        /// RootLOD is the coarsest level (biggest chunks).
        /// Personal note: this is inverted from what you'd expect ("higher" means *less* detail).
        /// </summary>
        private const int RootLOD = 4;

        /// <summary>
        /// Max number of nodes updated per Unity frame.
        /// This throttles Update() work to avoid spikes.
        /// </summary>
        private const int UpdatePerTick = 550;

        private readonly ChunkMath math;
        private readonly Transform follower;
        private readonly IChunkConfiguration configuration;
        private readonly ChunkGenerationProcessor processor;

        private readonly List<ChunkLodTreeNode> Nodes = new();
        private readonly Dictionary<ChunkKey, int> IndexByKey = new();
        private readonly Dictionary<Vector3Int, int> IndexByOrigin = new();
        private readonly Stack<int> FreeSingleBlocks = new();

        /// <summary>
        /// The current index of <see cref="Update"/> as we limit the amount of nodes updated during an
        /// update to help streamline the process.
        /// </summary>
        private int CurrentUpdateIndex = 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChunkLodOctree"/> class.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="processor"></param>
        public ChunkLodOctree(IChunkConfiguration configuration, Transform follower, ChunkGenerationProcessor processor)
        {
            this.math = new ChunkMath(configuration);
            this.follower = follower;
            this.processor = processor;
        }

        /// <summary>
        /// The total amount of nodes.
        /// </summary>
        public int Count => Nodes.Count;

        /// <summary>
        /// Add a new root node into the LOD system. After adding the root the system will automatically manage
        /// the chunk and its children. The root coordinates should be sized to fit a <see cref="RootLOD"/> size.
        /// </summary>
        /// <param name="coord"></param>
        public void AddRoot(Vector3Int coord)
        {
            TryCreateSingleNode(new ChunkKey(coord, RootLOD));
        }

        public uint GetLODEdgeMask(ChunkKey key)
        {
            if (key.LODIndex == 0)
                return 0;

            uint mask = 0;

            Vector3Int origin0 = key.BaseCenter;
            int span = 1 << key.LODIndex; // how many LOD0 chunks this chunk spans

            for (int face = 0; face < 6; face++)
            {
                Vector3Int offset = ChunkMath.ChunkOffsets[face];

                Vector3Int neighborOrigin0 = origin0 + new Vector3Int(
                    offset.x * span,
                    offset.y * span,
                    offset.z * span
                );

                if (math.GetLODForChunk(neighborOrigin0, follower.position) < key.LODIndex)
                    mask |= 1u << face;
            }

            return mask;
        }


        /// <summary>
        /// Called every frame. Processes up to UpdatePerTick nodes in round-robin fashion.
        /// Landmine: never loop over all nodes each frame — that killed perf in the old version.
        /// </summary>
        public void Update()
        {
            int count = Nodes.Count;
            int processed = 0;

            while (processed < UpdatePerTick && count > 0)
            {
                if (CurrentUpdateIndex >= count)
                    CurrentUpdateIndex = 0;

                var node = Nodes[CurrentUpdateIndex];
                if (node.State == NodeState.IdleLeaf || node.State == NodeState.Subdivided)
                {
                    UpdateNode(CurrentUpdateIndex);
                }

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
            var node = Nodes[index];

            if (node.Key == ChunkKey.Invalid)
            {
                throw new System.ArgumentException("Invalid key provided for update.");
            }

            var decision = GetLODDecision(node);

            switch (node.State)
            {
                case NodeState.IdleLeaf:
                    if (decision == LodDecision.Subdivide)
                    {
                        PerformSubdivide(index);
                        return;
                    }

                    if (!node.MeshReady)
                    {
                        node.State = NodeState.AwaitGeneration;
                        this.processor.RequestChunkGeneration(node.Key, OnRequestGenerationCompleted);
                    }

                    // Check the chunk to see if it may need an 
                    // update with a new LOD edge mask being set.
                    uint newMask = GetLODEdgeMask(node.Key);
                    if (newMask != node.LodEdgeMask)
                    {
                        node.LodEdgeMask = newMask;
                        processor.RequestEdit(node.Key);
                    }

                    return;
                case NodeState.Subdivided:
                    if (decision == LodDecision.Merge)
                    {
                        PerformMerge(index);
                        return;
                    }
                    return;
                case NodeState.AwaitSubdivide:
                    return;
                case NodeState.AwaitMerge:
                    return;
                case NodeState.AwaitGeneration:
                    return;
            }
        }

        /// <summary>
        /// Create a single block to be used for generation purposes.
        /// </summary>
        /// <returns></returns>
        private int AllocSingleBlock()
        {
            if (FreeSingleBlocks.Count > 0)
                return FreeSingleBlocks.Pop();

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

            IndexByKey.Remove(n.Key);
            IndexByOrigin.Remove(n.Key.Origin);
            processor.RemoveChunk(n.Key);

            n.Free();
            FreeSingleBlocks.Push(index);
        }

        /// <summary>
        /// Decide whether to keep, subdivide, or merge a node based on desired LOD.
        /// Skips nodes that are mid-transition.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private LodDecision GetLODDecision(ChunkLodTreeNode node)
        {
            if (node.State == NodeState.AwaitSubdivide ||
                node.State == NodeState.AwaitMerge)
            {
                return LodDecision.NoChange;
            }

            int desired = math.GetLODForChunk(node.Key.BaseCenter, follower.position);

            if (node.State == NodeState.IdleLeaf)
            {
                if (node.LODIndex != 0 && node.LODIndex > desired)
                    return LodDecision.Subdivide;

                return LodDecision.NoChange;
            }

            if (node.State == NodeState.Subdivided)
            {
                if (node.LODIndex < RootLOD && node.LODIndex <= desired)
                    return LodDecision.Merge;

                return LodDecision.NoChange;
            }

            return LodDecision.NoChange;
        }

        /// <summary>
        /// Split node into 8 children. Each child gets a surface check before creation.
        /// </summary>
        /// <param name="index"></param>
        private void PerformSubdivide(int index)
        {
            ChunkLodTreeNode node = Nodes[index];
            node.State = NodeState.AwaitSubdivide;

            // Handle edge case: child subdivides before generating.
            // Only count it as resolved if the *parent* is in AwaitSubdivide.
            if (node.ParentIndex != -1)
            {
                var parent = Nodes[node.ParentIndex];
                if (parent.State == NodeState.AwaitSubdivide)
                    MarkChildResolvedIfHasParent(index);
            }

            int childLOD = node.Key.LODIndex - 1;
            for (int i = 0; i < 8; i++)
            {
                Vector3Int childOrigin = node.Key.Origin * 2 + GetChildOffset(i);
                TryCreateSingleNode(new ChunkKey(childOrigin, childLOD), index);
            }
        }

        /// <summary>
        /// Collapse children back to parent. Parent requests a new mesh generation.
        /// Landmine: children are only actually freed once the parent finishes loading.
        /// </summary>
        /// <param name="index"></param>
        private void PerformMerge(int index)
        {
            ChunkLodTreeNode node = Nodes[index];

            // Wait until all children finish
            foreach (var childIndex in node.Children)
                if (Nodes[childIndex].State != NodeState.IdleLeaf)
                    return; // keep waiting; UpdateNode will try again later

            // Set to merge.
            node.State = NodeState.AwaitMerge;

            if (!node.MeshReady)
            {
                // Request generation using our modified completion.
                this.processor.RequestChunkGeneration(node.Key, OnRequestGenerationMergeCompleted);
            }
            else
            {
                // Resolves an issue where merge happens before the parent mesh is destroyed.
                this.OnMergeComplete(node);
            }
        }

        /// <summary>
        /// Request creation of a child node. Only allocates if the surface check passes.
        /// Also increments parent's ChildrenChecked count. 
        /// When all 8 callbacks return, parent transition is finished.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parentIndex"></param>
        private void TryCreateSingleNode(ChunkKey key, int parentIndex = -1)
        {
            if (this.IndexByKey.ContainsKey(key))
                throw new ArgumentException("Asking to create a node with an existing key.");

            this.processor.RequestSurfaceCheck(key, OnSurfaceCheckCompleted, parentIndex);
        }

        /// <summary>
        /// Handle the execution of the surface check for a given node.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parentIndex"></param>
        /// <param name="hasSurface"></param>
        private void OnSurfaceCheckCompleted(ChunkKey key, int parentIndex, bool hasSurface)
        {
            ChunkLodTreeNode parent = null;

            // Should we notify the parent this child been checked?
            if (parentIndex != -1)
            {
                parent = Nodes[parentIndex];
                parent.ChildrenChecked++;
            }

            if (hasSurface)
            {
                int childIndex = AllocSingleBlock();
                var child = Nodes[childIndex];

                if (key == ChunkKey.Invalid)
                {
                    throw new System.ArgumentException("Invalid key returned on surface check.");
                }

                // If we have already seen this key skip.
                if (!IndexByKey.TryAdd(key, childIndex))
                {
                    FreeSingleBlock(childIndex);
                }
                else
                {
                    IndexByOrigin[key.Origin] = childIndex;

                    child.Key = key;
                    child.State = NodeState.IdleLeaf;
                    child.ParentIndex = parentIndex;

                    if (parentIndex != -1)
                    {
                        parent.ChildrenExpected++;
                        parent.Children.Add(childIndex);
                    }
                }
            }

            // Did we complete all 8 checks with no children?
            if (parentIndex != -1 && parent.ChildrenChecked == 8 && parent.ChildrenExpected == 0)
                parent.State = NodeState.IdleLeaf;
        }

        /// <summary>
        /// Handle the execution of the generation job for
        /// </summary>
        /// <param name="node"></param>
        /// <param name="success"></param>
        /// <exception cref="System.ArgumentException"></exception>
        private void OnRequestGenerationCompleted(ChunkKey key, int thisIsNotUsedIgnoreIt, bool success)
        {
            if (!success)
                throw new System.ArgumentException("Chunk generation failed.");

            if (!IndexByKey.TryGetValue(key, out int nodeIndex))
                throw new System.ArgumentNullException("Invalid key returned.");

            ChunkLodTreeNode node = Nodes[nodeIndex];
            node.MeshReady = true;
            node.State = NodeState.IdleLeaf;

            // Handle parent relationship.
            MarkChildResolvedIfHasParent(nodeIndex);
        }

        /// <summary>
        /// Handle the execution of the generation job for a node that is merging.
        /// </summary>
        /// <param name="node"></param>
        /// <param name="success"></param>
        /// <exception cref="System.ArgumentException"></exception>
        private void OnRequestGenerationMergeCompleted(ChunkKey key, int parentIndex, bool success)
        {
            if (!IndexByKey.TryGetValue(key, out int nodeIndex))
                throw new System.ArgumentNullException("Invalid key returned.");

            ChunkLodTreeNode node = Nodes[nodeIndex];
            if (!success)
                throw new System.ArgumentException("Chunk generation failed.");

            this.OnMergeComplete(node);
        }

        /// <summary>
        /// Finalize the merge process by killing the children of a node.
        /// </summary>
        /// <param name="node"></param>
        private void OnMergeComplete(ChunkLodTreeNode node)
        {
            // The children can be safely removed now.
            foreach (var child in node.Children)
            {
                FreeSingleBlock(child);
            }

            node.Children.Clear();
            node.ChildrenExpected = 0;
            node.ChildrenChecked = 0;
            node.ChildrenReady = 0;

            node.MeshReady = true;
            node.State = NodeState.IdleLeaf;
        }

        /// <summary>
        /// Increase the count of ready children for a given node.
        /// </summary>
        /// <param name="childNode"></param>
        private void MarkChildResolvedIfHasParent(int childNode)
        {
            ChunkLodTreeNode node = Nodes[childNode];
            if (node.ParentIndex == -1)
                return;

            ChunkLodTreeNode parent = Nodes[node.ParentIndex];
            parent.ChildrenReady++;

            if (parent.ChildrenReady == parent.ChildrenExpected &&
                parent.ChildrenChecked == 8)
            {
                parent.MeshReady = false;
                parent.State = NodeState.Subdivided;
                processor.RemoveChunk(parent.Key);
            }
        }

        /// <summary>
        /// Returns offset coordinates for one of the 8 children in a subdivide.
        /// IMPORTANT: this must stay consistent with your chunk layout math,
        /// otherwise neighbors will not line up.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="baseOffset"></param>
        /// <returns></returns>
        /// <exception cref="System.IndexOutOfRangeException"></exception>
        private static Vector3Int GetChildOffset(int index)
        {
            return new Vector3Int(
                (index & 1) != 0 ? 1 : 0,
                (index & 2) != 0 ? 1 : 0,
                (index & 4) != 0 ? 1 : 0);
        }
    }
}