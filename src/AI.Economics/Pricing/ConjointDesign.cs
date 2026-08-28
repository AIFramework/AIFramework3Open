using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Economics.Pricing;

/// <summary>
/// Атрибут conjoint-исследования: свойство товара, которое варьируется
/// в предъявляемых карточках.
/// </summary>
/// <param name="Name">Название атрибута.</param>
/// <param name="Levels">Названия уровней; первый становится базой кодирования.</param>
/// <param name="NumericValues">
/// Числовые значения уровней. Заданы — атрибут кодируется одним непрерывным
/// столбцом вместо набора индикаторов. Так задаётся цена: тогда коэффициент
/// при ней имеет размерность полезности на рубль и позволяет пересчитать
/// частные полезности в деньги.
/// </param>
public sealed record ConjointAttribute(
    string Name,
    IReadOnlyList<string> Levels,
    IReadOnlyList<double>? NumericValues = null)
{
    /// <summary>Кодируется ли атрибут одним непрерывным столбцом.</summary>
    public bool IsNumeric => NumericValues is { Count: > 0 };

    /// <summary>Число столбцов матрицы плана, занимаемых атрибутом.</summary>
    public int ColumnCount => IsNumeric ? 1 : Math.Max(Levels.Count - 1, 0);
}

/// <summary>Карточка товара: по одному уровню каждого атрибута.</summary>
/// <param name="LevelIndices">Индексы выбранных уровней в порядке атрибутов.</param>
public sealed record ConjointProfile(IReadOnlyList<int> LevelIndices);

/// <summary>Одно задание на выбор: респондент выбрал одну карточку из набора.</summary>
public sealed record ChoiceTask
{
    /// <summary>Идентификатор респондента.</summary>
    public int Respondent { get; init; }

    /// <summary>Предъявленные карточки.</summary>
    public IReadOnlyList<ConjointProfile> Alternatives { get; init; } = [];

    /// <summary>Индекс выбранной карточки.</summary>
    public int ChosenIndex { get; init; }
}

/// <summary>
/// План conjoint-исследования: набор атрибутов и правило кодирования карточек
/// в матрицу признаков.
/// </summary>
/// <remarks>
/// Категориальные атрибуты кодируются индикаторами с базовым уровнем: для
/// атрибута с тремя уровнями создаются два столбца, полезность первого уровня
/// принимается за ноль. Без базового уровня матрица плана вырождена, и
/// коэффициенты не определены.
/// </remarks>
public sealed class ConjointDesign
{
    private readonly List<ConjointAttribute> _attributes;

    /// <summary>Создаёт план исследования.</summary>
    /// <param name="attributes">Атрибуты в фиксированном порядке.</param>
    /// <exception cref="ArgumentNullException">Атрибуты не заданы.</exception>
    /// <exception cref="ArgumentException">Нет ни одного атрибута с двумя уровнями.</exception>
    public ConjointDesign(IReadOnlyList<ConjointAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        _attributes = [.. attributes];

        if (_attributes.Count == 0 || _attributes.All(a => a.ColumnCount == 0))
            throw new ArgumentException("Нужен хотя бы один варьирующийся атрибут.", nameof(attributes));

        ParameterNames = BuildNames();
    }

    /// <summary>Атрибуты плана.</summary>
    public IReadOnlyList<ConjointAttribute> Attributes => _attributes;

    /// <summary>Число оцениваемых коэффициентов.</summary>
    public int ParameterCount => _attributes.Sum(a => a.ColumnCount);

    /// <summary>Имена коэффициентов в порядке столбцов матрицы плана.</summary>
    public IReadOnlyList<string> ParameterNames { get; }

    /// <summary>
    /// Индекс столбца ценового атрибута; −1, если числовой атрибут не задан.
    /// </summary>
    public int PriceColumn
    {
        get
        {
            int column = 0;
            foreach (ConjointAttribute attribute in _attributes)
            {
                if (attribute.IsNumeric) return column;
                column += attribute.ColumnCount;
            }
            return -1;
        }
    }

    /// <summary>Кодирует карточку в строку матрицы плана.</summary>
    /// <param name="profile">Карточка.</param>
    /// <returns>Вектор признаков длиной <see cref="ParameterCount"/>.</returns>
    /// <exception cref="ArgumentNullException">Карточка не задана.</exception>
    /// <exception cref="ArgumentException">Число уровней не совпадает с числом атрибутов.</exception>
    public double[] Encode(ConjointProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.LevelIndices.Count != _attributes.Count)
            throw new ArgumentException(
                "Карточка должна задавать по одному уровню на каждый атрибут.", nameof(profile));

        var row = new double[ParameterCount];
        int column = 0;

        for (int a = 0; a < _attributes.Count; a++)
        {
            ConjointAttribute attribute = _attributes[a];
            int level = profile.LevelIndices[a];

            if (attribute.IsNumeric)
            {
                row[column] = attribute.NumericValues![level];
                column++;
                continue;
            }

            // Базовый уровень не получает столбца: его полезность равна нулю
            if (level > 0) row[column + level - 1] = 1.0;
            column += attribute.ColumnCount;
        }

        return row;
    }

    private List<string> BuildNames()
    {
        var names = new List<string>(ParameterCount);

        foreach (ConjointAttribute attribute in _attributes)
        {
            if (attribute.IsNumeric)
            {
                names.Add(attribute.Name);
                continue;
            }

            for (int level = 1; level < attribute.Levels.Count; level++)
                names.Add($"{attribute.Name}: {attribute.Levels[level]}");
        }

        return names;
    }
}
