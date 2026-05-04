using AI.DataStructs.Algebraic;

namespace AI.DataPrepaire.DataLoader.NNWBlockLoader;

/// <summary>
/// Интерфейс блока отображения вектора в вектор. Используется как лёгкий
/// post-processor (например, нормализация / линейная проекция выхода BERT)
/// без зависимости от какого-либо конкретного NN-ядра.
/// </summary>
public interface INNWBlockV2V
{
    /// <summary>
    /// Прямой проход: <paramref name="input"/> -> выход.
    /// </summary>
    Vector Forward(Vector input);
}
