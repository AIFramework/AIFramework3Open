using AI.DataStructs.Algebraic;
using System;
using System.Threading;

namespace AI.ComputerVision.ImgTransforms;

/// <summary>
/// Данные преобразования Собеля.
/// GradImg / PhGrad теперь вычисляются однократно и кэшируются
/// в одном fused-проходе по <see cref="Matrix.Data"/> (меньше аллокаций и обходов памяти).
/// </summary>
[Serializable]
public class SobelData
{
    private Matrix _gradX;
    private Matrix _gradY;

    [NonSerialized] private Matrix _gradMag;
    [NonSerialized] private Matrix _gradPhase;
    [NonSerialized] private int _derived; // 0 — не вычислен, 1 — вычислен

    /// <summary>
    /// Градиент вдоль оси X
    /// </summary>
    public Matrix GradX
    {
        get => _gradX;
        set { _gradX = value; Invalidate(); }
    }

    /// <summary>
    /// Градиент вдоль оси Y
    /// </summary>
    public Matrix GradY
    {
        get => _gradY;
        set { _gradY = value; Invalidate(); }
    }

    /// <summary>
    /// Модуль градиента √(Gx² + Gy²)
    /// </summary>
    public Matrix GradImg
    {
        get
        {
            EnsureDerived();
            return _gradMag;
        }
    }

    /// <summary>
    /// Фаза градиента (сохранена прежняя семантика Acos(Gx / (|G| + eps))).
    /// </summary>
    public Matrix PhGrad
    {
        get
        {
            EnsureDerived();
            return _gradPhase;
        }
    }

    /// <summary>
    /// Создание изображения с данными преобразования Собеля
    /// </summary>
    public SobelData(Matrix gradX, Matrix gradY)
    {
        if (gradX is null) throw new ArgumentNullException(nameof(gradX));
        if (gradY is null) throw new ArgumentNullException(nameof(gradY));
        if (gradX.Width != gradY.Width || gradX.Height != gradY.Height)
            throw new ArgumentException("Размеры GradX и GradY не совпадают.");
        _gradX = gradX;
        _gradY = gradY;
    }

    private void Invalidate()
    {
        _gradMag = null;
        _gradPhase = null;
        Volatile.Write(ref _derived, 0);
    }

    private void EnsureDerived()
    {
        if (Volatile.Read(ref _derived) == 1) return;

        int H = _gradX.Height;
        int W = _gradX.Width;

        var mag = new Matrix(H, W);
        var ph = new Matrix(H, W);

        double[] gx = _gradX.Data;
        double[] gy = _gradY.Data;
        double[] md = mag.Data;
        double[] pd = ph.Data;
        double eps = AISettings.GlobalEps;

        int n = gx.Length;
        for (int i = 0; i < n; i++)
        {
            double x = gx[i];
            double y = gy[i];
            double m = Math.Sqrt(x * x + y * y);
            md[i] = m;
            // Сохраняем прежнюю семантику (HOG и FeaturesInBinaryImg рассчитаны на этот диапазон).
            double cos = x / (m + eps);
            if (cos > 1.0) cos = 1.0;
            else if (cos < -1.0) cos = -1.0;
            pd[i] = Math.Acos(cos);
        }

        _gradMag = mag;
        _gradPhase = ph;
        Volatile.Write(ref _derived, 1);
    }
}
