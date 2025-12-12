using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GingerVoxelSystem.Engine.Stage
{
    /// <summary>
    /// Generates a localized volumetric SDF block used for entity collision.
    /// 
    /// This stage evaluates the full terrain density function on the GPU and
    /// outputs a low-resolution, conservative density volume centered on the entity.
    /// The resulting data is read back once and cached on the CPU for fast,
    /// per-frame collision queries (grounding, walls, ceilings).
    /// 
    /// Important:
    /// - This is NOT tied to render chunks or LOD.
    /// - GPU work happens only when the collision block is regenerated.
    /// - No per-frame GPU dispatch or readback.
    /// </summary>
    public class DensityCollisionStage : IDisposable
    {
        private readonly int collisionKernel;

        private readonly ComputeShader densityShader;
        private readonly ComputeBuffer outputBuffer;

        // Prevents overlapping GPU readbacks, which would otherwise
        // reintroduce stalls and defeat the point of caching.
        private bool readbackInFlight;

        /// <summary>
        /// Creates a new <see cref="DensityCollisionStage"/> and initializes the
        /// GPU resources required to generate a single collision SDF block.
        /// </summary>
        /// <param name="densityCollision">
        /// Compute shader containing the <c>GenerateCollisionBlock</c> kernel.
        /// This shader is expected to evaluate the full terrain SDF.
        /// </param>
        public DensityCollisionStage(ComputeShader densityCollision)
        {
            if (densityCollision == null)
            {
                throw new ArgumentNullException(nameof(densityCollision));
            }

            this.densityShader = densityCollision;

            // Kernel lookup
            this.collisionKernel = this.densityShader.FindKernel("GenerateCollisionBlock");

            // Collision block configuration.
            // These values intentionally do not change at runtime.
            this.densityShader.SetInt("BlockResolution", 32);
            this.densityShader.SetFloat("VoxelSpacing", 3.0f);
            this.densityShader.SetFloat("CollisionBias", 0.5f);

            // Output buffer holds a dense 32x32x32 grid of SDF samples.
            // This buffer is reused across regenerations.
            this.outputBuffer = new ComputeBuffer(32 * 32 * 32,sizeof(float),ComputeBufferType.Structured);

            this.densityShader.SetBuffer(this.collisionKernel,"DensityOutput",this.outputBuffer);
        }

        /// <summary>
        /// Dispatches the collision SDF generation kernel centered on the given
        /// world position. This should only be called when a new collision block
        /// is required (e.g. the entity exits the current block bounds).
        /// </summary>
        /// <param name="entityPos">World-space center position of the collision block.</param>
        public void DispatchCollision(Vector3 entityPos)
        {
            if (readbackInFlight)
                return;

            this.densityShader.SetVector("CenterPosition", entityPos);

            // 32 / 4 = 8 thread groups per axis
            this.densityShader.Dispatch(this.collisionKernel, 8, 8, 8);
        }

        /// <summary>
        /// Requests an async GPU readback of the generated collision block.
        /// The returned density array is suitable for CPU-side trilinear sampling.
        /// </summary>
        /// <param name="onSuccess">
        /// Callback invoked with the full SDF block once the readback completes.
        /// </param>
        public void RequestOutput(Action<float[]> onSuccess)
        {
            if (readbackInFlight)
                return;

            readbackInFlight = true;

            AsyncGPUReadback.Request(outputBuffer, req =>
            {
                readbackInFlight = false;

                if (req.hasError)
                    return;

                var data = req.GetData<float>();
                onSuccess?.Invoke(data.ToArray());
            });
        }

        /// <summary>
        /// Releases GPU resources owned by this stage.
        /// </summary>
        public void Dispose()
        {
            outputBuffer?.Release();
        }
    }
}
