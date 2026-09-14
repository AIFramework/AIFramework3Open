#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AI.Geometry.Constraints;

/// <summary>
/// Эскиз для решения геометрических задач: именованные скалярные неизвестные, построенные на них
/// сущности (точки, прямые и отрезки, окружности) и ограничения между ними.
/// </summary>
/// <remarks>
/// <para>
/// Модель плоская и удобна для построения из записей с текстовыми идентификаторами: задача, описанная словами,
/// переводится в список сущностей, ограничений и запросов, эскиз вычисляет ответ.
/// </para>
/// <para>
/// Точка «A» создает неизвестные «A.x» и «A.y»; окружность «c» создает неизвестную радиуса «c.r».
/// Свободный параметр (неизвестная длина, угол, отношение) добавляется методом <see cref="AddParameter"/>
/// и может стоять на месте числа в ограничениях. Углы задаются в радианах.
/// </para>
/// <para>
/// Точка, добавленная с тремя координатами, создает еще «A.z», и эскиз становится пространственным: расстояния, длины,
/// совпадение, середина, принадлежность прямой, параллельность, перпендикулярность и угол считаются в пространстве,
/// а прямые можно сочетать с плоскостями (<see cref="AddPlane"/>). Эскиз только из плоских точек решается как раньше.
/// </para>
/// <example>
/// <code>
/// var sketch = new GeometricSketch();
/// sketch.AddPoint("A", 0, 0, isFixed: true);
/// sketch.AddPoint("B", 1, 0);
/// sketch.AddPoint("C", 1, 1);
/// sketch.AddLine("AB", "A", "B");
/// sketch.Horizontal("AB");
/// sketch.Distance("A", "B", 3);
/// sketch.Distance("B", "C", 4);
/// sketch.Distance("C", "A", 5);
/// SketchSolution solution = sketch.Solve();
/// double angle = solution.AngleAt("A", "B", "C"); // π/2
/// </code>
/// </example>
/// </remarks>
public sealed partial class GeometricSketch
{
    private const string HiddenPrefix = "#";

