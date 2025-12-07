using UnityEngine;

namespace Assets.Scripts.Terrain.Engine.Stage
{
    /// <summary>
    /// Small utility stage for running generic compute kernels that don't belong
    /// to the main terrain pipeline. Right now this only handles buffer clearing.
    /// </summary>
    public class UtilityStage
    {
        private readonly int clearKernel;
        private readonly ComputeShader utilityShader;

        /// <summary>
        /// Creates a new <see cref="UtilityStage"/> and wires up the ClearRange kernel.
        /// </summary>
        /// <param name="utility">Compute shader containing the utility kernels.</param>
        public UtilityStage(ComputeShader utility)
        {
            this.utilityShader = utility;
            clearKernel = this.utilityShader.FindKernel("ClearRange");
        }

        /// <summary>
        /// Dispatches the ClearRange kernel to zero out a span of a compute buffer.
        /// Useful for clearing counters, cursors, or per-chunk ranges before reuse.
        /// </summary>
        /// <param name="bufferToClear">Buffer that will be written to.</param>
        /// <param name="start">Starting index.</param>
        /// <param name="length">Number of elements to clear.</param>
        public void DispatchClear(ComputeBuffer bufferToClear, int start, int length)
        {
            utilityShader.SetInt("ClearStart", start);
            utilityShader.SetInt("ClearLength", length);
            utilityShader.SetBuffer(clearKernel, "ClearBuffer", bufferToClear);

            utilityShader.Dispatch(clearKernel, 1, 1, 1);
        }
    }
}
