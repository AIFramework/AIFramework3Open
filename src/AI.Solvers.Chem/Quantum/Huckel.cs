using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using System.Globalization;
using System.Text;

namespace AI.Solvers.Chem.Quantum;

/// <summary>
/// Решение задачи Хюккеля для сопряжённой системы
/// </summary>
/// <remarks>
/// Энергия орбитали выражается как E = alpha + x·beta, где x - собственное значение
/// топологической матрицы. Резонансный интеграл beta отрицателен, поэтому наименьшей
/// энергии отвечает наибольшее x: орбитали упорядочены по убыванию x, то есть
/// по возрастанию энергии.
/// </remarks>
public sealed class HuckelSolution
{
    private readonly PiSystem _system;

    /// <summary>Коэффициенты x орбиталей: E = alpha + x·beta</summary>
    public IReadOnlyList<double> OrbitalCoefficients { get; }

    /// <summary>Коэффициенты атомных орбиталей: строка - центр, столбец - молекулярная орбиталь</summary>
    public Matrix Coefficients { get; }

    /// <summary>Заселённости орбиталей</summary>
    public IReadOnlyList<int> Occupations { get; }

    /// <summary>Электронная плотность на центрах</summary>
    public IReadOnlyList<double> ChargeDensities { get; }

    /// <summary>Заряды на центрах: вклад атома минус плотность</summary>
    public IReadOnlyList<double> Charges { get; }

    /// <summary>Свободные валентности центров</summary>
    public IReadOnlyList<double> FreeValences { get; }

    /// <summary>Номер верхней занятой орбитали; -1, если занятых нет</summary>
    public int HomoIndex { get; }

    /// <summary>Номер нижней свободной орбитали; -1, если свободных нет</summary>
    public int LumoIndex { get; }

    /// <summary>Полная энергия системы в единицах beta (слагаемое при beta)</summary>
    public double TotalEnergy { get; }

    /// <summary>Число электронов в системе</summary>
    public int Electrons => _system.Electrons;

    /// <summary>Число центров</summary>
    public int Count => _system.Count;

    internal HuckelSolution(
        PiSystem system,
        double[] orbitalCoefficients,
        Matrix coefficients,
        int[] occupations)
    {
        _system = system;
        OrbitalCoefficients = orbitalCoefficients;
        Coefficients = coefficients;
        Occupations = occupations;

        int count = system.Count;
        double total = 0;

        for (int mo = 0; mo < count; mo++)
            total += occupations[mo] * orbitalCoefficients[mo];

        TotalEnergy = total;

        var densities = new double[count];
        var charges = new double[count];

        for (int atom = 0; atom < count; atom++)
        {
            double density = 0;

            for (int mo = 0; mo < count; mo++)
                density += occupations[mo] * coefficients[atom, mo] * coefficients[atom, mo];

            densities[atom] = density;
            charges[atom] = system.Centers[atom].Electrons - density;
        }

        ChargeDensities = densities;
        Charges = charges;

        var valences = new double[count];

        for (int atom = 0; atom < count; atom++)
        {
            double bonds = 0;

            for (int other = 0; other < count; other++)
            {
                if (other != atom && system.HasBond(atom, other))
                    bonds += BondOrderOf(coefficients, occupations, count, atom, other);
            }

            // Наибольшая возможная сумма порядков связей для тригонального углерода
            valences[atom] = Math.Sqrt(3) - bonds;
        }

        FreeValences = valences;

        int homo = -1;

        for (int mo = 0; mo < count; mo++)
        {
            if (occupations[mo] > 0)
                homo = mo;
        }

        HomoIndex = homo;
        LumoIndex = homo + 1 < count ? homo + 1 : -1;
    }

    /// <summary>Коэффициент x верхней занятой орбитали</summary>
    public double Homo => HomoIndex >= 0 ? OrbitalCoefficients[HomoIndex] : double.NaN;

    /// <summary>Коэффициент x нижней свободной орбитали</summary>
    public double Lumo => LumoIndex >= 0 ? OrbitalCoefficients[LumoIndex] : double.NaN;

    /// <summary>Щель между граничными орбиталями в единицах beta</summary>
    public double Gap => HomoIndex >= 0 && LumoIndex >= 0 ? Homo - Lumo : double.NaN;

    /// <summary>
    /// Энергия делокализации в единицах beta: выигрыш против такого же числа
    /// изолированных двойных связей
    /// </summary>
    public double DelocalizationEnergy => TotalEnergy - Electrons;

    /// <summary>Порядок связи между центрами</summary>
    /// <param name="first">Номер первого центра</param>
    /// <param name="second">Номер второго центра</param>
    public double BondOrder(int first, int second)
        => BondOrderOf(Coefficients, Occupations, Count, first, second);