    private readonly List<string> _names = [];
    private readonly List<double> _guesses = [];
    private readonly List<bool> _fixed = [];
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly HashSet<string> _points = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Start, string End)> _lines = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string Center, string Radius)> _circles = new(StringComparer.Ordinal);
    private readonly HashSet<string> _spatial = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string A, string B, string C)> _planes = new(StringComparer.Ordinal);
    private readonly List<SketchConstraint> _constraints = [];
    private int _constantCount;
    private string? _zero;

    /// <summary>
    /// Имена видимых неизвестных (координаты точек, радиусы, параметры) в порядке добавления.
    /// </summary>
    public IReadOnlyList<string> Unknowns => _names.Where(name => !IsHidden(name)).ToList();

    /// <summary>
    /// Ограничения эскиза в порядке добавления.
    /// </summary>
    public IReadOnlyList<SketchConstraint> Constraints => _constraints;

    /// <summary>
    /// Добавляет свободный скалярный параметр (неизвестную длину, угол, отношение).
    /// </summary>
    /// <param name="name">Имя параметра; не должно начинаться с «#».</param>
    /// <param name="guess">Начальное приближение.</param>
    /// <param name="isFixed">Если true, значение считается известным и не меняется решателем.</param>
    /// <returns>Имя параметра.</returns>
    public string AddParameter(string name, double guess, bool isFixed = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (IsHidden(name))
            throw new ArgumentException($"Имя «{name}» не должно начинаться с «{HiddenPrefix}».", nameof(name));

        AddUnknown(name, guess, isFixed);
        return name;
    }

    /// <summary>
    /// Добавляет точку плоскости с неизвестными «id.x» и «id.y».
    /// </summary>
    /// <param name="id">Идентификатор точки.</param>
    /// <param name="x">Начальное приближение абсциссы.</param>
    /// <param name="y">Начальное приближение ординаты.</param>
    /// <param name="isFixed">Если true, обе координаты известны и не меняются решателем.</param>
    public void AddPoint(string id, double x, double y, bool isFixed = false)
    {
        EnsureNewEntity(id);
        AddUnknown(X(id), x, isFixed);
        AddUnknown(Y(id), y, isFixed);
        _points.Add(id);
    }

    /// <summary>
    /// Добавляет точку пространства с неизвестными «id.x», «id.y» и «id.z».
    /// Ограничения, где участвует такая точка или плоскость, считаются в пространстве; плоская точка в них лежит при z = 0.
    /// </summary>
    /// <param name="id">Идентификатор точки.</param>
    /// <param name="x">Начальное приближение абсциссы.</param>
    /// <param name="y">Начальное приближение ординаты.</param>
    /// <param name="z">Начальное приближение аппликаты.</param>
    /// <param name="isFixed">Если true, все три координаты известны и не меняются решателем.</param>
    public void AddPoint(string id, double x, double y, double z, bool isFixed = false)
    {
        AddPoint(id, x, y, isFixed);
        AddUnknown(Z(id), z, isFixed);
        _spatial.Add(id);
    }

    /// <summary>
    /// Добавляет плоскость через три ранее добавленные точки. Нормаль плоскости (B − A) × (C − A).
    /// </summary>
    /// <param name="id">Идентификатор плоскости.</param>
    /// <param name="a">Первая точка.</param>
    /// <param name="b">Вторая точка.</param>
    /// <param name="c">Третья точка.</param>
    public void AddPlane(string id, string a, string b, string c)
    {
        EnsureNewEntity(id);
        RequirePoint(a);
        RequirePoint(b);
        RequirePoint(c);
        _planes.Add(id, (a, b, c));
    }

    /// <summary>
    /// Добавляет прямую (или отрезок) через две ранее добавленные точки.
    /// Для длины, середины и отношений отрезком считается участок между этими точками.
    /// </summary>
    /// <param name="id">Идентификатор прямой.</param>
    /// <param name="start">Первая точка.</param>
    /// <param name="end">Вторая точка.</param>
    public void AddLine(string id, string start, string end)
    {
        EnsureNewEntity(id);
        RequirePoint(start);
        RequirePoint(end);
        _lines.Add(id, (start, end));
    }

    /// <summary>
    /// Добавляет окружность с центром в ранее добавленной точке и неизвестным радиусом «id.r».
    /// </summary>
    /// <param name="id">Идентификатор окружности.</param>
    /// <param name="center">Точка-центр.</param>
    /// <param name="radius">Начальное приближение радиуса.</param>
    /// <param name="isRadiusFixed">Если true, радиус известен и не меняется решателем.</param>
    public void AddCircle(string id, string center, double radius, bool isRadiusFixed = false)
    {
        EnsureNewEntity(id);
        RequirePoint(center);
        string radiusName = id + ".r";
        AddUnknown(radiusName, radius, isRadiusFixed);
        _circles.Add(id, (center, radiusName));
    }

    /// <summary>
    /// Меняет начальное приближение неизвестной и, при необходимости, признак фиксации.
    /// Позволяет выбрать ветвь решения, задав приближение рядом с нужной.
    /// </summary>
    /// <param name="name">Имя неизвестной (например, «A.x», «c.r» или имя параметра).</param>
    /// <param name="guess">Новое начальное приближение.</param>
    /// <param name="isFixed">Новый признак фиксации; null оставляет прежний.</param>
    public void SetGuess(string name, double guess, bool? isFixed = null)
    {
        int index = IndexOf(name);
        _guesses[index] = guess;

        if (isFixed.HasValue)
            _fixed[index] = isFixed.Value;
    }

    /// <summary>
    /// Удаляет ограничение по имени.
    /// </summary>
    /// <param name="name">Имя ограничения.</param>
    /// <returns>true, если ограничение найдено и удалено.</returns>
    public bool RemoveConstraint(string name) => _constraints.RemoveAll(c => c.Name == name) > 0;

    internal static bool IsHidden(string name) => name.StartsWith(HiddenPrefix, StringComparison.Ordinal);

    internal IReadOnlyList<string> AllNames => _names;

    internal (string Start, string End) LinePoints(string line) =>
        _lines.TryGetValue(line, out var points) ? points : throw Missing("прямая", line);

    internal (string Center, string Radius) CircleParts(string circle) =>
        _circles.TryGetValue(circle, out var parts) ? parts : throw Missing("окружность", circle);

    internal (string A, string B, string C) PlanePoints(string plane) =>
        _planes.TryGetValue(plane, out var points) ? points : throw Missing("плоскость", plane);

    internal bool IsPlane(string id) => _planes.ContainsKey(id);

    internal bool IsSpatialPoint(string point) => _spatial.Contains(point);

    // Точки, задающие направление: две у прямой (направляющий вектор), три у плоскости (нормаль)
    internal string[] Corners(string id)
    {
        if (_planes.TryGetValue(id, out var plane))
            return [plane.A, plane.B, plane.C];

        var (start, end) = LinePoints(id);
        return [start, end];
    }

    internal void RequirePoint(string point)
    {
        if (!_points.Contains(point))
            throw Missing("точка", point);
    }

    internal int IndexOf(string name) =>
        _index.TryGetValue(name, out int index) ? index : throw Missing("неизвестная", name);

    internal static string X(string point) => point + ".x";

    internal static string Y(string point) => point + ".y";

    internal static string Z(string point) => point + ".z";

    // Число в ограничении хранится как скрытая фиксированная неизвестная: тогда число и параметр обрабатываются одинаково
    private string Constant(double value)
    {
        string name = HiddenPrefix + (++_constantCount).ToString(CultureInfo.InvariantCulture);
        AddUnknown(name, value, true);
        return name;
    }

    private void AddUnknown(string name, double guess, bool isFixed)
    {
        if (!double.IsFinite(guess))
            throw new ArgumentException($"Значение «{name}» должно быть конечным числом.", nameof(guess));

        if (!_index.TryAdd(name, _names.Count))
            throw new ArgumentException($"Неизвестная «{name}» уже существует.", nameof(name));

        _names.Add(name);
        _guesses.Add(guess);
        _fixed.Add(isFixed);
    }

    private void EnsureNewEntity(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (IsHidden(id) || _points.Contains(id) || _lines.ContainsKey(id) || _circles.ContainsKey(id) || _planes.ContainsKey(id))
            throw new ArgumentException($"Сущность «{id}» уже существует или имя недопустимо.", nameof(id));
    }

    private static KeyNotFoundException Missing(string what, string id) => new($"Не найдена {what} «{id}».");
}
