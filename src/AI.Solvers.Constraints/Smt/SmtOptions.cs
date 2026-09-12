using AI.Solvers.Constraints.Sat;
using AI.Solvers.Optimization;

namespace AI.Solvers.Constraints.Smt;

/// <summary>Настройки решателя SMT</summary>
public sealed class SmtOptions
{
    /// <summary>
    /// Предел числа лемм теории — арифметических противоречий, запрещённых булевой части
    /// </summary>
    public int MaxLemmas { get; set; } = 10_000;

    /// <summary>Настройки булева решателя</summary>
    public SatOptions Sat { get; set; } = new();

    /// <summary>Настройки проверки арифметики: пределы итераций симплекса и узлов ветвления</summary>
    public LpOptions Lp { get; set; } = new();

    /// <summary>
    /// Наименьший запас, при котором строгое неравенство считается выполненным
    /// </summary>
    public double StrictMargin { get; set; } = 1e-9;

    /// <summary>Допуск при проверке модели прямым вычислением формулы</summary>
    public double Tolerance { get; set; } = 1e-7;
}
