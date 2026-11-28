using AI.DataStructs.Algebraic;

namespace AI.DSP.Multirate;

/// <summary>
/// Блок многоскоростной обработки: общий интерфейс фильтров, передискретизаторов и их цепочек.
/// </summary>
public interface IMultirateBlock
{
    /// <summary>Коэффициент изменения частоты: F_вых / F_вх.</summary>
    double RateFactor { get; }

    /// <summary>Групповая задержка, оставшаяся после компенсации, в периодах выходной частоты.</summary>
    double Delay { get; }

    /// <summary>Число операций на один выходной отсчёт.</summary>
    OperationCount OperationsPerOutput { get; }

    /// <summary>Вид арифметики.</summary>
    Arithmetic Arithmetic { get; }

    /// <summary>
    /// Потоковая обработка очередного фрагмента; состояние сохраняется между вызовами.
    /// </summary>
    /// <param name="input">Входные отсчёты.</param>
    /// <returns>Готовые выходные отсчёты (их число зависит от коэффициента и задержки).</returns>
    Vector Process(Vector input);

    /// <summary>
    /// Потоковая обработка одного отсчёта.
    /// </summary>
    Vector Process(double sample);

    /// <summary>
    /// Завершение потока: выдаёт отсчёты, ожидающие будущих входов (входы за концом считаются нулями).
    /// </summary>
    Vector Flush();

    /// <summary>Сброс состояния.</summary>
    void Reset();

    /// <summary>
    /// Обработка сигнала целиком: сброс, обработка и завершение потока.
    /// </summary>
    Vector ProcessBlock(Vector input);
}
