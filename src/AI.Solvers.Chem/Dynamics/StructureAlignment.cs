using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;
using AI.Solvers.Chem.Structures;

namespace AI.Solvers.Chem.Dynamics;

/// <summary>
/// Результат совмещения структур
/// </summary>
/// <param name="Aligned">Совмещённая структура</param>
/// <param name="Rmsd">Среднеквадратичное отклонение после совмещения, ангстремы</param>
/// <param name="Rotation">Матрица поворота</param>
public readonly record struct AlignmentResult(MolecularStructure Aligned, double Rmsd, Matrix Rotation);

/// <summary>
/// Совмещение структур по Кабшу и связанные меры отклонения
/// </summary>
/// <remarks>
/// Поворот, наилучший в смысле наименьших квадратов, находится через сингулярное
/// разложение ковариационной матрицы. Отдельно исправляется случай det &lt; 0:
/// без этой поправки алгоритм может выдать отражение, которое формально уменьшает
/// отклонение, но превращает молекулу в её зеркальный образ.
/// </remarks>
public static class StructureAlignment
{
    /// <summary>
    /// Среднеквадратичное отклонение без совмещения
    /// </summary>
    /// <param name="first">Первая структура</param>
    /// <param name="second">Вторая структура</param>
    public static double Rmsd(MolecularStructure first, MolecularStructure second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Count != second.Count)
            throw new ArgumentException("Структуры должны содержать одинаковое число атомов");

        double sum = 0;

        for (int i = 0; i < first.Count; i++)
        {
            Vector3 delta = first.Atoms[i].Position - second.Atoms[i].Position;
            sum += delta.Dot(delta);
        }

