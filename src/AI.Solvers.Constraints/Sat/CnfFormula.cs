namespace AI.Solvers.Constraints.Sat;

/// <summary>
/// Формула в конъюнктивной нормальной форме.
/// </summary>
/// <remarks>
/// <para>
/// Литералы записываются так же, как в формате DIMACS: переменные нумеруются с единицы,
/// отрицание обозначается минусом. Дизъюнкт <c>[1, -2, 3]</c> означает
/// <c>x₁ ∨ ¬x₂ ∨ x₃</c>.
/// </para>
/// <para>
/// Кроме прямого добавления дизъюнктов есть частые сочетания — «ровно один», «не больше
/// одного», импликация: они выражаются через дизъюнкты, но писать их руками каждый раз
/// утомительно и легко ошибиться в знаке.
/// </para>
/// </remarks>
public sealed class CnfFormula
{
    private readonly List<int[]> _clauses = [];
    private int _variables;

    /// <summary>Число переменных</summary>
    public int VariableCount => _variables;

    /// <summary>Число дизъюнктов</summary>
    public int ClauseCount => _clauses.Count;

    /// <summary>Дизъюнкты формулы</summary>
    public IReadOnlyList<int[]> Clauses => _clauses;

    /// <summary>
    /// Объявляет новую переменную и возвращает её номер
    /// </summary>
    public int AddVariable() => ++_variables;

    /// <summary>
    /// Объявляет несколько переменных и возвращает их номера
    /// </summary>
    /// <param name="count">Сколько переменных объявить</param>
    public int[] AddVariables(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var created = new int[count];

        for (int i = 0; i < count; i++)
            created[i] = AddVariable();

        return created;
    }

    /// <summary>
    /// Добавляет дизъюнкт
    /// </summary>
    /// <param name="literals">Литералы: номер переменной со знаком</param>
    /// <exception cref="ArgumentException">Литерал нулевой либо ссылается на необъявленную переменную</exception>
    public CnfFormula AddClause(params int[] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);

        foreach (int literal in literals)
        {
            if (literal == 0)
                throw new ArgumentException("Литерал не может быть нулём: переменные нумеруются с единицы", nameof(literals));

            int variable = Math.Abs(literal);

            if (variable > _variables)
                _variables = variable;
        }

        _clauses.Add((int[])literals.Clone());

        return this;
    }

    /// <summary>
    /// Добавляет дизъюнкт из перечисления литералов
    /// </summary>
    /// <param name="literals">Литералы</param>
    public CnfFormula AddClause(IEnumerable<int> literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        return AddClause(literals.ToArray());
    }

    /// <summary>
    /// Требует истинности литерала
    /// </summary>
    /// <param name="literal">Литерал</param>
    public CnfFormula Assert(int literal) => AddClause(literal);

    /// <summary>
    /// Импликация: из <paramref name="premise"/> следует <paramref name="conclusion"/>
    /// </summary>
    /// <param name="premise">Посылка</param>
    /// <param name="conclusion">Следствие</param>
    public CnfFormula Implies(int premise, int conclusion) => AddClause(-premise, conclusion);

    /// <summary>
    /// Не больше одного истинного литерала — попарные запреты
    /// </summary>
    /// <param name="literals">Литералы</param>
    /// <remarks>
    /// Кодировка попарная: <c>n(n−1)/2</c> дизъюнктов. Для десятков литералов это дёшево,
    /// для сотен стоит переходить к порядковой или лестничной кодировке со вспомогательными
    /// переменными.
    /// </remarks>
    public CnfFormula AtMostOne(params int[] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);

        for (int i = 0; i < literals.Length; i++)
            for (int j = i + 1; j < literals.Length; j++)
                _ = AddClause(-literals[i], -literals[j]);

        return this;
    }

    /// <summary>
    /// Хотя бы один истинный литерал
    /// </summary>
    /// <param name="literals">Литералы</param>
    public CnfFormula AtLeastOne(params int[] literals) => AddClause(literals);

    /// <summary>
    /// Ровно один истинный литерал
    /// </summary>
    /// <param name="literals">Литералы</param>
    public CnfFormula ExactlyOne(params int[] literals)
    {
        _ = AtLeastOne(literals);
        return AtMostOne(literals);
    }

    /// <summary>Запись формулы в формате DIMACS CNF</summary>
    public string ToDimacs()
    {
        var text = new System.Text.StringBuilder();
        _ = text.Append("p cnf ").Append(_variables).Append(' ').Append(_clauses.Count).AppendLine();

        foreach (int[] clause in _clauses)
        {
            foreach (int literal in clause)
                _ = text.Append(literal).Append(' ');

            _ = text.AppendLine("0");
        }

        return text.ToString();
    }

    /// <summary>Краткое описание формулы</summary>
    public override string ToString() => $"КНФ: переменных {_variables}, дизъюнктов {_clauses.Count}";
}
