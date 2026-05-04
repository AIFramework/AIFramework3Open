using System;
using System.Runtime.InteropServices;

namespace AI.ComputerVision.FrequencyDomain;

/// <summary>
/// Вычислительный бэкенд для 2D FFT.
/// </summary>
public enum FftBackend
{
    /// <summary>CPU: Parallel.For + Fft64 (всегда доступен)</summary>
    Cpu = 0,
    /// <summary>GPU: cuFFT через CUDA Toolkit (требуется cufft64 + cudart64)</summary>
    Cuda = 1,
}

/// <summary>
/// Информация о доступности CUDA-бэкенда для FFT.
/// Результат кэшируется при первом обращении.
/// </summary>
public static class CudaFftInfo
{
    private static readonly Lazy<(bool ok, string name)> _probe = new(Probe);

    /// <summary>Доступен ли cuFFT</summary>
    public static bool IsAvailable => _probe.Value.ok;

    /// <summary>Имя найденной DLL (или причина недоступности)</summary>
    public static string StatusMessage => _probe.Value.name;

    private static (bool, string) Probe()
    {
        try
        {
            foreach (var pair in CuFftBindings.Candidates)
            {
                try
                {
                    var b = CuFftBindings.For(pair.fft, pair.rt);
                    IntPtr devPtr;
                    int rc = b.cudaMalloc(out devPtr, (IntPtr)8);
                    if (rc != 0) continue;
                    b.cudaFree(devPtr);

                    IntPtr namePtr = b.cudaGetDeviceName();
                    string devName = namePtr != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(namePtr) ?? pair.fft
                        : pair.fft;

                    return (true, $"CUDA: {devName} ({pair.fft})");
                }
                catch (DllNotFoundException) { }
                catch (EntryPointNotFoundException) { }
                catch (BadImageFormatException) { }
                catch { }
            }
        }
        catch
        {
            return (false, "CUDA недоступна (ошибка при загрузке DLL)");
        }
        return (false, "CUDA недоступна (cufft64/cudart64 не найдены)");
    }
}

/// <summary>
/// Выполняет 2D FFT на GPU через cuFFT. Одноразовый: создай -> exec -> dispose.
/// </summary>
internal sealed class CuFftHandle : IDisposable
{
    private readonly ICuFftBindings _b;
    private IntPtr _plan;
    private bool _disposed;

    public bool IsValid => _plan != IntPtr.Zero;

    private CuFftHandle(ICuFftBindings bindings, int rows, int cols, bool forward)
    {
        _b = bindings;
        int rc = _b.cufftPlan2d(out _plan, rows, cols, 0x69 /* CUFFT_Z2Z */);
        if (rc != 0) _plan = IntPtr.Zero;
    }

    /// <summary>
    /// Выполняет 2D FFT (forward или inverse) для double complex на GPU.
    /// data — чередующийся массив [re0, im0, re1, im1, ...] длиной rows*cols*2.
    /// </summary>
    public bool Exec2D(double[] data, int rows, int cols, bool forward)
    {
        if (!IsValid) return false;
        long n = (long)rows * cols;
        long byteSize = n * 2 * sizeof(double);

        int rc = _b.cudaMalloc(out IntPtr devPtr, (IntPtr)byteSize);
        if (rc != 0) return false;

        try
        {
            rc = _b.cudaMemcpy(devPtr, data, (IntPtr)byteSize, 1 /* H2D */);
            if (rc != 0) return false;

            int dir = forward ? -1 : 1; // CUFFT_FORWARD=-1, CUFFT_INVERSE=1
            rc = _b.cufftExecZ2Z(_plan, devPtr, devPtr, dir);
            if (rc != 0) return false;

            rc = _b.cudaMemcpy(data, devPtr, (IntPtr)byteSize, 2 /* D2H */);
            if (rc != 0) return false;

            if (!forward)
            {
                double inv = 1.0 / n;
                for (long i = 0; i < data.Length; i++)
                    data[i] *= inv;
            }

            return true;
        }
        finally
        {
            _b.cudaFree(devPtr);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_plan != IntPtr.Zero)
        {
            _b.cufftDestroy(_plan);
            _plan = IntPtr.Zero;
        }
    }