        return Math.Sqrt(sum / first.Count);
    }

    /// <summary>
    /// Совмещает структуру с образцом поворотом и переносом
    /// </summary>
    /// <param name="mobile">Совмещаемая структура</param>
    /// <param name="reference">Образец</param>
    /// <param name="indices">Номера атомов, по которым идёт совмещение; null - по всем</param>
    public static AlignmentResult Align(
        MolecularStructure mobile,
        MolecularStructure reference,
        IReadOnlyList<int> indices = null)
    {
        ArgumentNullException.ThrowIfNull(mobile);
        ArgumentNullException.ThrowIfNull(reference);

        if (mobile.Count != reference.Count)
            throw new ArgumentException("Структуры должны содержать одинаковое число атомов");

        indices ??= Enumerable.Range(0, mobile.Count).ToArray();

        if (indices.Count < 3)
            throw new ArgumentException("Для совмещения нужно не менее трёх атомов", nameof(indices));

        Vector3 mobileCentre = Centre(mobile, indices);
        Vector3 referenceCentre = Centre(reference, indices);

        var covariance = new Matrix(3, 3);

        foreach (int i in indices)
        {
            Vector3 p = mobile.Atoms[i].Position - mobileCentre;
            Vector3 q = reference.Atoms[i].Position - referenceCentre;

            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                    covariance[row, column] += p[row] * q[column];
            }
        }

        var (u, _, v) = Svd.Decompose(covariance);

        // Знак определителя V·Uᵀ отличает поворот от отражения
        double sign = Determinant(Multiply(v, Transpose(u))) < 0 ? -1 : 1;
        var correction = new Matrix(3, 3);
        correction[0, 0] = 1;
        correction[1, 1] = 1;
        correction[2, 2] = sign;

        Matrix rotation = Multiply(Multiply(v, correction), Transpose(u));

        var aligned = new MolecularStructure { Cell = mobile.Cell, Name = mobile.Name };

        for (int i = 0; i < mobile.Count; i++)
        {
            Vector3 centred = mobile.Atoms[i].Position - mobileCentre;
            aligned.Add(mobile.Atoms[i].WithPosition(Apply(rotation, centred) + referenceCentre));
        }

        double sum = 0;

        foreach (int i in indices)
        {
            Vector3 delta = aligned.Atoms[i].Position - reference.Atoms[i].Position;
            sum += delta.Dot(delta);
        }

        return new AlignmentResult(aligned, Math.Sqrt(sum / indices.Count), rotation);
    }

    /// <summary>
    /// Среднеквадратичное отклонение каждого кадра траектории от образца
    /// </summary>
    /// <param name="trajectory">Траектория</param>
    /// <param name="reference">Образец; null - первый кадр</param>
    /// <param name="indices">Номера атомов для совмещения</param>
    public static double[] RmsdOverTime(
        Trajectory trajectory,
        MolecularStructure reference = null,
        IReadOnlyList<int> indices = null)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        reference ??= trajectory.Frames[0];
        var result = new double[trajectory.Count];

        for (int frame = 0; frame < trajectory.Count; frame++)
            result[frame] = Align(trajectory.Frames[frame], reference, indices).Rmsd;

        return result;
    }

    /// <summary>
    /// Среднеквадратичная флуктуация атомов по траектории, ангстремы
    /// </summary>
    /// <param name="trajectory">Траектория</param>
    /// <param name="align">Совмещать ли кадры перед расчётом</param>
    public static double[] Fluctuations(Trajectory trajectory, bool align = true)
    {
        ArgumentNullException.ThrowIfNull(trajectory);

        if (trajectory.Count < 2)
            throw new ArgumentException("Нужно не менее двух кадров", nameof(trajectory));

        MolecularStructure reference = trajectory.Frames[0];
        var frames = new List<MolecularStructure>(trajectory.Count);

        foreach (MolecularStructure frame in trajectory.Frames)
            frames.Add(align ? Align(frame, reference).Aligned : frame);

        int atoms = trajectory.AtomCount;
        var average = new Vector3[atoms];

        foreach (MolecularStructure frame in frames)
        {
            for (int atom = 0; atom < atoms; atom++)
                average[atom] += frame.Atoms[atom].Position;
        }

        for (int atom = 0; atom < atoms; atom++)
            average[atom] /= frames.Count;

        var fluctuations = new double[atoms];

        foreach (MolecularStructure frame in frames)
        {
            for (int atom = 0; atom < atoms; atom++)
            {
                Vector3 delta = frame.Atoms[atom].Position - average[atom];
                fluctuations[atom] += delta.Dot(delta);
            }
        }

        for (int atom = 0; atom < atoms; atom++)
            fluctuations[atom] = Math.Sqrt(fluctuations[atom] / frames.Count);

        return fluctuations;
    }

    private static Vector3 Centre(MolecularStructure structure, IReadOnlyList<int> indices)
    {
        var sum = new Vector3();

        foreach (int i in indices)
            sum += structure.Atoms[i].Position;

        return sum / indices.Count;
    }

    private static Vector3 Apply(Matrix rotation, Vector3 point)
        => new(
            (rotation[0, 0] * point.X) + (rotation[0, 1] * point.Y) + (rotation[0, 2] * point.Z),
            (rotation[1, 0] * point.X) + (rotation[1, 1] * point.Y) + (rotation[1, 2] * point.Z),
            (rotation[2, 0] * point.X) + (rotation[2, 1] * point.Y) + (rotation[2, 2] * point.Z));

    private static Matrix Multiply(Matrix left, Matrix right)
    {
        var result = new Matrix(3, 3);

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                double sum = 0;

                for (int k = 0; k < 3; k++)
                    sum += left[row, k] * right[k, column];

                result[row, column] = sum;
            }
        }

        return result;
    }

    private static Matrix Transpose(Matrix matrix)
    {
        var result = new Matrix(3, 3);

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
                result[row, column] = matrix[column, row];
        }

        return result;
    }

    private static double Determinant(Matrix matrix)
        => (matrix[0, 0] * ((matrix[1, 1] * matrix[2, 2]) - (matrix[1, 2] * matrix[2, 1])))
            - (matrix[0, 1] * ((matrix[1, 0] * matrix[2, 2]) - (matrix[1, 2] * matrix[2, 0])))
            + (matrix[0, 2] * ((matrix[1, 0] * matrix[2, 1]) - (matrix[1, 1] * matrix[2, 0])));
}
