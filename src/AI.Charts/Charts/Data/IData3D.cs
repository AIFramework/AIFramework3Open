using AI.DataStructs.Algebraic;
using System;

namespace AI.Charts.Data;

/// <summary>
/// Grid-based 3D data for surface and wireframe plots.
/// X and Y define the grid axes; Z is a matrix of values Z[row, col]
/// where rows correspond to xGrid indices and cols to yGrid indices.
/// </summary>
[Serializable]
internal sealed class SurfaceData3D
{
    public Vector XGrid { get; }
    public Vector YGrid { get; }
    public double[,] Z { get; }

    public int Rows => XGrid.Count;
    public int Cols => YGrid.Count;

    public double ZMin { get; }
    public double ZMax { get; }

    public SurfaceData3D(Vector xGrid, Vector yGrid, double[,] z)
    {
        if (z.GetLength(0) != xGrid.Count || z.GetLength(1) != yGrid.Count)
            throw new ArgumentException(
                $"Z matrix dimensions [{z.GetLength(0)},{z.GetLength(1)}] must match xGrid({xGrid.Count}) x yGrid({yGrid.Count}).");

        XGrid = xGrid.Clone();
        YGrid = yGrid.Clone();
        Z = (double[,])z.Clone();

        double mn = double.MaxValue, mx = double.MinValue;
        for (int i = 0; i < z.GetLength(0); i++)
        for (int j = 0; j < z.GetLength(1); j++)
        {
            double v = z[i, j];
            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
            if (v < mn) mn = v;
            if (v > mx) mx = v;
        }

        ZMin = mn == double.MaxValue ? 0 : mn;
        ZMax = mx == double.MinValue ? 1 : mx;
    }
}

/// <summary>
/// Point-cloud 3D data for scatter plots: three vectors of equal length.
/// </summary>
[Serializable]
internal sealed class PointCloudData3D
{
    public Vector X { get; }
    public Vector Y { get; }
    public Vector Z { get; }
    public int Count => X.Count;

    public double ZMin { get; }
    public double ZMax { get; }

    public PointCloudData3D(Vector x, Vector y, Vector z)
    {
        if (x.Count != y.Count || x.Count != z.Count)
            throw new ArgumentException("X, Y, Z vectors must have the same length.");

        X = x.Clone();
        Y = y.Clone();
        Z = z.Clone();

        double mn = double.MaxValue, mx = double.MinValue;
        for (int i = 0; i < z.Count; i++)
        {
            double v = z[i];
            if (double.IsNaN(v) || double.IsInfinity(v)) continue;
            if (v < mn) mn = v;
            if (v > mx) mx = v;
        }

        ZMin = mn == double.MaxValue ? 0 : mn;
        ZMax = mx == double.MinValue ? 1 : mx;
    }
}