    /// <summary>
    /// Пытается создать cuFFT-план. Возвращает null, если CUDA недоступна.
    /// </summary>
    public static CuFftHandle TryCreate(int rows, int cols, bool forward = true)
    {
        foreach (var pair in CuFftBindings.Candidates)
        {
            try
            {
                var b = CuFftBindings.For(pair.fft, pair.rt);
                var h = new CuFftHandle(b, rows, cols, forward);
                if (h.IsValid) return h;
                h.Dispose();
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (BadImageFormatException) { }
            catch { }
        }
        return null;
    }
}

#region P/Invoke биндинги (cuFFT + cudart)

internal interface ICuFftBindings
{
    int cufftPlan2d(out IntPtr plan, int nx, int ny, int type);
    int cufftExecZ2Z(IntPtr plan, IntPtr idata, IntPtr odata, int direction);
    int cufftDestroy(IntPtr plan);

    int cudaMalloc(out IntPtr devPtr, IntPtr size);
    int cudaFree(IntPtr devPtr);
    int cudaMemcpy(IntPtr dst, double[] src, IntPtr count, int kind);
    int cudaMemcpy(double[] dst, IntPtr src, IntPtr count, int kind);

    IntPtr cudaGetDeviceName();
}

internal static class CuFftBindings
{
    public static readonly (string fft, string rt)[] Candidates =
    {
        ("cufft64_12", "cudart64_12"),
        ("cufft64_11", "cudart64_110"),
        ("cufft64_10", "cudart64_102"),
        ("cufft64_10", "cudart64_101"),
    };

