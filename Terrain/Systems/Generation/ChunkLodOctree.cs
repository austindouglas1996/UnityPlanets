namespace UnityTerrainGenerator.Systems.Generation
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityTerrainGenerator.Core;
    using UnityTerrainGenerator.Engine.Generation;
    using UnityTerrainGenerator.Helpers;

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
        /// Tracks what stage a node is in:
        /// - Unloaded: not in memory
        /// - Loading: async request pending
        /// - Ready: mesh is generated
        /// - Subdivided: node has been replaced by children
        /// </summary>
        internal enum ContentPhase { Unloaded, Loading, Ready, Subdivided }

        /// <summary>
        /// LOD decision outcome when evaluating a node:
        /// - KeepLeaf: no change
        /// - Subdivide: split into children
        /// - Merge: collapse children back to parent
        /// </summary>
        internal enum LodDecision { KeepLeaf, Subdivide, Merge }

        /// <summary>
        /// Transient state during async operations to avoid double-scheduling.
        /// Nodes should only ever be in one transition at a time.
        /// </summary>
        internal enum Transition { None, Subdivide, Merge }

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
            /// Returns true if this node *can* safely subdivide given the desired LOD.
            /// Landmine: Must only subdivide leaves that are NonEmpty and not already subdivided.
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
            /// Returns true if this node *can* safely merge back to a parent LOD.
            /// Landmine: Only works if children exist.
            /// </summary>
            /// <param name="desired"></param>
            /// <returns></returns>
            public bool CanMerge(int desired)
            {
                return Key.LODIndex < desired && HasChildren;
            }

            /// <summary>
            /// Reset all fields so the node can be reused.
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
            /// Enter a transition state. Prevents double scheduling.
            /// Returns false if a transition was already in progress.
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
            /// Finish the current transition and update Phase accordingly.
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
        /// RootLOD is the coarsest level (biggest chunks).
        /// Personal note: this is inverted from what you'd expect ("higher" means *less* detail).
        /// </summary>
        private const int RootLOD = 6;

        /// <summary>
        /// Max number of nodes updated per Unity frame.
        /// This throttles Update() work to avoid spikes.
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
        /// Initializes a new instance of the <see cref="ChunkLodOctree"/> class.
        /// </summary>
        /// <param name="services"></param>
        /// <param name="processor"></param>
        public ChunkLodOctree(IChunkServices services, ChunkGenerationProcessor processor)
        {
            this.services = services;
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

        /// <summary>
        /// Called every frame. Processes up to UpdatePerTick nodes in round-robin fashion.
        /// Landmine: never loop over all nodes each frame — that killed perf in the old version.
        /// </summary>
        public void Update()
        {
            ConsoleTimer.Start("ChunkLODOctTree");

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

            ConsoleTimer.Stop("ChunkLODOctTree");
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
        /// Decide whether to keep, subdivide, or merge a node based on desired LOD.
        /// Skips nodes that are mid-transition.
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private LodDecision GetLODDecision(ChunkLodTreeNode node)
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
        /// Split node into 8 children. Each child gets a surface check before creation.
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
        /// Collapse children back to parent. Parent requests a new mesh generation.
        /// Landmine: children are only actually freed once the parent finishes loading.
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
        /// Request creation of a child node. Only allocates if the surface check passes.
        /// Also increments parent's ChildrenChecked count. 
        /// When all 8 callbacks return, parent transition is finished.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parentIndex"></param>
        private void TryCreateSingleNode(ChunkKey key, int parentIndex = -1)
        {
            this.processor.RequestSurfaceCheck(key, OnSurfaceCheckCompleted, parentIndex);
        }

        /// <summary>
        /// Request actual chunk generation (mesh build). Called for leaves that passed surface check. 
        /// Also used during merge to recreate parent mesh.
        /// Landmine: if this fails or you forget to clear children, you'll leak nodes.
        /// </summary>
        /// <param name="node"></param>
        /// <exception cref="System.ArgumentException"></exception>
        private void RequestGeneration(ChunkLodTreeNode node)
        {
            node.Phase = ContentPhase.Loading;
            this.processor.RequestChunkGeneration(node.Key, OnRequestGenerationCompleted);
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
        }

        /// <summary>
        /// Handle the execution of the generation job for
        /// </summary>
        /// <param name="node"></param>
        /// <param name="success"></param>
        /// <exception cref="System.ArgumentException"></exception>
        private void OnRequestGenerationCompleted(ChunkKey key, int parentIndex, bool success)
        {
            int nodeIndex = -1;
            if (!IndexByKey.TryGetValue(key, out nodeIndex))
                return;

            ChunkLodTreeNode node = Nodes[nodeIndex];

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
        private Vector3Int GetChildOffset(int index, Vector3Int baseOffset)
        {
            int cx = baseOffset.x;
            int cy = baseOffset.y;
            int cz = baseOffset.z;

            switch (index)
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
}