namespace AI.Biology.Networks;

/// <summary>Аттрактор булевой сети: неподвижная точка или цикл</summary>
/// <param name="States">Состояния аттрактора в порядке смены</param>
/// <param name="BasinSize">Число начальных состояний, приходящих в этот аттрактор</param>
public sealed record BooleanAttractor(IReadOnlyList<bool[]> States, int BasinSize)
{
    /// <summary>Длина цикла</summary>
    public int Length => States.Count;

    /// <summary>Неподвижная ли это точка</summary>
    public bool IsFixedPoint => States.Count == 1;
}

/// <summary>
/// Булева сеть генной регуляции: каждый ген включён или выключен, следующее состояние
/// задаётся логической функцией от регуляторов.
/// </summary>
/// <remarks>
/// <para>
/// Модель Кауфмана (1969) грубее кинетики, но отвечает на главный качественный вопрос: какие
/// устойчивые режимы есть у регуляторной схемы. Аттракторы сопоставляют с типами клеток и их
/// состояниями, бассейны — с тем, насколько режим устойчив к возмущению.
/// </para>
/// <para>
/// Обновление синхронное: все гены меняются разом. Неподвижные точки при любой схеме обновления
/// одни и те же, а циклы — нет: синхронный цикл при асинхронном обновлении может распасться.
/// Аттракторы ищутся полным перебором 2ⁿ состояний, поэтому генов не больше двадцати.
/// </para>
/// <para>
/// Правило записывается формулой: имена генов, <c>!</c> или <c>NOT</c>, <c>&amp;</c> или <c>AND</c>,
/// <c>|</c> или <c>OR</c>, скобки, константы 0 и 1. Ген без правила сохраняет значение — так
/// задают внешние входы.
/// </para>
/// </remarks>
public sealed class BooleanNetwork
{
    /// <summary>Наибольшее число генов для перебора аттракторов</summary>
    public const int MaxEnumerableGenes = 20;

    private readonly string[] _genes;
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly Func<bool[], bool>?[] _rules;

    /// <summary>Создаёт сеть</summary>
    /// <param name="genes">Имена генов: буквы, цифры и подчёркивание, не с цифры</param>
    public BooleanNetwork(IEnumerable<string> genes)
    {
        ArgumentNullException.ThrowIfNull(genes);

        _genes = genes.ToArray();

        if (_genes.Length == 0)
            throw new ArgumentException("Нужен хотя бы один ген", nameof(genes));

        for (int i = 0; i < _genes.Length; i++)
        {
            string gene = _genes[i];

            if (string.IsNullOrEmpty(gene) || !(char.IsLetter(gene[0]) || gene[0] == '_')
                || !gene.All(c => char.IsLetterOrDigit(c) || c == '_') || IsKeyword(gene))
            {
                throw new ArgumentException($"Имя гена «{gene}» недопустимо", nameof(genes));
            }

            if (!_index.TryAdd(gene, i))
                throw new ArgumentException($"Ген «{gene}» повторяется", nameof(genes));
        }

        _rules = new Func<bool[], bool>?[_genes.Length];
    }

    /// <summary>Гены в порядке следования в векторе состояния</summary>
    public IReadOnlyList<string> Genes => _genes;

    /// <summary>Номер гена</summary>
    /// <param name="gene">Имя</param>
    public int IndexOf(string gene)
        => _index.TryGetValue(gene, out int index) ? index : throw new KeyNotFoundException($"Гена «{gene}» в сети нет");

    /// <summary>Задаёт правило формулой</summary>
    /// <param name="gene">Ген</param>
    /// <param name="formula">Логическая формула, например <c>A &amp; !B</c></param>
    public void SetRule(string gene, string formula)
    {
        ArgumentNullException.ThrowIfNull(formula);

        _rules[IndexOf(gene)] = BooleanFormula.Compile(formula, _index);
    }

    /// <summary>Задаёт правило функцией, получающей значения генов по имени</summary>
    /// <param name="gene">Ген</param>
    /// <param name="rule">Правило, например <c>s =&gt; s("A") &amp;&amp; !s("B")</c></param>
    public void SetRule(string gene, Func<Func<string, bool>, bool> rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        _rules[IndexOf(gene)] = state => rule(name => state[IndexOf(name)]);
    }

    /// <summary>Следующее состояние при синхронном обновлении</summary>
    /// <param name="state">Текущее состояние</param>
    public bool[] Step(IReadOnlyList<bool> state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Count != _genes.Length)
            throw new ArgumentException($"Состояние должно содержать {_genes.Length} значений", nameof(state));

        bool[] current = state.ToArray();
        var next = new bool[current.Length];

        for (int i = 0; i < next.Length; i++)
            next[i] = _rules[i]?.Invoke(current) ?? current[i];

