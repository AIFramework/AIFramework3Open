using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class AdvancedSolver
{
    #region Преобразование Лапласа

    public static string LaplaceTransform(string expression, string variable = "t")
    {
        try
        {
            var expr = AdvancedMathExpression.Parse(expression).Simplify();
            var result = LaplaceTable.Find(expr, variable);

            if (result.Contains("не найдено"))
            {
                return result + "\n\n" +
                       "Подсказка: используйте 'table' для просмотра полной таблицы\n" +
                       "Пример: Laplace table";
            }

            return result;
        }
        catch (Exception ex)
        {
            return $"L{{{expression}}} - ошибка: {ex.Message}";
        }
    }

    public static string ShowLaplaceTable() => LaplaceTable.GetFullTable();

    #endregion
}
