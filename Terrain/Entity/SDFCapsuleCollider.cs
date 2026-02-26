using GingerVoxelSystem.Core;
using GingerVoxelSystem.Engine.Stage;
using UnityEngine;

namespace GingerVoxelSystem.Entity
{
    /// <summary>
    /// Entity-level collision component backed by a cached SDF volume.
    /// 
    /// This component is responsible for:
    /// - Requesting localized terrain density from the GPU
    /// - Caching the resulting SDF block on the CPU
    /// - Performing per-frame collision queries against that cached data
    /// 
    /// It does not handle movement, input, or velocity integration.
    /// Those concerns are owned by higher-level entity controllers.
    /// </summary>
    public class SDFCapsuleCollider : MonoBehaviour
    {
        [SerializeField] private ComputeShader densityCollisionShader;
        private DensityCollisionStage collisionStage;

        /// <summary>
        /// This is illegal. Should be fixed someday.
        /// </summary>
        [SerializeField] private TerrainGeneratorMono ChunkServices;

        /// <summary>
        /// Force an update now.
        /// </summary>
        [SerializeField] private bool forceUpdateNow = false;

        /// <summary>
        /// Radius of the entity collision capsule.
        /// </summary>
        public float Radius = 0.35f;

        /// <summary>
        /// Total height of the entity collision capsule.
        /// </summary>
        public float Height = 1.8f;

        /// <summary>
        /// Indicates whether the entity is currently grounded based on
        /// SDF sampling results.
        /// </summary>
        public bool Grounded { get; private set; }

        // Cached SDF block data used for collision queries
        private float[] sdfBlock;
        private Vector3 blockOrigin;
        private int blockResolution;
        private float voxelSize;
        private float blockWorldSize;

        // Tracks whether the cached block is safe to sample
        private bool BlockValid;
        private bool blockRequested;

        // Last computed distance from the entity foot position to the SDF surface
        private float lastGroundDistance;

        /// <summary>
        /// Indicates whether collision resolution can be performed using
        /// the currently cached block.
        /// </summary>
        public bool CanResolve => BlockValid;

        /// <summary>
        /// Last ground distance computed during collision resolution.
        /// Useful for penetration correction by higher-level movement logic.
        /// </summary>
        public float LastGroundDistance => lastGroundDistance;

        /// <summary>
        /// Initializes the collision stage and immediately requests
        /// an initial SDF block centered on the entity.
        /// </summary>
        private void Awake()
        {
            collisionStage = new DensityCollisionStage(densityCollisionShader,ChunkServices);
            RequestNewBlock();
        }

        private void Update()
        {
            if (BlockValid)
            {
                ResolveGroundCPU();
            }

            if (forceUpdateNow)
            {
                forceUpdateNow = false;
                RequestNewBlock();
            }

            if (Input.GetKeyDown(KeyCode.J))
            {
                RequestNewBlock();
            }

            CheckBlockBounds();
        }

        private void OnDestroy()
        {
            collisionStage?.Dispose();
        }

        /// <summary>
        /// Resolves grounding by sampling the cached SDF block at the
        /// entity’s foot position and updating grounded state.
        /// 
        /// This method does not modify position or velocity.
        /// </summary>
        private void ResolveGroundCPU()
        {
            Vector3 foot = transform.position + Vector3.down * (Height * 0.5f - Radius);

            lastGroundDistance = SampleSdfBlock(foot);
            Grounded = lastGroundDistance < Radius + 0.02f;
        }

        /// <summary>
        /// Trilinearly samples the cached SDF block at a given world position.
        /// Positions outside the block are treated as empty space.
        /// </summary>
        /// <remarks>AI wrote this function ): I could not get it right.</remarks>
        private float SampleSdfBlock(Vector3 worldPos)
        {
            Vector3 local = (worldPos - blockOrigin) / voxelSize;

            int x0 = Mathf.FloorToInt(local.x);
            int y0 = Mathf.FloorToInt(local.y);
            int z0 = Mathf.FloorToInt(local.z);

            if (x0 < 0 || y0 < 0 || z0 < 0 ||
                x0 >= blockResolution - 1 ||
                y0 >= blockResolution - 1 ||
                z0 >= blockResolution - 1)
            {
                return 999f;
            }

            float fx = local.x - x0;
            float fy = local.y - y0;
            float fz = local.z - z0;

            int sx = blockResolution;
            int sy = blockResolution;

            int i000 = x0 + y0 * sx + z0 * sx * sy;
            int i100 = i000 + 1;
            int i010 = i000 + sx;
            int i110 = i010 + 1;
            int i001 = i000 + sx * sy;
            int i101 = i001 + 1;
            int i011 = i001 + sx;
            int i111 = i011 + 1;

            float c00 = Mathf.Lerp(sdfBlock[i000], sdfBlock[i100], fx);
            float c10 = Mathf.Lerp(sdfBlock[i010], sdfBlock[i110], fx);
            float c01 = Mathf.Lerp(sdfBlock[i001], sdfBlock[i101], fx);
            float c11 = Mathf.Lerp(sdfBlock[i011], sdfBlock[i111], fx);

            float c0 = Mathf.Lerp(c00, c10, fy);
            float c1 = Mathf.Lerp(c01, c11, fy);

            return Mathf.Lerp(c0, c1, fz);
        }

        /// <summary>
        /// Checks whether the entity has moved close to the edge of the
        /// cached collision block and requests a new one if necessary.
        /// </summary>
        private void CheckBlockBounds()
        {
            if (!BlockValid || blockRequested)
                return;

            Vector3 center = blockOrigin + Vector3.one * (blockWorldSize * 0.5f);
            Vector3 delta = transform.position - center;

            float threshold = blockWorldSize * 0.45f;

            if (Mathf.Abs(delta.x) > threshold ||
                Mathf.Abs(delta.y) > threshold ||
                Mathf.Abs(delta.z) > threshold)
            {
                RequestNewBlock();
            }
        }

        /// <summary>
        /// Requests a new collision SDF block centered on the current
        /// entity position.
        /// </summary>
        private void RequestNewBlock()
        {
            blockRequested = true;
            BlockValid = false;

            Vector3 center = transform.position;

            collisionStage.DispatchCollision(center);
            collisionStage.RequestOutput(data =>
            {
                const int RES = 32;
                const float SPACING = 3.0f;

                float halfExtent = (RES - 1) * 0.5f * SPACING;

                sdfBlock = data;
                blockOrigin = center - Vector3.one * halfExtent;
                blockResolution = RES;
                voxelSize = SPACING;
                blockWorldSize = (RES - 1) * SPACING;
                BlockValid = true;

                blockRequested = false;
            });
        }
    }
}
