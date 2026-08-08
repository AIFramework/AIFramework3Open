using System.Globalization;

namespace AiFrameworkDemo.Core;

/// <summary>
/// Сборка структурированного результата демо (<see cref="DemoReport"/>).
///
/// Зачем: раньше каждый DemoRunner склеивал StringBuilder-ом строки вида
/// «  «слово»  TF-IDF=0,1010  TF=0,1429», выравнивая колонки пробелами.
/// Такой вывод нечитаем при 30+ строках и ломается на пропорциональном
/// шрифте. Здесь те же данные описываются структурно, а вёрстку берёт на
/// себя UI.
///
/// Использование:
/// <code>
///   var rep = new ReportBuilder()
///       .Metric("Лучший документ", "Нейросети", hint: "argmax по сумме TF·IDF")
///       .Metric("Скор", score, "", tone: MetricTone.Good)
///       .Note("Метрики нормированы на максимум.");
///
///   rep.Table("Релевантность по документам", ["Документ", "Score"], numeric: [false, true])
///      .Row("Нейросети", "0.2020")
///      .Row("Экономика", "0.0000");
///
///   return Png(cv, s, textOutput: log, report: rep.Build());
/// </code>
/// </summary>
public sealed class ReportBuilder
{
    private readonly List<DemoMetric> _metrics = [];
    private readonly List<DemoTable>  _tables  = [];
    private string? _note;

    /// <summary>Ключевое число результата. Показывается плашкой над графиком.</summary>
    public ReportBuilder Metric(string label, string value, string? unit = null,
        string? hint = null, MetricTone tone = MetricTone.Neutral)
    {
        _metrics.Add(new DemoMetric(label, value, unit, hint, tone));
        return this;
    }

    /// <summary>Числовая метрика: форматирование инвариантное, чтобы не зависеть от локали сервера.</summary>
    public ReportBuilder Metric(string label, double value, string? unit = null,
        string? hint = null, MetricTone tone = MetricTone.Neutral, string format = "G4")
    {
        return Metric(label, value.ToString(format, CultureInfo.InvariantCulture), unit, hint, tone);
    }

    /// <summary>
    /// Целочисленная метрика. Отдельная перегрузка нужна, чтобы счётчики не
    /// уходили в экспоненциальную запись: "G4" превратил бы 12345 в 1.234E+04.
    /// </summary>
    public ReportBuilder Metric(string label, int value, string? unit = null,
        string? hint = null, MetricTone tone = MetricTone.Neutral)
    {
        return Metric(label, value.ToString(CultureInfo.InvariantCulture), unit, hint, tone);
    }

    /// <summary>Как читать результат: одна-две строки под плашками.</summary>
    public ReportBuilder Note(string note)
    {
        _note = note;
        return this;
    }

    /// <summary>
    /// Начинает таблицу. Возвращает построитель строк, а не сам ReportBuilder:
    /// так нельзя случайно добавить строку не в ту таблицу.
    /// </summary>
    /// <param name="numeric">Флаги «колонка числовая» — выравнивание вправо.</param>
    public TableBuilder Table(string title, IReadOnlyList<string> headers,
        IReadOnlyList<bool>? numeric = null, string? note = null)
    {
        var rows = new List<IReadOnlyList<string>>();
        // Регистрируем сразу: порядок таблиц в отчёте — порядок объявления,
        // а не порядок, в котором в них полетели строки. Список строк общий,
        // поэтому поздние Row() попадут в уже добавленную таблицу.
        _tables.Add(new DemoTable(title, headers, rows, numeric, note));
        return new TableBuilder(this, headers, rows);
    }

    /// <summary>Пустые таблицы в отчёт не попадают: заголовок без строк только мешает.</summary>
    public DemoReport Build() => new()
    {
        Metrics = _metrics,
        Tables  = _tables.Where(t => t.Rows.Count > 0).ToList(),
        Note    = _note,
    };

    /// <summary>Построитель строк одной таблицы.</summary>
    public sealed class TableBuilder
    {
        private readonly ReportBuilder _owner;
        private readonly IReadOnlyList<string> _headers;
        private readonly List<IReadOnlyList<string>> _rows;

        internal TableBuilder(ReportBuilder owner, IReadOnlyList<string> headers,
            List<IReadOnlyList<string>> rows)
        {
            _owner   = owner;
            _headers = headers;
            _rows    = rows;
        }

        /// <summary>Добавляет строку. Лишние ячейки отбрасываются, недостающие дополняются пустыми.</summary>
        public TableBuilder Row(params string[] cells)
        {
            var normalized = new string[_headers.Count];
            for (int i = 0; i < normalized.Length; i++)
                normalized[i] = i < cells.Length ? cells[i] ?? "" : "";
            _rows.Add(normalized);
            return this;
        }

        /// <summary>Строка «подпись + числа» с единым форматом.</summary>
        public TableBuilder RowNum(string first, params double[] values)
        {
            var cells = new string[values.Length + 1];
            cells[0] = first;
            for (int i = 0; i < values.Length; i++)
                cells[i + 1] = values[i].ToString("F4", CultureInfo.InvariantCulture);
            return Row(cells);
        }

        /// <summary>Возврат к построителю отчёта — для чейнинга нескольких таблиц.</summary>
        public ReportBuilder End() => _owner;
    }
}