        return next;
    }

    /// <summary>Все аттракторы синхронной динамики с размерами бассейнов</summary>
    public IReadOnlyList<BooleanAttractor> Attractors()
    {
        int n = _genes.Length;

        if (n > MaxEnumerableGenes)
            throw new InvalidOperationException($"Перебор состояний возможен при числе генов не больше {MaxEnumerableGenes}");

        int total = 1 << n;
        var basin = new int[total];
        var stamp = new int[total];
        var position = new int[total];
        Array.Fill(basin, -1);

        var cycles = new List<List<int>>();
        var sizes = new List<int>();
        var path = new List<int>();
        var buffer = new bool[n];

        for (int start = 0; start < total; start++)
        {
            if (basin[start] >= 0)
                continue;

            path.Clear();
            int state = start;

            while (basin[state] < 0 && stamp[state] != start + 1)
            {
                stamp[state] = start + 1;
                position[state] = path.Count;
                path.Add(state);
                state = Next(state, buffer);
            }

            int attractor;

            if (basin[state] < 0)
            {
                attractor = cycles.Count;
                cycles.Add(path.Skip(position[state]).ToList());
                sizes.Add(0);
            }
            else
            {
                attractor = basin[state];
            }

            foreach (int visited in path)
                basin[visited] = attractor;

            sizes[attractor] += path.Count;
        }

        return cycles
            .Select((cycle, i) => new BooleanAttractor(cycle.Select(Decode).ToList(), sizes[i]))
            .ToList();
    }

    /// <summary>Неподвижные точки — при любой схеме обновления одни и те же</summary>
    public IReadOnlyList<bool[]> FixedPoints()
        => Attractors().Where(a => a.IsFixedPoint).Select(a => a.States[0]).ToList();

    /// <summary>Запись состояния нулями и единицами в порядке генов</summary>
    /// <param name="state">Состояние</param>
    public static string Format(IReadOnlyList<bool> state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new string(state.Select(v => v ? '1' : '0').ToArray());
    }

    private int Next(int code, bool[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = ((code >> i) & 1) == 1;

        int next = 0;

        for (int i = 0; i < buffer.Length; i++)
        {
            if (_rules[i]?.Invoke(buffer) ?? buffer[i])
                next |= 1 << i;
        }

        return next;
    }

    private bool[] Decode(int code)
    {
        var state = new bool[_genes.Length];

        for (int i = 0; i < state.Length; i++)
            state[i] = ((code >> i) & 1) == 1;

        return state;
    }

    private static bool IsKeyword(string word)
        => word.ToUpperInvariant() is "AND" or "OR" or "NOT" or "TRUE" or "FALSE";

    private static class BooleanFormula
    {
        public static Func<bool[], bool> Compile(string text, IReadOnlyDictionary<string, int> index)
        {
            List<string> tokens = Tokenize(text);
            int position = 0;

            Func<bool[], bool> result = Or();

            if (position != tokens.Count)
                throw new FormatException($"Лишний символ «{tokens[position]}» в формуле «{text}»");

            return result;

            bool Accept(string token)
            {
                if (position < tokens.Count && tokens[position] == token)
                {
                    position++;
                    return true;
                }

                return false;
            }

            Func<bool[], bool> Or()
            {
                Func<bool[], bool> left = And();

                while (Accept("|"))
                {
                    Func<bool[], bool> first = left;
                    Func<bool[], bool> second = And();
                    left = state => first(state) || second(state);
                }

                return left;
            }

            Func<bool[], bool> And()
            {
                Func<bool[], bool> left = Not();

                while (Accept("&"))
                {
                    Func<bool[], bool> first = left;
                    Func<bool[], bool> second = Not();
                    left = state => first(state) && second(state);
                }

                return left;
            }

            Func<bool[], bool> Not()
            {
                if (Accept("!"))
                {
                    Func<bool[], bool> inner = Not();
                    return state => !inner(state);
                }

                return Atom();
            }

            Func<bool[], bool> Atom()
            {
                if (position >= tokens.Count)
                    throw new FormatException($"Формула «{text}» оборвана");

                string token = tokens[position++];

                if (token == "(")
                {
                    Func<bool[], bool> inner = Or();

                    return Accept(")") ? inner : throw new FormatException($"Не закрыта скобка в формуле «{text}»");
                }

                if (token is "0" or "FALSE")
                    return _ => false;

                if (token is "1" or "TRUE")
                    return _ => true;

                if (!index.TryGetValue(token, out int gene))
                    throw new FormatException($"В формуле «{text}» неизвестный ген «{token}»");

                return state => state[gene];
            }
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            int i = 0;

            while (i < text.Length)
            {
                char c = text[i];

                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c is '(' or ')' or '!')
                {
                    tokens.Add(c.ToString());
                    i++;
                    continue;
                }

                if (c is '&' or '|')
                {
                    tokens.Add(c.ToString());
                    i += i + 1 < text.Length && text[i + 1] == c ? 2 : 1;
                    continue;
                }

                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    int start = i;

                    while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
                        i++;

                    string word = text[start..i];

                    tokens.Add(word.ToUpperInvariant() switch
                    {
                        "AND" => "&",
                        "OR" => "|",
                        "NOT" => "!",
                        "TRUE" => "TRUE",
                        "FALSE" => "FALSE",
                        _ => word
                    });

                    continue;
                }

                throw new FormatException($"Символ «{c}» в формуле «{text}» не разобран");
            }

            return tokens;
        }
    }
}
