using ILGPU;
using ILGPU.Runtime;
using System;
using System.Runtime.CompilerServices;

namespace AI.ML.NeuralNetworks.Gpu.CuBlas;

/// <summary>
/// Helper methods to get device pointers from ILGPU ArrayViews.
/// </summary>
internal static class CuBlasExtensions
{
    /// <summary>
    /// Gets the native device pointer from an ArrayView (CUDA only).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IntPtr GetDevicePointer(this ArrayView<float> view)
    {
        return view.LoadEffectiveAddressAsPtr();
    }
}
