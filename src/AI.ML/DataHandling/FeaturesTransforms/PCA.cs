using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;

namespace AI.ML.DataHandling.FeaturesTransforms;

/// <summary>
/// Метод главных компонент
/// </summary>
[Serializable]
public class PCA
{
    /// <summary>
    /// Число итераций
    /// </summary>
    public int Iterations { get; set; } = 50;

    /// <summary>
    /// Информация об экземпляре
    /// </summary>
    public PCAInfo Info { get; protected set; }

    /// <summary>
    /// Собственные числа ков матрицы
    /// </summary>
    public Vector Eigenvalues { get; protected set; }


    /// <summary>
    /// Значение сходимости (если разница в RQ алгоритме выше, срабатывает исключение)
    /// </summary>
    public double Eps { get; set; } = 0.5;

    /// <summary>
    /// Матрица преобразования: столбцы — главные компоненты в исходных координатах
    /// </summary>
    public Matrix Components => _pca;

    // Число компонент
    private readonly int? _k;
    // Матрица преобразования
    private Matrix _pca;

    /// <summary>
    /// Корень из собственных чисел ков матрицы
    /// </summary>
    private Vector _sqrtEigenvalues;



    /// <summary>
    /// Метод главных компонент
    /// </summary>
    /// <param name="k">Число компонент null - все</param>
    public PCA(int? k = null)
    {
        _k = k;
    }

    /// <summary>
    /// Обучение PCA
    /// </summary>
    /// <param name="matrix">Матрица данных</param>
    public PCAInfo Train(Matrix matrix)
    {

        Matrix var_matrix = Matrix.GetCovMatrixFromColumns(matrix); // Получение кор. матрицы

        // Ковариационная матрица симметрична, поэтому используется симметричный решатель
        // ядра (метод вращений). Прежний путь - неявная QR-итерация с восстановлением
        // векторов через систему с регуляризацией - давал неортогональные компоненты:
        // скалярное произведение соседних доходило до 3e-4 вместо нуля.
        (Vector spectrum, Matrix components) =
            Eigen.Symmetric(var_matrix, EigenOrder.Descending, Iterations * 4);

        Info = new PCAInfo();

        // Определение числа компонент
        int k = _k == null ? var_matrix.Height : _k.Value;
        k = k > var_matrix.Height ? var_matrix.Height : k;

        var eigenvalues = spectrum;

        // Оценка качества
        if (k == var_matrix.Height)
        {
            Info.SaveVar = eigenvalues.Sum();
            Info.LastVar = 0;
        }
        else
        {
            Info.SaveVar = 0;
            Info.LastVar = 0;
            int i = 0;
            for (; i < k; i++) Info.SaveVar += eigenvalues[i]; // Объясненная дисперсия
            for (; i < eigenvalues.Count; i++) Info.LastVar += eigenvalues[i]; // Остаточная дисперсия
        }

        // Мера сходимости: наибольшая невязка ||A·v - λ·v|| по всем найденным парам
        Info.EpsEigenvalues = Residual(var_matrix, spectrum, components);
        Info.IsConvergence = Info.EpsEigenvalues <= Eps;

        Eigenvalues = eigenvalues.CutAndZero(k); // Топ k собственных чисел
        _sqrtEigenvalues = Eigenvalues.Transform(Math.Sqrt); // Корнм из собственных чисел (для нормализации)

        _pca = new Matrix(var_matrix.Height, k); // Матрица преобразования: столбцы - компоненты

        for (int component = 0; component < k; component++)
            for (int row = 0; row < var_matrix.Height; row++)
                _pca[row, component] = components[row, component];

        return Info;
    }


    // Наибольшая невязка собственной пары: max_k ||A·v_k - λ_k·v_k||_inf
    private static double Residual(Matrix a, Vector values, Matrix vectors)
    {
        int n = a.Height;
        double worst = 0;

        for (int k = 0; k < n; k++)
        {
            for (int i = 0; i < n; i++)
            {
                double sum = 0;

                for (int j = 0; j < n; j++)
                    sum += a[i, j] * vectors[j, k];

                worst = Math.Max(worst, Math.Abs(sum - (values[k] * vectors[i, k])));
            }
        }

        return worst;
    }

    /// <summary>
    /// Обучение PCA
    /// </summary>
    /// <param name="vectors">Матрица данных</param>
    public PCAInfo Train(Vector[] vectors)
    {
        Matrix matrix = Matrix.FromVectorsAsRows(vectors);
        return Train(matrix);
    }

    /// <summary>
    /// Прямое преобразование
    /// </summary>
    /// <param name="data">Данные</param>
    /// <param name="isNormal">Нормализовывать ли</param>
    public Vector[] Transform(IEnumerable<Vector> data, bool isNormal = false)
    {
        Matrix dMatr = Matrix.FromVectorsAsRows(data);
        return Matrix.GetRows(Transform(dMatr, isNormal));
    }

    /// <summary>
    /// Прямое преобразование одного вектора
    /// </summary>
    /// <param name="vector">Данные</param>
    /// <param name="isNormal">Нормализовывать ли</param>
    public Vector Transform(Vector vector, bool isNormal = false)
    {
        return Transform(new[] { vector }, isNormal)[0];
    }

    /// <summary>
    /// Прямое преобразование
    /// </summary>
    /// <param name="data">Данные</param>
    /// <param name="isNormal">Нормализовывать ли</param>
    public Matrix Transform(Matrix data, bool isNormal = false)
    {

        if (_pca == null)
            throw new Exception("Обучите алгоритм PCA!");

        Matrix res = data * _pca;

        // Нужна ли нормализация 
        if (isNormal)
        {
            for (int i = 0; i < res.Width; i++)
                for (int j = 0; j < res.Height; j++)
                    res[j, i] /= _sqrtEigenvalues[i];
        }

        return res;
    }
}

/// <summary>
/// Информация о преобразовании
/// </summary>
[Serializable]
public class PCAInfo
{
    /// <summary>
    /// Доля сохраненной энергии
    /// </summary>
    public double InfoSaveEnergy => SaveVar / (SaveVar + LastVar);

    /// <summary>
    /// Остаточная дисперсия
    /// </summary>
    public double LastVar { get; set; }

    /// <summary>
    /// Сохраненная дисперсия
    /// </summary>
    public double SaveVar { get; set; }

    /// <summary>
    /// Ошибка при вычисление собственных чисел
    /// </summary>
    public double EpsEigenvalues { get; set; }

    /// <summary>
    /// Сошелся ли алгоритм
    /// </summary>
    public bool IsConvergence { get; set; }

}
