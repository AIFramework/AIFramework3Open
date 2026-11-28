using AI.DataStructs.Algebraic;
using AI.Econometrics;
using AI.Insights;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Общее для пространств над таблицами — <c>regress</c>, <c>ts</c>, <c>causal</c>, <c>opt</c>: выборка колонок
/// из таблицы и оформление результатов.
/// </summary>
/// <remarks>
/// Эти пространства отдают результат одинаково: запись с числами и таблицами, а в конце —
/// <c>summary</c> и <c>warnings</c> из разбора, который делает сама библиотека
/// (<see cref="IInterpretable"/>). Предупреждения там — нарушенные допущения, из-за которых
/// числам верить нельзя, и потерять их по дороге в скрипт значило бы отдать автору ровно ту
/// часть ответа, которая выглядит убедительно.
/// </remarks>
internal static class ScriptData
{
    /// <summary>Колонки таблицы матрицей объект × признак, в указанном порядке.</summary>
    public static Matrix Columns(ScriptTable data, IReadOnlyList<string> names, string function)
    {
        if (names.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{function}: не указано ни одной колонки");

        var matrix = new Matrix(data.RowCount, names.Count);

        for (int j = 0; j < names.Count; j++)
        {
            Vector column = Column(data, names[j], function);

            for (int i = 0; i < column.Count; i++) matrix[i, j] = column[i];
        }

        return matrix;
    }

    /// <summary>
    /// Числовая колонка; отказ, если в ней пропуск.
    /// </summary>
    /// <remarks>
    /// Пропуск в таблице становится <c>nan</c>, и оценка по такой колонке выходит <c>nan</c>
    /// целиком — либо, хуже, число, посчитанное по части строк без предупреждения.
    /// </remarks>
    public static Vector Column(ScriptTable data, string name, string function)
    {
        Vector column = data.Column(name).ToVector();

        for (int i = 0; i < column.Count; i++)
        {
            if (double.IsNaN(column[i]))
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: в колонке '{name}' пропуск в строке {i}",
                    "уберите неполные строки: table.drop_na()");
            }
        }

