using AI.DataStructs.Algebraic;
using System;

namespace AI.ML.DataHandling.DataSets;

/// <summary>
/// Представляет структуру вектор-класс для классификации.
/// </summary>
[Serializable]
public class VectorDatasetItem
{
    /// <summary>
    /// Получает или задает вектор для классификации.
    /// </summary>
    public Vector Features { get; set; }

    /// <summary>
    /// Получает или задает метку класса.
    /// </summary>
    public int ClassMark { get; set; }

    /// <summary>
    /// Получает или задает радиус (схожесть).
    /// </summary>
    public double R { get; set; }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="VectorDatasetItem"/> с указанными вектором и меткой класса.
    /// </summary>
    /// <param name="vector">Вектор для классификации.</param>
    /// <param name="mark">Метка класса.</param>
    public VectorDatasetItem(Vector vector, int mark)
    {
        Features = vector ?? throw new ArgumentNullException(nameof(vector), "Вектор не может быть null.");
        ClassMark = mark;
    }

    /// <summary>
    /// Инициализирует новый экземпляр класса <see cref="VectorDatasetItem"/>.
    /// </summary>
    public VectorDatasetItem() { }
}
