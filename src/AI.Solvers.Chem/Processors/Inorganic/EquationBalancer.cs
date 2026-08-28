// ═══════════════════════════════════════════════════════════
// NUGET PACKAGES REQUIRED:


using FractalAgentsAI.Solvers.Chem.Core;
using FractalAgentsAI.Solvers.Chem.Database;
using MathNet.Numerics.LinearAlgebra;

namespace FractalAgentsAI.Solvers.Chem.Processors.Inorganic;

// БАЛАНСИРОВКА УРАВНЕНИЙ
public class EquationBalancer
{
    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    public EquationBalancer(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    public ChemResult Balance(ParsedCommand cmd)
    {
        try
        {
            var reactants = ParseSide(cmd.Parameters["reactants"]);
            var products = ParseSide(cmd.Parameters["products"]);

            // Построение матрицы элементов
            var elements = GetAllElements(reactants, products);
            var matrix = BuildMatrix(reactants, products, elements);

            // Решение системы методом Гаусса
            var coefficients = SolveByGaussian(matrix);

            // Нормализация к целым числам
            coefficients = NormalizeCoefficients(coefficients);

            // Форматирование результата
            var result = FormatBalancedEquation(reactants, products, coefficients);

            var chemResult = ChemResult.Ok(result);

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                chemResult.Steps.Add("1. Parsed reactants and products");
                chemResult.Steps.Add($"2. Elements involved: {string.Join(", ", elements)}");
                chemResult.Steps.Add("3. Built stoichiometric matrix");
                chemResult.Steps.Add("4. Solved using Gaussian elimination");
                chemResult.Steps.Add($"5. Normalized coefficients: {string.Join(", ", coefficients.Select(c => c.ToString("F2")))}");
            }

            return chemResult;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Balancing failed: {ex.Message}");
        }
    }

    private List<MolecularFormula> ParseSide(string side)
    {
        var parts = side.Split('+').Select(p => p.Trim()).ToList();
        return parts.Select(p => new MolecularFormula(p)).ToList();
    }

    private List<string> GetAllElements(List<MolecularFormula> reactants, List<MolecularFormula> products)
    {
        var elements = new HashSet<string>();

        foreach (var formula in reactants.Concat(products))
        {
            foreach (var element in formula.Elements.Keys)
                elements.Add(element);
        }

        return elements.OrderBy(e => e).ToList();
    }

    private Matrix<double> BuildMatrix(List<MolecularFormula> reactants,
                                      List<MolecularFormula> products,
                                      List<string> elements)
    {
        int rows = elements.Count;
        int cols = reactants.Count + products.Count;

        var matrix = Matrix<double>.Build.Dense(rows, cols);

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];

            // Реагенты (положительные)
            for (int j = 0; j < reactants.Count; j++)
            {
                matrix[i, j] = reactants[j].Elements.ContainsKey(element)
                    ? reactants[j].Elements[element]
                    : 0;
            }