    /// <summary>Энергия орбитали в электронвольтах</summary>
    /// <param name="index">Номер орбитали</param>
    /// <param name="alpha">Кулоновский интеграл alpha, эВ</param>
    /// <param name="beta">Резонансный интеграл beta, эВ (отрицателен)</param>
    public double OrbitalEnergy(int index, double alpha = -11.4, double beta = -2.7)
        => alpha + (OrbitalCoefficients[index] * beta);

    /// <summary>Энергия перехода между граничными орбиталями, эВ</summary>
    /// <param name="beta">Резонансный интеграл beta, эВ (отрицателен)</param>
    public double ExcitationEnergy(double beta = -2.7) => Gap * Math.Abs(beta);

    /// <summary>
    /// Длина волны длинноволновой полосы поглощения, нм
    /// </summary>
    /// <param name="beta">Резонансный интеграл beta, эВ (отрицателен)</param>
    public double AbsorptionWavelength(double beta = -2.7)
    {
        double energy = ExcitationEnergy(beta);

        // Соотношение lambda[нм] = 1239.84 / E[эВ]
        return energy > 0 ? 1239.84195 / energy : double.NaN;
    }

    /// <summary>Абсолютная жёсткость по Пирсону, эВ</summary>
    /// <param name="beta">Резонансный интеграл beta, эВ (отрицателен)</param>
    public double Hardness(double beta = -2.7) => Gap * Math.Abs(beta) / 2;

    /// <summary>Абсолютная электроотрицательность, эВ</summary>
    /// <param name="alpha">Кулоновский интеграл alpha, эВ</param>
    /// <param name="beta">Резонансный интеграл beta, эВ (отрицателен)</param>
    public double Electronegativity(double alpha = -11.4, double beta = -2.7)
        => -(OrbitalEnergy(HomoIndex, alpha, beta) + OrbitalEnergy(LumoIndex, alpha, beta)) / 2;

    /// <summary>
    /// Выполняется ли правило Хюккеля 4n+2 для моноциклической системы
    /// </summary>
    public bool ObeysHuckelRule
        => Electrons > 0 && (Electrons - 2) % 4 == 0;

    /// <summary>Отчёт по решению</summary>
    public string Report()
    {
        var culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();

        text.AppendLine($"Метод Хюккеля: {(string.IsNullOrEmpty(_system.Name) ? "сопряжённая система" : _system.Name)}");
        text.AppendLine(string.Format(culture, "  Центров: {0}, электронов: {1}", Count, Electrons));
        text.AppendLine(string.Format(culture, "  Полная энергия: {0}·alpha + {1:F4}·beta", Electrons, TotalEnergy));
        text.AppendLine(string.Format(culture, "  Энергия делокализации: {0:F4}·beta", DelocalizationEnergy));

        if (HomoIndex >= 0 && LumoIndex >= 0)
        {
            text.AppendLine(string.Format(culture,
                "  ВЗМО x = {0:F4}, НСМО x = {1:F4}, щель {2:F4}·beta", Homo, Lumo, Gap));
            text.AppendLine(string.Format(culture,
                "  Оценка длинноволнового максимума: {0:F0} нм", AbsorptionWavelength()));
        }

        text.AppendLine("  Орбитали (x, заселённость):");

        for (int mo = 0; mo < Count; mo++)
        {
            text.AppendLine(string.Format(culture, "    {0,3}: x = {1,8:F4}  n = {2}",
                mo + 1, OrbitalCoefficients[mo], Occupations[mo]));
        }

        text.AppendLine("  Центры (плотность, заряд, свободная валентность):");

        for (int atom = 0; atom < Count; atom++)
        {
            text.AppendLine(string.Format(culture, "    {0,-5} q = {1,7:F4}  заряд = {2,7:F4}  F = {3,6:F3}",
                _system.Centers[atom], ChargeDensities[atom], Charges[atom], FreeValences[atom]));
        }

        return text.ToString();
    }

    private static double BondOrderOf(Matrix coefficients, IReadOnlyList<int> occupations, int count, int first, int second)
    {
        double order = 0;

        for (int mo = 0; mo < count; mo++)
            order += occupations[mo] * coefficients[first, mo] * coefficients[second, mo];

        return order;
    }
}

