#nullable enable
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;
using AI.Units;

namespace AI.Physics.Continuum.Structures;

/// <summary>
/// Пространственная шарнирная ферма методом жесткостей: стержни работают только на растяжение и сжатие.
/// </summary>
/// <remarks>
/// <para>
/// Все величины в СИ: координаты в метрах, площади в м², моменты инерции в м⁴, силы в ньютонах.
/// Ось узла без жесткости (плоская ферма поперек своей плоскости) закрепляется сама, если на нее нет нагрузки;
/// изменяемая ферма не решается, а сообщается исключением <see cref="InvalidOperationException"/>.
/// </para>
/// <para>
/// Сжатый стержень проверяется на устойчивость по Эйлеру (<see cref="Beams.EulerBucklingLoad"/>) с шарнирными
/// концами: длина потери устойчивости равна длине стержня. Расчет линейный: малые перемещения, упругий материал.
/// </para>
/// </remarks>
public sealed class Truss
{
    private readonly List<(Vector3 Position, TrussSupport Support)> _nodes = [];
    private readonly List<Member> _members = [];
    private readonly Dictionary<int, Vector3> _loads = [];

    /// <summary>Число узлов</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>Число стержней</summary>
    public int MemberCount => _members.Count;

    /// <summary>Добавляет узел</summary>
    /// <param name="position">Положение узла, м</param>
    /// <param name="support">Закрепленные оси</param>
    /// <returns>Номер узла</returns>
    public int AddNode(Vector3 position, TrussSupport support = TrussSupport.None)
    {
        _nodes.Add((position, support));
        return _nodes.Count - 1;
    }

    /// <summary>Добавляет стержень</summary>
    /// <param name="from">Узел начала</param>
    /// <param name="to">Узел конца</param>
    /// <param name="material">Материал: берется модуль Юнга</param>
    /// <param name="area">Площадь сечения, м²</param>
    /// <param name="secondMoment">Наименьший момент инерции сечения, м⁴; по умолчанию сплошной круг той же площади A²/(4π)</param>
    /// <returns>Номер стержня</returns>
    public int AddMember(int from, int to, ElasticMaterial material, double area, double? secondMoment = null)
    {
        ArgumentNullException.ThrowIfNull(material);
        CheckNode(from, nameof(from));
        CheckNode(to, nameof(to));

        if (!(area > 0))
            throw new ArgumentOutOfRangeException(nameof(area), "Площадь сечения должна быть положительной");

        double inertia = secondMoment ?? (area * area / (4 * Math.PI));
        if (!(inertia > 0))
            throw new ArgumentOutOfRangeException(nameof(secondMoment), "Момент инерции должен быть положительным");

        _members.Add(new Member(from, to, area, material, inertia));
        return _members.Count - 1;
    }

    /// <summary>Добавляет сосредоточенную силу в узле; силы в одном узле складываются</summary>
    /// <param name="node">Узел</param>
    /// <param name="force">Сила, Н</param>
    public void AddLoad(int node, Vector3 force)
    {
        CheckNode(node, nameof(node));
        _loads[node] = _loads.GetValueOrDefault(node) + force;
    }

    /// <summary>Решает ферму</summary>
    /// <exception cref="InvalidOperationException">Ферма изменяема или перемещения слишком велики для линейного расчета</exception>
    public TrussSolution Solve()
    {
        int size = _nodes.Count * 3;
        var stiffness = new Matrix(size, size);
        var geometry = _members.Select(Geometry).ToList();

        for (int m = 0; m < _members.Count; m++)
        {
            (int from, int to) = (_members[m].From, _members[m].To);
            (Vector3 unit, double rigidity, _) = geometry[m];

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    double value = rigidity * unit[r] * unit[c];
                    stiffness[(3 * from) + r, (3 * from) + c] += value;
                    stiffness[(3 * to) + r, (3 * to) + c] += value;
                    stiffness[(3 * from) + r, (3 * to) + c] -= value;
                    stiffness[(3 * to) + r, (3 * from) + c] -= value;
                }
            }
        }

        var loads = new double[size];
        foreach ((int node, Vector3 force) in _loads)
        {
            for (int axis = 0; axis < 3; axis++)
                loads[(3 * node) + axis] += force[axis];
        }

        (Vector displacement, HashSet<int> fixedAxes) = StiffnessSystem.Solve(
            stiffness,
            loads,
            dof => _nodes[dof / 3].Support.HasFlag((TrussSupport)(1 << (dof % 3))),
            dof => $"в узле {dof / 3} по оси {"xyz"[dof % 3]}",
            Span(),
            _ => true,
            "ферма");

        Vector3 Moved(int node) => new(displacement[3 * node], displacement[(3 * node) + 1], displacement[(3 * node) + 2]);

        // Реакция опоры: то, что стержни не уравновесили в закрепленной оси
        double Reaction(int dof) => fixedAxes.Contains(dof)
            ? Enumerable.Range(0, size).Sum(c => stiffness[dof, c] * displacement[c]) - loads[dof]
            : 0;

        double[] forces = _members
            .Select((member, m) => geometry[m].Rigidity * geometry[m].Unit.Dot(Moved(member.To) - Moved(member.From)))
            .ToArray();
        double[] buckling = forces
            .Select((force, m) => force < 0 ? EulerLoad(_members[m], geometry[m].Length) / -force : double.PositiveInfinity)
            .ToArray();

        return new TrussSolution(
            new Vector(forces),
            new Vector(forces.Select((force, m) => force / _members[m].Area)),
            Enumerable.Range(0, _nodes.Count).Select(node => new Vector3(Reaction(3 * node), Reaction((3 * node) + 1), Reaction((3 * node) + 2))).ToList(),
            Enumerable.Range(0, _nodes.Count).Select(Moved).ToList(),
            new Vector(buckling));
    }

    /// <summary>Критическая сила Эйлера стержня с шарнирными концами, Н</summary>
    private static double EulerLoad(Member member, double length)
        => Beams.EulerBucklingLoad(
            member.Material,
            new Quantity(member.SecondMoment, Beams.SecondMomentDimension),
            new Quantity(length, Dimension.LengthDim),
            ColumnEnds.PinnedPinned).SiValue;

    private (Vector3 Unit, double Rigidity, double Length) Geometry(Member member)
    {
        Vector3 span = _nodes[member.To].Position - _nodes[member.From].Position;
        double length = span.Length;

        if (!(length > 0))
            throw new InvalidOperationException($"Стержень {member.From}-{member.To} нулевой длины");

        return (span / length, member.Area * member.Material.YoungModulus.SiValue / length, length);
    }

    /// <summary>Размер фермы: диагональ охватывающего узлы прямоугольного параллелепипеда, м</summary>
    private double Span()
    {
        if (_nodes.Count == 0)
            return 0;

        Vector3 low = _nodes.Select(node => node.Position).Aggregate(Vector3.Min);
        Vector3 high = _nodes.Select(node => node.Position).Aggregate(Vector3.Max);

        return (high - low).Length;
    }

    private void CheckNode(int node, string name)
    {
        if (node < 0 || node >= _nodes.Count)
            throw new ArgumentOutOfRangeException(name, $"Узла {node} нет");
    }

    private sealed record Member(int From, int To, double Area, ElasticMaterial Material, double SecondMoment);
}
