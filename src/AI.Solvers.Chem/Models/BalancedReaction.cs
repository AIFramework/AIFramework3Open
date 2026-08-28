using AI.Solvers.Chem.Database;

namespace AI.Solvers.Chem.Models;

/// <summary>
/// Сбалансированная реакция: вещества, целочисленные коэффициенты и проверенные балансы
/// </summary>
public sealed class BalancedReaction
{
    /// <summary>Реагенты</summary>
    public IReadOnlyList<MolecularFormula> Reactants { get; }

    /// <summary>Продукты</summary>
    public IReadOnlyList<MolecularFormula> Products { get; }

    /// <summary>
    /// Коэффициенты: сначала реагенты, затем продукты, в порядке их следования
    /// </summary>
    public IReadOnlyList<int> Coefficients { get; }

    /// <summary>Элементы, участвующие в реакции</summary>
    public IReadOnlyList<string> Elements { get; }

    /// <summary>Учитывался ли баланс заряда</summary>
    public bool HasCharge { get; }

    /// <summary>
    /// Размерность пространства решений: значение больше единицы означает,
    /// что набор веществ допускает несколько независимых балансов
    /// </summary>
    public int Nullity { get; }

    /// <summary>Уравнение с коэффициентами</summary>
    public string Equation { get; }

    /// <summary>Создаёт сбалансированную реакцию</summary>
    public BalancedReaction(
        IReadOnlyList<MolecularFormula> reactants,
        IReadOnlyList<MolecularFormula> products,
        IReadOnlyList<int> coefficients,
        IReadOnlyList<string> elements,
        bool hasCharge,
        int nullity,
        string equation)
    {
        Reactants = reactants;
        Products = products;
        Coefficients = coefficients;
        Elements = elements;
        HasCharge = hasCharge;
        Nullity = nullity;
        Equation = equation;
    }

    /// <summary>Коэффициент перед реагентом по его индексу</summary>
    public int ReactantCoefficient(int index) => Coefficients[index];

    /// <summary>Коэффициент перед продуктом по его индексу</summary>
    public int ProductCoefficient(int index) => Coefficients[Reactants.Count + index];

    /// <summary>
    /// Ищет вещество по формуле среди реагентов и продуктов
    /// </summary>
    /// <param name="formula">Формула вещества</param>
    /// <param name="species">Найденное вещество</param>
    /// <param name="coefficient">Его коэффициент</param>
    /// <param name="isProduct">Является ли продуктом</param>
    public bool TryFind(string formula, out MolecularFormula species, out int coefficient, out bool isProduct)
    {
        for (int i = 0; i < Reactants.Count; i++)
        {
            if (Matches(Reactants[i], formula))
            {
                species = Reactants[i];
                coefficient = ReactantCoefficient(i);
                isProduct = false;
                return true;
            }
        }

        for (int i = 0; i < Products.Count; i++)
        {
            if (Matches(Products[i], formula))
            {
                species = Products[i];
                coefficient = ProductCoefficient(i);
                isProduct = true;
                return true;
            }
        }

        species = null;
        coefficient = 0;
        isProduct = false;
        return false;
    }

    /// <summary>
    /// Атомная экономия: доля массы реагентов, перешедшая в целевой продукт, %
    /// </summary>
    /// <param name="product">Формула целевого продукта</param>
    /// <param name="database">База атомных масс</param>
    public double AtomEconomyPercent(string product, ChemDatabase database)
    {
        if (!TryFind(product, out var species, out int coefficient, out bool isProduct) || !isProduct)
            throw new ArgumentException($"'{product}' is not among the products of the reaction");

        double productMass = coefficient * species.CalculateMolarMass(database);
        double inputMass = 0;

        for (int i = 0; i < Reactants.Count; i++)
            inputMass += ReactantCoefficient(i) * Reactants[i].CalculateMolarMass(database);

        return inputMass > 0 ? 100.0 * productMass / inputMass : double.NaN;
    }

    /// <summary>Уравнение реакции</summary>
    public override string ToString() => Equation;

    private static bool Matches(MolecularFormula species, string formula)
        => string.Equals(species.CoreFormula, formula, StringComparison.OrdinalIgnoreCase)
        || string.Equals(species.Formula, formula, StringComparison.OrdinalIgnoreCase);
}