    public static ICuFftBindings For(string fft, string rt) => (fft, rt) switch
    {
        ("cufft64_12", "cudart64_12")   => new CuFft12(),
        ("cufft64_11", "cudart64_110")  => new CuFft11(),
        ("cufft64_10", "cudart64_102")  => new CuFft10_102(),
        ("cufft64_10", "cudart64_101")  => new CuFft10_101(),
        _ => throw new ArgumentException($"Unknown cuFFT pair: {fft}/{rt}"),
    };
}

#region CUDA 12

file sealed class CuFft12 : ICuFftBindings
{
    private const string Fft = "cufft64_12";
    private const string Rt  = "cudart64_12";

    [DllImport(Fft)] private static extern int cufftPlan2d(out IntPtr plan, int nx, int ny, int type);
    [DllImport(Fft)] private static extern int cufftExecZ2Z(IntPtr plan, IntPtr idata, IntPtr odata, int direction);
    [DllImport(Fft)] private static extern int cufftDestroy(IntPtr plan);

    [DllImport(Rt)] private static extern int cudaMalloc(out IntPtr devPtr, IntPtr size);
    [DllImport(Rt)] private static extern int cudaFree(IntPtr devPtr);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyH2D(IntPtr dst, double[] src, IntPtr count, int kind);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyD2H(double[] dst, IntPtr src, IntPtr count, int kind);

    [DllImport(Rt, EntryPoint = "cudaGetDeviceProperties")]
    private static extern int cudaGetDeviceProperties(byte[] prop, int device);

    int ICuFftBindings.cufftPlan2d(out IntPtr p, int nx, int ny, int t) => cufftPlan2d(out p, nx, ny, t);
    int ICuFftBindings.cufftExecZ2Z(IntPtr p, IntPtr i, IntPtr o, int d) => cufftExecZ2Z(p, i, o, d);
    int ICuFftBindings.cufftDestroy(IntPtr p) => cufftDestroy(p);
    int ICuFftBindings.cudaMalloc(out IntPtr p, IntPtr s) => cudaMalloc(out p, s);
    int ICuFftBindings.cudaFree(IntPtr p) => cudaFree(p);
    int ICuFftBindings.cudaMemcpy(IntPtr dst, double[] src, IntPtr c, int k) => cudaMemcpyH2D(dst, src, c, k);
    int ICuFftBindings.cudaMemcpy(double[] dst, IntPtr src, IntPtr c, int k) => cudaMemcpyD2H(dst, src, c, k);
    IntPtr ICuFftBindings.cudaGetDeviceName()
    {
        try
        {
            var prop = new byte[1024];
            int rc = cudaGetDeviceProperties(prop, 0);
            if (rc != 0) return IntPtr.Zero;
            int len = Array.IndexOf(prop, (byte)0);
            if (len <= 0) len = 256;
            return Marshal.StringToHGlobalAnsi(
                System.Text.Encoding.ASCII.GetString(prop, 0, Math.Min(len, 256)).Trim());
        }
        catch { return IntPtr.Zero; }
    }
}

#endregion

#region CUDA 11

file sealed class CuFft11 : ICuFftBindings
{
    private const string Fft = "cufft64_11";
    private const string Rt  = "cudart64_110";

    [DllImport(Fft)] private static extern int cufftPlan2d(out IntPtr plan, int nx, int ny, int type);
    [DllImport(Fft)] private static extern int cufftExecZ2Z(IntPtr plan, IntPtr idata, IntPtr odata, int direction);
    [DllImport(Fft)] private static extern int cufftDestroy(IntPtr plan);

    [DllImport(Rt)] private static extern int cudaMalloc(out IntPtr devPtr, IntPtr size);
    [DllImport(Rt)] private static extern int cudaFree(IntPtr devPtr);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyH2D(IntPtr dst, double[] src, IntPtr count, int kind);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyD2H(double[] dst, IntPtr src, IntPtr count, int kind);

    [DllImport(Rt, EntryPoint = "cudaGetDeviceProperties")]
    private static extern int cudaGetDeviceProperties(byte[] prop, int device);

    int ICuFftBindings.cufftPlan2d(out IntPtr p, int nx, int ny, int t) => cufftPlan2d(out p, nx, ny, t);
    int ICuFftBindings.cufftExecZ2Z(IntPtr p, IntPtr i, IntPtr o, int d) => cufftExecZ2Z(p, i, o, d);
    int ICuFftBindings.cufftDestroy(IntPtr p) => cufftDestroy(p);
    int ICuFftBindings.cudaMalloc(out IntPtr p, IntPtr s) => cudaMalloc(out p, s);
    int ICuFftBindings.cudaFree(IntPtr p) => cudaFree(p);
    int ICuFftBindings.cudaMemcpy(IntPtr dst, double[] src, IntPtr c, int k) => cudaMemcpyH2D(dst, src, c, k);
    int ICuFftBindings.cudaMemcpy(double[] dst, IntPtr src, IntPtr c, int k) => cudaMemcpyD2H(dst, src, c, k);
    IntPtr ICuFftBindings.cudaGetDeviceName()
    {
        try
        {
            var prop = new byte[1024];
            int rc = cudaGetDeviceProperties(prop, 0);
            if (rc != 0) return IntPtr.Zero;
            int len = Array.IndexOf(prop, (byte)0);
            if (len <= 0) len = 256;
            return Marshal.StringToHGlobalAnsi(
                System.Text.Encoding.ASCII.GetString(prop, 0, Math.Min(len, 256)).Trim());
        }
        catch { return IntPtr.Zero; }
    }
}

#endregion

#region CUDA 10.2

file sealed class CuFft10_102 : ICuFftBindings
{
    private const string Fft = "cufft64_10";
    private const string Rt  = "cudart64_102";

    [DllImport(Fft)] private static extern int cufftPlan2d(out IntPtr plan, int nx, int ny, int type);
    [DllImport(Fft)] private static extern int cufftExecZ2Z(IntPtr plan, IntPtr idata, IntPtr odata, int direction);
    [DllImport(Fft)] private static extern int cufftDestroy(IntPtr plan);

    [DllImport(Rt)] private static extern int cudaMalloc(out IntPtr devPtr, IntPtr size);
    [DllImport(Rt)] private static extern int cudaFree(IntPtr devPtr);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyH2D(IntPtr dst, double[] src, IntPtr count, int kind);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyD2H(double[] dst, IntPtr src, IntPtr count, int kind);

    [DllImport(Rt, EntryPoint = "cudaGetDeviceProperties")]
    private static extern int cudaGetDeviceProperties(byte[] prop, int device);

    int ICuFftBindings.cufftPlan2d(out IntPtr p, int nx, int ny, int t) => cufftPlan2d(out p, nx, ny, t);
    int ICuFftBindings.cufftExecZ2Z(IntPtr p, IntPtr i, IntPtr o, int d) => cufftExecZ2Z(p, i, o, d);
    int ICuFftBindings.cufftDestroy(IntPtr p) => cufftDestroy(p);
    int ICuFftBindings.cudaMalloc(out IntPtr p, IntPtr s) => cudaMalloc(out p, s);
    int ICuFftBindings.cudaFree(IntPtr p) => cudaFree(p);
    int ICuFftBindings.cudaMemcpy(IntPtr dst, double[] src, IntPtr c, int k) => cudaMemcpyH2D(dst, src, c, k);
    int ICuFftBindings.cudaMemcpy(double[] dst, IntPtr src, IntPtr c, int k) => cudaMemcpyD2H(dst, src, c, k);
    IntPtr ICuFftBindings.cudaGetDeviceName()
    {
        try
        {
            var prop = new byte[1024];
            int rc = cudaGetDeviceProperties(prop, 0);
            if (rc != 0) return IntPtr.Zero;
            int len = Array.IndexOf(prop, (byte)0);
            if (len <= 0) len = 256;
            return Marshal.StringToHGlobalAnsi(
                System.Text.Encoding.ASCII.GetString(prop, 0, Math.Min(len, 256)).Trim());
        }
        catch { return IntPtr.Zero; }
    }
}

#endregion

#region CUDA 10.1

file sealed class CuFft10_101 : ICuFftBindings
{
    private const string Fft = "cufft64_10";
    private const string Rt  = "cudart64_101";

    [DllImport(Fft)] private static extern int cufftPlan2d(out IntPtr plan, int nx, int ny, int type);
    [DllImport(Fft)] private static extern int cufftExecZ2Z(IntPtr plan, IntPtr idata, IntPtr odata, int direction);
    [DllImport(Fft)] private static extern int cufftDestroy(IntPtr plan);

    [DllImport(Rt)] private static extern int cudaMalloc(out IntPtr devPtr, IntPtr size);
    [DllImport(Rt)] private static extern int cudaFree(IntPtr devPtr);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyH2D(IntPtr dst, double[] src, IntPtr count, int kind);
    [DllImport(Rt, EntryPoint = "cudaMemcpy")]
    private static extern int cudaMemcpyD2H(double[] dst, IntPtr src, IntPtr count, int kind);

    [DllImport(Rt, EntryPoint = "cudaGetDeviceProperties")]
    private static extern int cudaGetDeviceProperties(byte[] prop, int device);

    int ICuFftBindings.cufftPlan2d(out IntPtr p, int nx, int ny, int t) => cufftPlan2d(out p, nx, ny, t);
    int ICuFftBindings.cufftExecZ2Z(IntPtr p, IntPtr i, IntPtr o, int d) => cufftExecZ2Z(p, i, o, d);
    int ICuFftBindings.cufftDestroy(IntPtr p) => cufftDestroy(p);
    int ICuFftBindings.cudaMalloc(out IntPtr p, IntPtr s) => cudaMalloc(out p, s);
    int ICuFftBindings.cudaFree(IntPtr p) => cudaFree(p);
    int ICuFftBindings.cudaMemcpy(IntPtr dst, double[] src, IntPtr c, int k) => cudaMemcpyH2D(dst, src, c, k);
    int ICuFftBindings.cudaMemcpy(double[] dst, IntPtr src, IntPtr c, int k) => cudaMemcpyD2H(dst, src, c, k);
    IntPtr ICuFftBindings.cudaGetDeviceName()
    {
        try
        {
            var prop = new byte[1024];
            int rc = cudaGetDeviceProperties(prop, 0);
            if (rc != 0) return IntPtr.Zero;
            int len = Array.IndexOf(prop, (byte)0);
            if (len <= 0) len = 256;
            return Marshal.StringToHGlobalAnsi(
                System.Text.Encoding.ASCII.GetString(prop, 0, Math.Min(len, 256)).Trim());
        }
        catch { return IntPtr.Zero; }
    }
}

#endregion

#endregion
