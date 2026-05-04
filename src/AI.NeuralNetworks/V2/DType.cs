using System;

namespace AI.ML.NeuralNetworks.V2;

/// <summary>
/// Тип элемента тензора. Аналог <c>torch.dtype</c>.
/// </summary>
/// <remarks>
/// Перечисление компактно (1 байт), эффективно как ключ в dispatch-таблицах.
/// Расширяется без breaking changes — добавьте новое значение и зарегистрируйте
/// kernel-ы в <see cref="Ops.OpRegistry"/>.
/// </remarks>
public enum DType : byte
{
    /// <summary>32-bit IEEE 754 single-precision.</summary>
    Float32 = 0,
    /// <summary>64-bit IEEE 754 double-precision.</summary>
    Float64 = 1,
    /// <summary>16-bit IEEE 754 half-precision (для AMP/инференса).</summary>
    Float16 = 2,
    /// <summary>16-bit Brain Float (Google bfloat16).</summary>
    BFloat16 = 3,
    /// <summary>8-bit signed integer.</summary>
    Int8 = 10,
    /// <summary>16-bit signed integer.</summary>
    Int16 = 11,
    /// <summary>32-bit signed integer.</summary>
    Int32 = 12,
    /// <summary>64-bit signed integer.</summary>
    Int64 = 13,
    /// <summary>8-bit unsigned integer.</summary>
    UInt8 = 20,
    /// <summary>Логический тип, упакованный в 1 байт.</summary>
    Bool = 30,
}

/// <summary>
/// Расширения для <see cref="DType"/>.
/// </summary>
public static class DTypes
{
    /// <summary>
    /// Размер элемента в байтах.
    /// </summary>
    public static int ElementSize(this DType dt) => dt switch
    {
        DType.Float64 or DType.Int64 => 8,
        DType.Float32 or DType.Int32 => 4,
        DType.Float16 or DType.BFloat16 or DType.Int16 => 2,
        DType.Int8 or DType.UInt8 or DType.Bool => 1,
        _ => throw new NotSupportedException($"Неизвестный DType: {dt}")
    };

    /// <summary>
    /// True для всех типов с плавающей точкой (включая half/bf16).
    /// </summary>
    public static bool IsFloating(this DType dt) =>
        dt is DType.Float32 or DType.Float64 or DType.Float16 or DType.BFloat16;

    /// <summary>
    /// True для целочисленных знаковых и беззнаковых типов (без bool).
    /// </summary>
    public static bool IsIntegral(this DType dt) =>
        dt is DType.Int8 or DType.Int16 or DType.Int32 or DType.Int64 or DType.UInt8;

    /// <summary>
    /// CLR-тип, соответствующий dtype (для рефлексии и generic-диспатча).
    /// </summary>
    public static Type ManagedType(this DType dt) => dt switch
    {
        DType.Float32 => typeof(float),
        DType.Float64 => typeof(double),
        DType.Float16 => typeof(Half),
        DType.BFloat16 => typeof(ushort),  // bf16 хранится как ushort, конверсия по битам
        DType.Int8 => typeof(sbyte),
        DType.Int16 => typeof(short),
        DType.Int32 => typeof(int),
        DType.Int64 => typeof(long),
        DType.UInt8 => typeof(byte),
        DType.Bool => typeof(byte),
        _ => throw new NotSupportedException($"Неизвестный DType: {dt}")
    };

    /// <summary>
    /// dtype, соответствующий CLR-типу <typeparamref name="T"/>.
    /// </summary>
    public static DType FromManaged<T>() where T : unmanaged
    {
        if (typeof(T) == typeof(float)) return DType.Float32;
        if (typeof(T) == typeof(double)) return DType.Float64;
        if (typeof(T) == typeof(Half)) return DType.Float16;
        if (typeof(T) == typeof(int)) return DType.Int32;
        if (typeof(T) == typeof(long)) return DType.Int64;
        if (typeof(T) == typeof(short)) return DType.Int16;
        if (typeof(T) == typeof(sbyte)) return DType.Int8;
        if (typeof(T) == typeof(byte)) return DType.UInt8;
        throw new NotSupportedException($"Тип {typeof(T)} не имеет соответствия в DType.");
    }

    /// <summary>
    /// Result-dtype при бинарной операции (numpy/PyTorch правила type promotion).
    /// </summary>
    /// <remarks>
    /// Упрощённая версия: float побеждает int; двойная точность побеждает одинарную;
    /// bf16+fp16 -> fp32 (промежуточный аккумулятор). Для смеси signed/unsigned
    /// результат поднимается на следующий уровень ширины signed-типа (как numpy).
    /// </remarks>
    public static DType Promote(DType a, DType b)
    {
        if (a == b) return a;
        if (a == DType.Float64 || b == DType.Float64) return DType.Float64;
        if (a.IsFloating() || b.IsFloating())
        {
            if ((a == DType.Float16 && b == DType.BFloat16) ||
                (a == DType.BFloat16 && b == DType.Float16))
                return DType.Float32;
            if (a == DType.Float32 || b == DType.Float32) return DType.Float32;
            if (a == DType.Float16 || b == DType.Float16) return DType.Float16;
            return DType.BFloat16;
        }
        if (a == DType.Bool) return b;
        if (b == DType.Bool) return a;
        return PromoteIntegers(a, b);
    }

    private static int IntWidthBits(DType dt) => dt switch
    {
        DType.Int8 or DType.UInt8 => 8,
        DType.Int16 => 16,
        DType.Int32 => 32,
        DType.Int64 => 64,
        _ => 0
    };

    private static bool IsSigned(DType dt) => dt is DType.Int8 or DType.Int16 or DType.Int32 or DType.Int64;

    private static DType PromoteIntegers(DType a, DType b)
    {
        bool aSigned = IsSigned(a);
        bool bSigned = IsSigned(b);
        int wa = IntWidthBits(a);
        int wb = IntWidthBits(b);
        if (aSigned == bSigned)
        {
            int w = Math.Max(wa, wb);
            return aSigned ? WidthToSigned(w) : DType.UInt8;
        }
        // Смешанные знаковые/беззнаковые: результат — signed с шириной max(signedWidth, unsignedWidth*2).
        int unsignedW = aSigned ? wb : wa;
        int signedW = aSigned ? wa : wb;
        int target = Math.Max(signedW, Math.Min(64, unsignedW * 2));
        return WidthToSigned(target);
    }

    private static DType WidthToSigned(int bits) => bits switch
    {
        <= 8 => DType.Int8,
        <= 16 => DType.Int16,
        <= 32 => DType.Int32,
        _ => DType.Int64
    };
}