        return column;
    }

    /// <summary>Колонка воздействия либо бинарного отклика: только нули и единицы.</summary>
    public static Vector Binary(ScriptTable data, string name, string function)
    {
        Vector column = Column(data, name, function);

        for (int i = 0; i < column.Count; i++)
        {
            if (column[i] is not (0 or 1))
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: в колонке '{name}' значение {column[i]} в строке {i}, а ждутся 0 и 1",
                    "перекодируйте колонку: table.derive(...) с условием if");
            }
        }

        return column;
    }

    /// <summary>Колонка целых чисел: номера периодов.</summary>
    public static int[] Integers(ScriptTable data, string name, string function)
    {
        Vector column = Column(data, name, function);
        var values = new int[column.Count];

        for (int i = 0; i < column.Count; i++)
        {
            if (column[i] != Math.Round(column[i]) || Math.Abs(column[i]) > int.MaxValue)
            {
                throw new ScriptError(
                    DiagnosticCodes.BadOperand,
                    $"{function}: в колонке '{name}' нецелое значение {column[i]} в строке {i}",
                    "номера периодов — целые числа: 1, 2, 3 или 2021, 2022");
            }

            values[i] = (int)column[i];
        }

        return values;
    }

    /// <summary>
    /// Идентификаторы объектов: целые числа как есть, строки — номерами в порядке появления.
    /// </summary>
    /// <remarks>
    /// Объект в данных чаще называют словом — регион, фирма, магазин, — чем номером, и требовать
    /// перекодировки ради того, что библиотеке нужны целые, было бы переносом её забот на автора.
    /// </remarks>
    public static int[] Codes(ScriptTable data, string name, string function)
    {
        ScriptColumn column = data.Column(name);

        if (column.Type == ScriptType.Num) return Integers(data, name, function);

        if (column.Type != ScriptType.Str)
        {
            throw new ScriptError(
                DiagnosticCodes.TypeMismatch,
                $"{function}: колонка '{name}' имеет тип {column.Type.ToName()}",
                "идентификатор объекта — число или строка");
        }

        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var codes = new int[column.Count];

        for (int i = 0; i < column.Count; i++)
        {
            string key = column[i].AsString();

            if (!seen.TryGetValue(key, out int code))
            {
                code = seen.Count + 1;
                seen[key] = code;
            }

            codes[i] = code;
        }

        return codes;
    }

    /// <summary>Панельный набор из таблицы: отклик, регрессоры, объект и период.</summary>
    public static PanelDataset Panel(
        ScriptTable data, string y, IReadOnlyList<string> x, string unit, string period, string function) => new()
    {
        Regressors = Columns(data, x, function),
        Response = Column(data, y, function),
        Units = Codes(data, unit, function),
        Periods = Integers(data, period, function),
        Names = x,
    };

    /// <summary>Колонка, не вошедшая в модель, и причина.</summary>
    public sealed record SkippedColumn(string Column, string Type, string Reason);

    /// <summary>Выбор рядов модели: что вошло и что пропущено.</summary>
    public sealed record SeriesChoice(string[] Names, IReadOnlyList<SkippedColumn> Skipped)
    {
        /// <summary>Строки для <c>warnings</c>: по одной на пропущенную колонку.</summary>
        public IEnumerable<string> Notes(string function) => Skipped.Select(column =>
            $"{function}: колонка '{column.Column}' не вошла в модель — {column.Reason}");
    }

    /// <summary>
    /// Ряды многомерной модели: указанные колонки либо все числовые.
    /// </summary>
    /// <remarks>
    /// По умолчанию берутся только числовые колонки: у таблицы с рядами почти всегда есть колонка
    /// даты, и требовать её явно исключать — значит заставлять каждого автора сделать одно и то же.
    /// Но пропуск не молчит: каждая не взятая колонка возвращается с причиной. Иначе выгрузка с
    /// десятичной запятой, где «выручка» прочиталась текстом, давала бы модель без выручки — и
    /// ни одного признака, что её там нет.
    /// </remarks>
    public static SeriesChoice Series(ScriptTable data, string[]? columns, string function)
    {
        var skipped = new List<SkippedColumn>();
        string[] names;

        if (columns is { Length: > 0 })
        {
            names = columns;
        }
        else
        {
            var numeric = new List<string>();

            foreach (ScriptColumn column in data.Columns)
            {
                if (column.Type == ScriptType.Num) numeric.Add(column.Name);
                else skipped.Add(new SkippedColumn(column.Name, column.Type.ToName(), SkipReason(column)));
            }

            names = [.. numeric];
        }

        if (names.Length < 2)
        {
            string left = skipped.Count == 0
                ? string.Empty
                : "; не взяты: " + string.Join("; ", skipped.Select(column => $"'{column.Column}' — {column.Reason}"));

            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: многомерной модели нужны хотя бы два ряда, а найдено {names.Length}{left}",
                "ряды — числовые колонки таблицы; перечислите их в columns: [...]");
        }

        return new SeriesChoice(names, skipped);
    }

    /// <summary>Почему колонка не числовая — словами, с подсказкой, если это поправимо.</summary>
    private static string SkipReason(ScriptColumn column)
    {
        if (column.Type == ScriptType.Date) return "это даты, а не ряд";

        if (column.Type != ScriptType.Str) return $"тип {column.Type.ToName()}, а ряд — числа";

        for (int i = 0; i < column.Count; i++)
        {
            if (column[i].IsNone) continue;

            string text = column[i].AsString().Trim();

            if (text.Length == 0) continue;

            // Не разбор числа, а только признак: в ячейке нет ничего, кроме цифр и знаков записи числа
            bool numberLike = text.Any(char.IsAsciiDigit)
                && text.All(c => char.IsAsciiDigit(c) || char.IsWhiteSpace(c) || "., +-−%₽$€'\u00A0\u202F".Contains(c, StringComparison.Ordinal));

            return numberLike
                ? $"текст, похожий на числа («{text}»): так читается выгрузка с десятичной запятой или пробелами в разрядах; table.clean(...) сделает колонку числовой"
                : $"текст («{text}»), а ряд — числа";
        }

        return "в колонке нет значений";
    }

    /// <summary>Строка таблицы ограничений: имя, коэффициенты по переменным, знак и правая часть.</summary>
    public sealed record ConstraintRow(string Name, Vector Coefficients, string Sign, double Rhs);

    /// <summary>
    /// Ограничения из таблицы: колонка на переменную, <c>sign</c>, <c>rhs</c> и необязательная <c>name</c>.
    /// </summary>
    /// <remarks>
    /// Формат один на <c>opt</c> и <c>csp</c>: таблица ограничений, собранная для одного решателя,
    /// годится другому. Колонка, не совпавшая ни с одной переменной, — отказ, а не молча
    /// пропущенный столбец: опечатка в имени иначе обнуляла бы коэффициент, и решение выходило бы
    /// «найденным» без ограничения.
    /// </remarks>
    /// <param name="table">Таблица ограничений.</param>
    /// <param name="variables">Переменные задачи в порядке объявления.</param>
    /// <param name="function">Имя функции для сообщений.</param>
    /// <param name="unknownHint">Подсказка для колонки, не совпавшей с переменной.</param>
    /// <param name="signs">Допустимые знаки.</param>
    public static List<ConstraintRow> ConstraintRows(
        ScriptTable table,
        IReadOnlyList<string> variables,
        string function,
        Func<string, string> unknownHint,
        params string[] signs)
    {
        if (!table.TryGet("sign", out ScriptColumn signColumn))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: в таблице ограничений нет колонки sign",
                $"sign — знак ограничения: {string.Join(", ", signs.Select(sign => $"\"{sign}\""))}");
        }

        if (!table.TryGet("rhs", out _))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: в таблице ограничений нет колонки rhs",
                "rhs — правая часть ограничения");
        }

        var known = new HashSet<string>(variables, StringComparer.Ordinal);

        foreach (string column in table.Names())
        {
            if (column is "sign" or "rhs" or "name" || known.Contains(column)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: колонка '{column}' не переменная задачи",
                unknownHint(column));
        }

        Vector rhs = Column(table, "rhs", function);
        Vector?[] columns = [.. variables.Select(v => table.TryGet(v, out _) ? Column(table, v, function) : null)];
        bool named = table.TryGet("name", out ScriptColumn names);
        (string Name, string Value)[] allowed = [.. signs.Select(sign => (sign, sign))];
        var rows = new List<ConstraintRow>(table.RowCount);

        for (int i = 0; i < table.RowCount; i++)
        {
            var coefficients = new Vector(variables.Count);

            for (int j = 0; j < variables.Count; j++) coefficients[j] = columns[j]?[i] ?? 0;

            string sign = Kind(signColumn[i].AsString("знак ограничения"), "sign", function, allowed);
            string name = named ? names[i].AsString("имя ограничения") : $"c{i + 1}";

            rows.Add(new ConstraintRow(name, coefficients, sign, rhs[i]));
        }

        return rows;
    }

    /// <summary>Значение строкового варианта; отказ со списком известных.</summary>
    public static T Kind<T>(string value, string parameter, string function, params (string Name, T Value)[] options)
    {
        foreach ((string name, T option) in options)
        {
            if (name == value) return option;
        }

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{function}: неизвестное значение {parameter}: \"{value}\"",
            $"известны: {string.Join(", ", options.Select(option => $"\"{option.Name}\""))}");
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new ScriptError(DiagnosticCodes.BadOperand, message);
    }

    // --- оформление ---

    /// <summary>Таблица коэффициентов: имя, оценка, ошибка, t, p и 95-процентный интервал.</summary>
    public static ScriptTable Coefficients(IReadOnlyList<Coefficient> coefficients) => Table(coefficients,
        ("name", c => c.Name),
        ("estimate", c => c.Estimate),
        ("std_error", c => c.StandardError),
        ("t", c => c.TStatistic),
        ("p", c => c.PValue),
        ("ci_low", c => c.ConfidenceLow),
        ("ci_high", c => c.ConfidenceHigh));

    /// <summary>Таблица из строк результата: колонка на каждое поле.</summary>
    public static ScriptTable Table<T>(IReadOnlyList<T> rows, params (string Name, Func<T, object> Value)[] columns)
    {
        var built = new ScriptColumn[columns.Length];

        for (int j = 0; j < columns.Length; j++)
        {
            Func<T, object> value = columns[j].Value;
            var values = new ScriptValue[rows.Count];

            for (int i = 0; i < rows.Count; i++) values[i] = Value(value(rows[i]));

            built[j] = ScriptColumn.Own(columns[j].Name, values);
        }

        return ScriptTable.Create(built);
    }

    /// <summary>Запись результата с разбором библиотеки в конце: <c>summary</c> и <c>warnings</c>.</summary>
    public static ScriptRecord Record(IInterpretable result, params (string Name, object Value)[] fields)
    {
        Interpretation verdict = result.Interpret();

        return Plain([.. fields, ("summary", verdict.Summary), ("warnings", verdict.Warnings)]);
    }

    /// <summary>
    /// Запись результата модели по рядам таблицы: кроме разбора библиотеки — что вошло и что пропущено.
    /// </summary>
    /// <remarks>
    /// Пропущенные колонки отдаются дважды: таблицей <c>skipped</c> (колонка, тип, причина) для
    /// проверки в скрипте и строками в начале <c>warnings</c> — их видит тот, кто просто печатает результат.
    /// </remarks>
    public static ScriptRecord Record(
        IInterpretable result, SeriesChoice series, string function, params (string Name, object Value)[] fields)
    {
        Interpretation verdict = result.Interpret();
        IReadOnlyList<string> warnings = [.. series.Notes(function), .. verdict.Warnings];

        return Plain([
            ("variables", series.Names),
            ("skipped", Table(series.Skipped, ("column", c => c.Column), ("type", c => c.Type), ("reason", c => c.Reason))),
            .. fields,
            ("summary", verdict.Summary),
            ("warnings", warnings)]);
    }

    /// <summary>Запись из пар «имя — значение».</summary>
    public static ScriptRecord Plain(params (string Name, object Value)[] fields)
    {
        var built = new List<KeyValuePair<string, ScriptValue>>(fields.Length);

        foreach ((string name, object value) in fields)
            built.Add(new KeyValuePair<string, ScriptValue>(name, Value(value)));

        return ScriptRecord.From(built);
    }

    /// <summary>
    /// Запись чисел по именам: состояние системы, параметры модели.
    /// </summary>
    /// <remarks>
    /// Так функция скрипта получает текущее состояние в <c>sim.stock_flow</c> и <c>ode.solve</c>
    /// и параметры в <c>fit.curve</c>: <c>s.prey</c>, <c>p.a</c> — по имени, а не по номеру.
    /// </remarks>
    public static ScriptRecord Numbers(IReadOnlyList<string> names, IReadOnlyList<double> values)
    {
        var fields = new List<KeyValuePair<string, ScriptValue>>(names.Count);

        for (int j = 0; j < names.Count; j++) fields.Add(new(names[j], ScriptValue.Num(values[j])));

        return ScriptRecord.From(fields);
    }

    private static ScriptValue Value(object value) => value switch
    {
        ScriptValue scriptValue => scriptValue,
        double number => ScriptValue.Num(number),
        int number => ScriptValue.Num(number),
        bool flag => ScriptValue.Bool(flag),
        string text => ScriptValue.Str(text),
        Vector vector => ScriptValue.Vec(vector),
        Matrix matrix => ScriptValue.Mat(matrix),
        ScriptTable table => ScriptValue.Table(table),
        ScriptRecord record => ScriptValue.Record(record),
        IReadOnlyList<string> texts => ScriptValue.List(ScriptList.Own([.. texts.Select(ScriptValue.Str)])),
        _ => throw new InvalidOperationException($"тип {value.GetType().Name} не переводится в значение языка"),
    };
}
