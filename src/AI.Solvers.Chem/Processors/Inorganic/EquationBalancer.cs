using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using AI.Solvers.Chem.Models;
using AI.Solvers.Chem.Parsing;
using System.Globalization;

namespace AI.Solvers.Chem.Processors.Inorganic;

/// <summary>
/// Балансировка химических уравнений через ядро стехиометрической матрицы.
/// Матрица содержит строку на каждый элемент и, для ионных уравнений, строку заряда.
/// </summary>
public class EquationBalancer
{
    private const double SingularTolerance = 1e-8;
    private const double IntegerTolerance = 1e-6;
    private const int MaxMultiplier = 500;

    private readonly ChemDatabase _database;
    private readonly VerbosityLevel _verbosity;

    /// <summary>
    /// Создаёт балансировщик уравнений
    /// </summary>
    public EquationBalancer(ChemDatabase database, VerbosityLevel verbosity)
    {
        _database = database;
        _verbosity = verbosity;
    }

    /// <summary>
    /// Балансирует уравнение реакции
    /// </summary>
    public ChemResult Balance(ParsedCommand cmd)
    {
        try
        {
            if (!TryBalance(cmd.GetString("reactants"), cmd.GetString("products"), out var reaction, out string error))
                return ChemResult.Error(error);

            var result = ChemResult.Ok(reaction.Equation);
            result.Data["coefficients"] = reaction.Coefficients;
            result.Data["elements"] = reaction.Elements;
            result.Data["reaction"] = reaction;

            if (reaction.Nullity > 1)
                result.Steps.Add($"Warning: the system is underdetermined ({reaction.Nullity} independent solutions), one of them is shown");

            if (_verbosity >= VerbosityLevel.Detailed)
            {
                result.Steps.Add($"1. Species: {reaction.Reactants.Count} reactant(s), {reaction.Products.Count} product(s)");
                result.Steps.Add($"2. Elements involved: {string.Join(", ", reaction.Elements)}"
                    + (reaction.HasCharge ? " (+ charge balance)" : ""));
                result.Steps.Add("3. Solved as the null space of the stoichiometric matrix (SVD)");
                result.Steps.Add($"4. Integer coefficients: {string.Join(", ", reaction.Coefficients)}");
                result.Steps.Add("5. Atom (and charge) conservation verified");
            }

            return result;
        }
        catch (Exception ex)
        {
            return ChemResult.Error($"Balancing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Балансирует уравнение и возвращает разобранную реакцию
    /// </summary>
    /// <param name="reactants">Левая часть уравнения</param>
    /// <param name="products">Правая часть уравнения</param>
    /// <param name="reaction">Сбалансированная реакция</param>
    /// <param name="error">Причина отказа, если сбалансировать не удалось</param>
    public bool TryBalance(string reactants, string products, out BalancedReaction reaction, out string error)
    {
        reaction = null;

        var left = MolecularFormula.ParseSide(reactants);
        var right = MolecularFormula.ParseSide(products);

        var elements = GetAllElements(left, right);
        var unknown = elements.Where(e => _database.GetElement(e) == null).ToList();

        if (unknown.Count > 0)
        {
            error = $"Unknown element(s) in the equation: {string.Join(", ", unknown)}";
            return false;
        }

        bool hasCharge = left.Concat(right).Any(f => f.Charge != 0);
        var matrix = BuildMatrix(left, right, elements, hasCharge);
        var solution = FindNullSpaceVector(matrix, out int nullity);

        if (solution == null)
        {
            error = nullity > 1
                ? $"Equation is underdetermined: the species admit {nullity} independent balances, remove or add species"
                : "Equation cannot be balanced: no solution with positive coefficients";
            return false;
        }

        if (!TryRationalize(solution, out int[] coefficients))
        {
            error = "Equation cannot be balanced with reasonable integer coefficients";
            return false;
        }

        string mismatch = Verify(left, right, coefficients, elements, hasCharge);

        if (mismatch != null)
        {
            error = $"Equation cannot be balanced: {mismatch}";
            return false;
        }

        error = null;
        reaction = new BalancedReaction(left, right, coefficients, elements, hasCharge, nullity,
            FormatBalancedEquation(left, right, coefficients));

        return true;
    }

    /// <summary>
    /// Балансирует уравнение, записанное целиком ("Fe + O2 = Fe2O3")
    /// </summary>
    /// <param name="equation">Уравнение реакции</param>
    /// <param name="reaction">Сбалансированная реакция</param>
    /// <param name="error">Причина отказа</param>
    public bool TryBalance(string equation, out BalancedReaction reaction, out string error)
    {
        reaction = null;

        if (!MolecularFormula.TrySplitEquation(equation, out string reactants, out string products))
        {
            error = "Reaction equation must contain an arrow: 'A + B = C'";
            return false;
        }

        return TryBalance(reactants, products, out reaction, out error);
    }

    private static List<string> GetAllElements(List<MolecularFormula> reactants, List<MolecularFormula> products)
    {
        var elements = new HashSet<string>(StringComparer.Ordinal);

        foreach (var formula in reactants.Concat(products))
        {
            foreach (string element in formula.Elements.Keys)
                elements.Add(element);
        }

        return elements.OrderBy(e => e, StringComparer.Ordinal).ToList();
    }

    // Реагенты дают положительные столбцы, продукты - отрицательные
    private static Matrix BuildMatrix(List<MolecularFormula> reactants,
                                      List<MolecularFormula> products,
                                      List<string> elements,
                                      bool hasCharge)
    {
        int rows = elements.Count + (hasCharge ? 1 : 0);
        int columns = reactants.Count + products.Count;
        var matrix = new Matrix(rows, columns);

        for (int i = 0; i < elements.Count; i++)
        {
            for (int j = 0; j < reactants.Count; j++)
                matrix[i, j] = reactants[j].GetCount(elements[i]);

            for (int j = 0; j < products.Count; j++)
                matrix[i, reactants.Count + j] = -products[j].GetCount(elements[i]);
        }

        if (hasCharge)
        {
            int row = elements.Count;

            for (int j = 0; j < reactants.Count; j++)
                matrix[row, j] = reactants[j].Charge;

            for (int j = 0; j < products.Count; j++)
                matrix[row, reactants.Count + j] = -products[j].Charge;
        }

        return matrix;
    }

    /// <summary>
    /// Вектор ядра матрицы: столбец V, отвечающий наименьшему сингулярному числу.
    /// Возвращает null, если решение содержит компоненты разных знаков
    /// (уравнение не сводится к положительным коэффициентам).
    /// </summary>
    private static double[] FindNullSpaceVector(Matrix matrix, out int nullity)
    {
        var (_, sigma, v) = Svd.Decompose(matrix);

        double maxSigma = sigma.Length == 0 ? 0 : sigma.Max();
        double threshold = Math.Max(maxSigma * SingularTolerance, 1e-12);

        nullity = sigma.Count(s => s <= threshold);

        int minIndex = 0;
        for (int i = 1; i < sigma.Length; i++)
        {
            if (sigma[i] < sigma[minIndex])
                minIndex = i;
        }

        int n = matrix.Width;
        var solution = new double[n];

        for (int i = 0; i < n; i++)
            solution[i] = v[i, minIndex];

        double scale = solution.Select(Math.Abs).DefaultIfEmpty(0).Max();

        if (scale < 1e-12)
            return null;

        // Знак выбирается так, чтобы наибольшая по модулю компонента была положительной
        int pivot = Array.FindIndex(solution, x => Math.Abs(x) >= scale - 1e-12);
        if (solution[pivot] < 0)
        {
            for (int i = 0; i < n; i++)
                solution[i] = -solution[i];
        }

        // Все коэффициенты обязаны быть положительными
        if (solution.Any(x => x < -IntegerTolerance * scale))
            return null;

        return solution;
    }

    /// <summary>
    /// Приводит вещественное решение к наименьшим натуральным коэффициентам
    /// </summary>
    private static bool TryRationalize(double[] solution, out int[] coefficients)
    {
        coefficients = null;

        double minValue = solution.Where(x => x > IntegerTolerance).DefaultIfEmpty(0).Min();

        if (minValue <= 0)
            return false;

        var normalized = solution.Select(x => x / minValue).ToArray();

        for (int multiplier = 1; multiplier <= MaxMultiplier; multiplier++)
        {
            var scaled = new long[normalized.Length];
            bool allIntegers = true;

            for (int i = 0; i < normalized.Length; i++)
            {
                double value = normalized[i] * multiplier;
                double rounded = Math.Round(value);

                if (Math.Abs(value - rounded) > IntegerTolerance * Math.Max(1.0, Math.Abs(value)) || rounded < 1)
                {
                    allIntegers = false;
                    break;
                }

                scaled[i] = (long)rounded;
            }

            if (!allIntegers)
                continue;

            long gcd = scaled[0];
            foreach (long value in scaled)
                gcd = Gcd(gcd, value);

            coefficients = scaled.Select(x => (int)(x / gcd)).ToArray();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Проверка сохранения атомов и заряда. Возвращает описание расхождения или null
    /// </summary>
    private static string Verify(List<MolecularFormula> reactants,
                                 List<MolecularFormula> products,
                                 int[] coefficients,
                                 List<string> elements,
                                 bool hasCharge)
    {
        foreach (string element in elements)
        {
            long left = 0, right = 0;

            for (int i = 0; i < reactants.Count; i++)
                left += (long)coefficients[i] * reactants[i].GetCount(element);

            for (int i = 0; i < products.Count; i++)
                right += (long)coefficients[reactants.Count + i] * products[i].GetCount(element);

            if (left != right)
                return $"{element} is not conserved ({left} vs {right})";
        }

        if (hasCharge)
        {
            long left = 0, right = 0;

            for (int i = 0; i < reactants.Count; i++)
                left += (long)coefficients[i] * reactants[i].Charge;

            for (int i = 0; i < products.Count; i++)
                right += (long)coefficients[reactants.Count + i] * products[i].Charge;

            if (left != right)
                return $"charge is not conserved ({left:+#;-#;0} vs {right:+#;-#;0})";
        }

        return null;
    }

    private static long Gcd(long a, long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);

        while (b != 0)
        {
            long temp = b;
            b = a % b;
            a = temp;
        }

        return a == 0 ? 1 : a;
    }

    private static string FormatBalancedEquation(List<MolecularFormula> reactants,
                                                 List<MolecularFormula> products,
                                                 int[] coefficients)
    {
        string Term(MolecularFormula formula, int coefficient) => coefficient > 1
            ? coefficient.ToString(CultureInfo.InvariantCulture) + " " + formula.CoreFormula + ChargeSuffix(formula)
            : formula.CoreFormula + ChargeSuffix(formula);

        var left = reactants.Select((f, i) => Term(f, coefficients[i]));
        var right = products.Select((f, i) => Term(f, coefficients[reactants.Count + i]));

        return $"{string.Join(" + ", left)} = {string.Join(" + ", right)}";
    }

    private static string ChargeSuffix(MolecularFormula formula)
    {
        if (formula.Charge == 0)
            return string.Empty;

        int magnitude = Math.Abs(formula.Charge);
        string digits = magnitude == 1 ? string.Empty : magnitude.ToString(CultureInfo.InvariantCulture);

        return digits + (formula.Charge > 0 ? "+" : "-");
    }
}