            // Продукты (отрицательные)
            for (int j = 0; j < products.Count; j++)
            {
                matrix[i, reactants.Count + j] = products[j].Elements.ContainsKey(element)
                    ? -products[j].Elements[element]
                    : 0;
            }
        }

        return matrix;
    }

    private double[] SolveByGaussian(Matrix<double> matrix)
    {
        // Используем более надёжный метод через SVD
        try
        {
            var svd = matrix.Svd(true);
            var nullSpace = svd.VT.Row(svd.VT.RowCount - 1);
            
            // Конвертируем в массив
            var solution = new double[nullSpace.Count];
            for (int i = 0; i < nullSpace.Count; i++)
            {
                solution[i] = Math.Abs(nullSpace[i]); // Берём абсолютные значения
            }
            
            return solution;
        }
        catch
        {
            // Fallback к ручному методу
            return SolveByManualMethod(matrix);
        }
    }
    
    private double[] SolveByManualMethod(Matrix<double> matrix)
    {
        int rows = matrix.RowCount;
        int cols = matrix.ColumnCount;
        
        // Клонируем матрицу для работы
        var m = matrix.Clone();
        
        // Прямой ход Гаусса
        for (int i = 0; i < Math.Min(rows, cols); i++)
        {
            // Находим максимальный элемент для pivot
            int maxRow = i;
            for (int k = i + 1; k < rows; k++)
            {
                if (Math.Abs(m[k, i]) > Math.Abs(m[maxRow, i]))
                    maxRow = k;
            }
            
            // Меняем строки
            if (maxRow != i)
            {
                for (int k = 0; k < cols; k++)
                {
                    var temp = m[i, k];
                    m[i, k] = m[maxRow, k];
                    m[maxRow, k] = temp;
                }
            }
            
            // Обнуляем элементы под pivot
            for (int k = i + 1; k < rows; k++)
            {
                if (Math.Abs(m[i, i]) < 1e-10) continue;
                
                double factor = m[k, i] / m[i, i];
                for (int j = i; j < cols; j++)
                {
                    m[k, j] -= factor * m[i, j];
                }
            }
        }
        
        // Находим свободную переменную (последний столбец)
        var solution = new double[cols];
        solution[cols - 1] = 1.0;
        
        // Обратная подстановка
        for (int i = Math.Min(rows, cols) - 2; i >= 0; i--)
        {
            double sum = 0;
            for (int j = i + 1; j < cols; j++)
            {
                sum += m[i, j] * solution[j];
            }
            
            if (Math.Abs(m[i, i]) > 1e-10)
            {
                solution[i] = -sum / m[i, i];
            }
        }
        
        // Делаем все положительными
        for (int i = 0; i < solution.Length; i++)
        {
            solution[i] = Math.Abs(solution[i]);
        }
        
        return solution;
    }


    private double[] NormalizeCoefficients(double[] coefficients)
    {
        // Делаем все положительными
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] = Math.Abs(coefficients[i]);
        }

        // Находим минимальный ненулевой коэффициент
        double minCoeff = coefficients.Where(c => c > 1e-10).Min();
        
        // Нормализуем относительно минимального
        for (int i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] /= minCoeff;
        }

        // Пробуем разные множители, чтобы найти наименьшие целые
        double[] result = null;
        for (int multiplier = 1; multiplier <= 100; multiplier++)
        {
            var temp = new double[coefficients.Length];
            bool allIntegers = true;
            
            for (int i = 0; i < coefficients.Length; i++)
            {
                temp[i] = coefficients[i] * multiplier;
                double rounded = Math.Round(temp[i]);
                
                if (Math.Abs(temp[i] - rounded) > 1e-6)
                {
                    allIntegers = false;
                    break;
                }
                
                temp[i] = rounded;
            }
            
            if (allIntegers)
            {
                // Нашли целые числа, проверяем НОД
                long gcd = (long)temp[0];
                for (int i = 1; i < temp.Length; i++)
                {
                    gcd = GCD(gcd, (long)temp[i]);
                }
                
                // Делим на НОД
                for (int i = 0; i < temp.Length; i++)
                {
                    temp[i] /= gcd;
                }
                
                result = temp;
                break;
            }
        }

        // Если не нашли, просто округляем
        if (result == null)
        {
            result = new double[coefficients.Length];
            for (int i = 0; i < coefficients.Length; i++)
            {
                result[i] = Math.Round(coefficients[i]);
            }
        }

        return result;
    }

    private long GCD(long a, long b)
    {
        while (b != 0)
        {
            long temp = b;
            b = a % b;
            a = temp;
        }
        return Math.Abs(a);
    }

    private string FormatBalancedEquation(List<MolecularFormula> reactants,
                                          List<MolecularFormula> products,
                                          double[] coefficients)
    {
        var left = new List<string>();
        var right = new List<string>();

        for (int i = 0; i < reactants.Count; i++)
        {
            int coeff = (int)coefficients[i];
            string term = coeff > 1 ? $"{coeff} {reactants[i].Formula}" : reactants[i].Formula;
            left.Add(term);
        }

        for (int i = 0; i < products.Count; i++)
        {
            int coeff = (int)coefficients[reactants.Count + i];
            string term = coeff > 1 ? $"{coeff} {products[i].Formula}" : products[i].Formula;
            right.Add(term);
        }

        return $"{string.Join(" + ", left)} = {string.Join(" + ", right)}";
    }
}
