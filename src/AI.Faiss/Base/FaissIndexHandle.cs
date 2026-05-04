using System;

namespace AI.Faiss.Base;

internal sealed class FaissIndexHandle : IDisposable
{
    private IntPtr _handle;
    private bool _disposed;

    public bool IsInvalid => _handle == IntPtr.Zero;

    public FaissIndexHandle(IntPtr handle)
    {
        _handle = handle;
    }

    internal IntPtr Pointer
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsInvalid)
                throw new InvalidOperationException("Дескриптор FAISS-индекса невалиден.");
            return _handle;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (!IsInvalid)
        {
            FaissNative.FN_Release(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