/// <summary>
/// Метод Хюккеля для сопряжённых систем
/// </summary>
public static class Huckel
{
    /// <summary>
    /// Решает задачу Хюккеля
    /// </summary>
    /// <param name="system">Сопряжённая система</param>
    public static HuckelSolution Solve(PiSystem system)
    {
        ArgumentNullException.ThrowIfNull(system);

        int count = system.Count;

        if (count == 0)
            throw new ArgumentException("Сопряжённая система пуста", nameof(system));

        int electrons = system.Electrons;

        if (electrons < 0 || electrons > 2 * count)
            throw new ArgumentException("Число электронов не помещается на орбиталях системы", nameof(system));

        Matrix topological = system.TopologicalMatrix();
        var (values, vectors) = JacobiEigen.Compute(topological, 500, 1e-13);

        // beta отрицателен, поэтому наименьшая энергия отвечает наибольшему x
        var order = Enumerable.Range(0, count).OrderByDescending(i => values[i]).ToArray();

        var sortedValues = new double[count];
        var sortedVectors = new Matrix(count, count);

        for (int mo = 0; mo < count; mo++)
        {
            sortedValues[mo] = values[order[mo]];

            for (int atom = 0; atom < count; atom++)
                sortedVectors[atom, mo] = vectors[atom, order[mo]];
        }

        Normalize(sortedVectors, count);

        var occupations = new int[count];
        int rest = electrons;

        for (int mo = 0; mo < count && rest > 0; mo++)
        {
            occupations[mo] = Math.Min(2, rest);
            rest -= occupations[mo];
        }

        return new HuckelSolution(system, sortedValues, sortedVectors, occupations);
    }

    /// <summary>
    /// Решает задачу Хюккеля для структуры, заданной SMILES
    /// </summary>
    /// <param name="smiles">Строка SMILES</param>
    /// <param name="charge">Заряд сопряжённой системы</param>
    public static HuckelSolution Solve(string smiles, int charge = 0)
        => Solve(PiSystem.FromSmiles(smiles, charge));

    /// <summary>
    /// Ортогонализация по Лёвдину: приводит обобщённую задачу H·c = E·S·c
    /// к обычной симметричной задаче на собственные значения
    /// </summary>
    /// <param name="hamiltonian">Матрица гамильтониана</param>
    /// <param name="overlap">Матрица перекрывания</param>
    /// <returns>Собственные энергии и коэффициенты в исходном базисе</returns>
    public static (double[] Energies, Matrix Coefficients) SolveGeneralized(Matrix hamiltonian, Matrix overlap)
    {
        ArgumentNullException.ThrowIfNull(hamiltonian);
        ArgumentNullException.ThrowIfNull(overlap);

        int n = hamiltonian.Height;

        if (hamiltonian.Width != n || overlap.Height != n || overlap.Width != n)
            throw new ArgumentException("Матрицы должны быть квадратными и одного размера");

        // S^(-1/2) считается через собственное разложение самой S
        var (values, vectors) = JacobiEigen.Compute(overlap, 500, 1e-13);
        var root = new Matrix(n, n);

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = 0;

                for (int k = 0; k < n; k++)
                {
                    if (values[k] <= 1e-12)
                        throw new ArgumentException("Матрица перекрывания вырождена", nameof(overlap));

                    sum += vectors[i, k] * vectors[j, k] / Math.Sqrt(values[k]);
                }

                root[i, j] = sum;
            }
        }

        Matrix transformed = root * hamiltonian * root;
        var (energies, transformedVectors) = JacobiEigen.Compute(transformed, 500, 1e-13);

        Matrix coefficients = root * transformedVectors;
        var order = Enumerable.Range(0, n).OrderBy(i => energies[i]).ToArray();

        var sortedEnergies = new double[n];
        var sortedCoefficients = new Matrix(n, n);

        for (int mo = 0; mo < n; mo++)
        {
            sortedEnergies[mo] = energies[order[mo]];

            for (int atom = 0; atom < n; atom++)
                sortedCoefficients[atom, mo] = coefficients[atom, order[mo]];
        }

        return (sortedEnergies, sortedCoefficients);
    }

    private static void Normalize(Matrix vectors, int count)
    {
        for (int mo = 0; mo < count; mo++)
        {
            double norm = 0;

            for (int atom = 0; atom < count; atom++)
                norm += vectors[atom, mo] * vectors[atom, mo];

            norm = Math.Sqrt(norm);

            if (norm < 1e-15)
                continue;

            // Знак орбитали произволен; фиксируем его по первому заметному коэффициенту,
            // иначе одна и та же система давала бы разные по знаку коэффициенты
            double sign = 1;

            for (int atom = 0; atom < count; atom++)
            {
                if (Math.Abs(vectors[atom, mo]) > 1e-9)
                {
                    sign = vectors[atom, mo] > 0 ? 1 : -1;
                    break;
                }
            }

            for (int atom = 0; atom < count; atom++)
                vectors[atom, mo] = vectors[atom, mo] / norm * sign;
        }
    }
}
