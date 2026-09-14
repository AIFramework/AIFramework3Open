using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.Solvers.Pde;
using AI.Solvers.Pde.FiniteDifference;
using AI.Solvers.Pde.FiniteElement;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>pde</c>: уравнения в частных производных.
/// </summary>
/// <remarks>
/// Коэффициенты, источники и граничные условия — функции скрипта: <c>x =&gt; ...</c> на отрезке,
/// <c>(x, y) =&gt; ...</c> на плоскости, <c>(x, t) =&gt; ...</c> для источника во времени. Так
/// задача записывается формулой из постановки, а не таблицей значений. Область — векторы из
/// двух чисел <c>x: &lt;0, 1&gt;</c>, <c>y: &lt;0, 1&gt;</c>, одинаково на отрезке и на плоскости;
/// по умолчанию — единичная.
/// <para>
/// Решение на прямоугольнике — матрица, где строка — значение <c>y</c>, столбец — <c>x</c>: её сразу
/// рисует <c>plot.heatmap</c>. На треугольной сетке — вектор по узлам рядом с их координатами.
/// Разреженные матрицы и итерационные решатели библиотеки наружу не вынесены: в языке нет
/// разреженного типа, а для задач прототипа хватает <c>mat.solve</c>.
/// </para>
/// </remarks>
[ScriptModule("pde", "Частные производные: тепло, волна, Пуассон, упругость, каверна", Version = "0.1")]
public static class PdeModule
{
    // --- на отрезке ---

    /// <summary>
    /// Уравнение теплопроводности на отрезке.
    /// </summary>
    /// <remarks>
    /// Схема Кранка — Николсон устойчива при любом шаге; явная быстрее, но требует
    /// <c>Δt ≤ h²/(2α)</c>, и при нарушении поле <c>stable</c> — ложь, а в <c>warnings</c> — почему.
    /// Граница по умолчанию держит ноль.
    /// </remarks>
    [ScriptFn("heat", "Теплопроводность на отрезке: распределение к моменту until",
        Example = "let rod = pde.heat(x => math.sin(pi * x), diffusivity: 0.1, until: 0.5)")]
    public static ScriptRecord Heat(
        IScriptContext context,
        [ScriptParam("начальное распределение u(x)")] ScriptCallable initial,
        [ScriptParam("температуропроводность α")] double diffusivity,
        [ScriptParam("конечное время")] double until,
        [ScriptParam("отрезок <from, to>; по умолчанию <0, 1>")] Vector? x = null,
        [ScriptParam("узлов по пространству")] int nodes = 51,
        [ScriptParam("шагов по времени")] int steps = 200,
        [ScriptParam("значение на левом конце от времени t; по умолчанию 0")] ScriptCallable? left = null,
        [ScriptParam("значение на правом конце от времени t; по умолчанию 0")] ScriptCallable? right = null,
        [ScriptParam("источник f(x, t)")] ScriptCallable? source = null,
        [ScriptParam("схема: \"crank_nicolson\" либо \"explicit\"")] string kind = "crank_nicolson")
    {
        const string function = "pde.heat";

        TimeScheme scheme = ScriptData.Kind<TimeScheme>(kind, "kind", function,
            ("crank_nicolson", TimeScheme.CrankNicolson),
            ("explicit", TimeScheme.Explicit));

        ScriptData.Require(diffusivity > 0, $"{function}: температуропроводность должна быть положительной");
        RequireTime(until, steps, function);

        Grid1D grid = Line(x, nodes, function);

        context.CountAllocation((long)nodes * 4);

        HeatSolution solution = HeatEquation1D.Solve(
            grid, diffusivity,
            Of(context, initial, $"{function}: начальное распределение"),
            OfOrZero(context, left, $"{function}: левая граница"),
            OfOrZero(context, right, $"{function}: правая граница"),
            until, steps, scheme,
            source == null ? null : Of2(context, source, $"{function}: источник"));

        return ScriptData.Record(solution,
            ("x", grid.Nodes()),
            ("u", solution.Values),
            ("steps", solution.Steps),
            ("courant", solution.Courant),
            ("stable", solution.IsStable),
            ("kind", kind));
    }

    /// <summary>
    /// Волновое уравнение на отрезке с закреплёнными концами.
    /// </summary>
    /// <remarks>
    /// Явная схема устойчива при числе Куранта не больше единицы: <c>c·Δt ≤ h</c>. Поле
    /// <c>courant</c> показывает, насколько близко к краю; <c>previous</c> — слой на шаг раньше,
    /// по нему и <c>u</c> видна скорость струны в конечный момент.
    /// </remarks>
    [ScriptFn("wave", "Колебания струны: смещение к моменту until при закреплённых концах",
        Example = "let chord = pde.wave(x => math.sin(pi * x), speed: 1, until: 0.5)")]
    public static ScriptRecord Wave(
        IScriptContext context,
        [ScriptParam("начальное смещение u(x)")] ScriptCallable initial,
        [ScriptParam("скорость распространения c")] double speed,
        [ScriptParam("конечное время")] double until,
        [ScriptParam("отрезок <from, to>; по умолчанию <0, 1>")] Vector? x = null,
        [ScriptParam("узлов по пространству")] int nodes = 101,
        [ScriptParam("шагов по времени")] int steps = 200,
        [ScriptParam("начальная скорость v(x); по умолчанию 0")] ScriptCallable? velocity = null)
    {
        const string function = "pde.wave";

        ScriptData.Require(speed > 0, $"{function}: скорость должна быть положительной");
        RequireTime(until, steps, function);

        Grid1D grid = Line(x, nodes, function);

        context.CountAllocation((long)nodes * 4);

        WaveSolution solution = WaveEquation1D.Solve(
            grid, speed,
            Of(context, initial, $"{function}: начальное смещение"),
            velocity == null ? null : Of(context, velocity, $"{function}: начальная скорость"),
            until, steps);

        return ScriptData.Record(solution,
            ("x", grid.Nodes()),
            ("u", solution.Values),
            ("previous", solution.Previous),
            ("steps", solution.Steps),
            ("courant", solution.Courant),
            ("stable", solution.IsStable));
    }

    /// <summary>
    /// Стационарная диффузия с реакцией на отрезке методом конечных элементов.
    /// </summary>
    /// <remarks>
    /// Решает <c>−(k·u′)′ + c·u = f</c> с переменными коэффициентами. Граничное условие — запись:
    /// <c>{ value: 0 }</c> — задано значение, <c>{ flux: 2 }</c> — задан поток. На отрезке
    /// метод точен в узлах при постоянных коэффициентах, поэтому им удобно проверять и другие.
    /// </remarks>
    [ScriptFn("steady", "Стационарная диффузия с реакцией на отрезке: −(k·u′)′ + c·u = f",
        Example = "let u = pde.steady(x => 1, left: { value: 0 }, right: { flux: 2 })")]
    public static ScriptRecord Steady(
        IScriptContext context,
        [ScriptParam("источник f(x)")] ScriptCallable source,
        [ScriptParam("проводимость k(x); по умолчанию 1")] ScriptCallable? conductivity = null,
        [ScriptParam("коэффициент реакции c(x); по умолчанию 0")] ScriptCallable? reaction = null,
        [ScriptParam("отрезок <from, to>; по умолчанию <0, 1>")] Vector? x = null,
        [ScriptParam("узлов сетки")] int nodes = 51,
        [ScriptParam("левое условие: { value: … } либо { flux: … }; по умолчанию { value: 0 }")] ScriptRecord? left = null,
        [ScriptParam("правое условие: { value: … } либо { flux: … }; по умолчанию { value: 0 }")] ScriptRecord? right = null)
    {
        const string function = "pde.steady";

        Grid1D grid = Line(x, nodes, function);

        Fem1DSolution solution = Fem1D.Solve(
            grid,
            conductivity == null ? _ => 1 : Of(context, conductivity, $"{function}: проводимость"),
            OfOrZero(context, reaction, $"{function}: реакция"),
            Of(context, source, $"{function}: источник"),
            Condition(left, "left", function),
            Condition(right, "right", function));

        return ScriptData.Record(solution,
            ("x", grid.Nodes()),
            ("u", solution.Values),
            ("iterations", solution.Iterations),
            ("converged", solution.Converged));
    }

    // --- на плоскости ---

    /// <summary>
    /// Уравнение Пуассона в прямоугольнике конечными разностями.
    /// </summary>
    /// <remarks>
    /// Знак — как в постановках: <c>−Δu = f</c>, так что положительный источник даёт бугор.
    /// Без источника это уравнение Лапласа, и поле целиком определяется границей.
    /// </remarks>
    [ScriptFn("poisson", "Уравнение Пуассона −Δu = f в прямоугольнике по источнику и границе",
        Example = "let field = pde.poisson(source: (x, y) => 1, nodes: 41)")]
    public static ScriptRecord Poisson(
        IScriptContext context,
        [ScriptParam("источник f(x, y); без него — уравнение Лапласа")] ScriptCallable? source = null,
        [ScriptParam("значение на границе g(x, y); по умолчанию 0")] ScriptCallable? boundary = null,
        [ScriptParam("отрезок по x; по умолчанию <0, 1>")] Vector? x = null,
        [ScriptParam("отрезок по y; по умолчанию <0, 1>")] Vector? y = null,
        [ScriptParam("узлов по каждой стороне")] int nodes = 41)
    {
        const string function = "pde.poisson";

        if (source == null && boundary == null)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: без источника и границы решение тождественно ноль",
                "задайте source: (x, y) => … либо boundary: (x, y) => …");
        }

        Grid2D grid = Plane(x, y, nodes, function);

        context.CountAllocation((long)grid.NodeCount * 4);

        Func<double, double, double> edge = boundary == null ? (_, _) => 0 : Of2(context, boundary, $"{function}: граница");

        PoissonSolution solution = source == null
            ? Poisson2D.SolveLaplace(grid, edge)
            : Poisson2D.Solve(grid, Of2(context, source, $"{function}: источник"), edge);

        return ScriptData.Record(solution,
            ("u", solution.Values),
            ("x", Axis(grid.Left, grid.Right, grid.CountX)),
            ("y", Axis(grid.Bottom, grid.Top, grid.CountY)),
            ("iterations", solution.Iterations),
            ("residual", solution.Residual),
            ("converged", solution.Converged));
    }

    /// <summary>
    /// Треугольная сетка.
    /// </summary>
    /// <remarks>
    /// Сетка — запись: координаты узлов <c>x</c>, <c>y</c>, треугольники матрицей из троек номеров
    /// узлов и признак граничного узла. Её можно нарисовать <c>plot.scatter</c> и передать в
    /// <c>pde.poisson_mesh</c> или <c>pde.elasticity</c>. Параметр <c>radius</c> — внутренний радиус
    /// кольца либо радиус отверстия, <c>size</c> — внешний радиус кольца либо половина стороны
    /// пластины; для прямоугольника нужны <c>x</c>, <c>y</c> и <c>nodes</c>.
    /// </remarks>
    [ScriptFn("mesh", "Треугольная сетка: прямоугольник, четверть кольца, пластина с отверстием",
        Example = "let plate = pde.mesh(kind: \"plate_with_hole\", radius: 1, size: 10)")]
    public static ScriptRecord Mesh(
        IScriptContext context,
        [ScriptParam("форма: rectangle, annulus, plate_with_hole")] string kind = "rectangle",
        [ScriptParam("прямоугольник: отрезок по x")] Vector? x = null,
        [ScriptParam("прямоугольник: отрезок по y")] Vector? y = null,
        [ScriptParam("прямоугольник: узлов по стороне")] int nodes = 11,
        [ScriptParam("внутренний радиус кольца либо радиус отверстия")] double radius = 1,
        [ScriptParam("внешний радиус кольца либо половина стороны пластины")] double size = 2,
        [ScriptParam("элементов по радиусу")] int radial = 8,
        [ScriptParam("элементов по углу; для пластины чётное")] int angular = 16,
        [ScriptParam("пластина: отношение соседних элементов по радиусу, 1 — равномерно")] double grading = 1)
    {
        const string function = "pde.mesh";

        TriangularMesh mesh = ScriptData.Kind<Func<TriangularMesh>>(kind, "kind", function,
            ("rectangle", () => TriangularMesh.Rectangle(Plane(x, y, nodes, function))),
            ("annulus", () => TriangularMesh.QuarterAnnulus(radius, size, radial, angular)),
            ("plate_with_hole", () => TriangularMesh.QuarterPlateWithHole(radius, size, radial, angular, grading)))();

        context.CountAllocation((long)mesh.NodeCount * 3 + ((long)mesh.TriangleCount * 3));

        return MeshRecord(mesh);
    }

    /// <summary>
    /// Уравнение Пуассона на треугольной сетке методом конечных элементов.
    /// </summary>
    /// <remarks>
    /// Для областей, где конечные разности не годятся: кольцо, пластина с отверстием, любая
    /// сетка, собранная вручную. Решение — вектор по узлам сетки.
    /// </remarks>
    [ScriptFn("poisson_mesh", "Уравнение Пуассона на треугольной сетке: кольцо, пластина с отверстием",
        Example = "let field = pde.poisson_mesh(ring, source: (x, y) => 1)")]
    public static ScriptRecord PoissonMesh(
        IScriptContext context,
        [ScriptParam("сетка из pde.mesh")] ScriptRecord mesh,
        [ScriptParam("источник f(x, y); по умолчанию 0")] ScriptCallable? source = null,
        [ScriptParam("значение на границе g(x, y); по умолчанию 0")] ScriptCallable? boundary = null)
    {
        const string function = "pde.poisson_mesh";

        TriangularMesh triangles = MeshOf(mesh, function);

        Fem2DSolution solution = Fem2D.SolvePoisson(
            triangles,
            source == null ? (_, _) => 0 : Of2(context, source, $"{function}: источник"),
            boundary == null ? (_, _) => 0 : Of2(context, boundary, $"{function}: граница"));

        return ScriptData.Record(solution,
            ("u", solution.Values),
            ("x", Coordinates(triangles, triangles.X)),
            ("y", Coordinates(triangles, triangles.Y)),
            ("iterations", solution.Iterations),
            ("converged", solution.Converged));
    }

    /// <summary>
    /// Плоская задача теории упругости.
    /// </summary>
    /// <remarks>
    /// Закрепление и нагрузки — записи с условием на координаты: <c>{ where: (x, y) =&gt; ..., x: true,
    /// y: false }</c> закрепляет по x узлы, где условие истинно; <c>{ where, tx, ty }</c> — растяжение
    /// или сдвиг границы, <c>{ where, value }</c> — давление по нормали. Координаты узлов на границе
    /// лежат не точно, поэтому условие пишут через <c>math.approx</c>, а не <c>==</c>. Тело должно
    /// быть закреплено от сдвига и поворота целиком, иначе решения нет — это отказ, а не ноль.
    /// </remarks>
    [ScriptFn("elasticity", "Плоская упругость: перемещения и напряжения тела под нагрузкой",
        Example = "let plate = pde.elasticity(grid, young: 70e9, poisson_ratio: 0.33, fixed: [{ where: (x, y) => math.approx(x, 0), x: true, y: true }], traction: [{ where: (x, y) => math.approx(x, 1), tx: 1e6, ty: 0 }])")]
    public static ScriptRecord Elasticity(
        IScriptContext context,
        [ScriptParam("сетка из pde.mesh")] ScriptRecord mesh,
        [ScriptParam("модуль Юнга, Па")] double young,
        [ScriptParam("коэффициент Пуассона, от 0 до 0.5")] double poisson_ratio,
        [ScriptParam("закрепления: записи { where, x, y }")] ScriptList @fixed,
        [ScriptParam("распределённые силы на границе: записи { where, tx, ty }")] ScriptList? traction = null,
        [ScriptParam("давление по нормали: записи { where, value }")] ScriptList? pressure = null,
        [ScriptParam("состояние: \"plane_stress\" — тонкая пластина, \"plane_strain\" — длинное тело")] string kind = "plane_stress",
        [ScriptParam("толщина пластины")] double thickness = 1)
    {
        const string function = "pde.elasticity";

        PlaneCondition condition = ScriptData.Kind<PlaneCondition>(kind, "kind", function,
            ("plane_stress", PlaneCondition.PlaneStress),
            ("plane_strain", PlaneCondition.PlaneStrain));

        ScriptData.Require(young > 0, $"{function}: модуль Юнга должен быть положительным");
        ScriptData.Require(poisson_ratio is >= 0 and < 0.5, $"{function}: коэффициент Пуассона лежит в [0, 0.5)");
        ScriptData.Require(thickness > 0, $"{function}: толщина должна быть положительной");

        TriangularMesh triangles = MeshOf(mesh, function);
        var problem = new ElasticityProblem(triangles, young, poisson_ratio, condition, thickness);

        foreach ((ScriptRecord record, int index) in Items(@fixed, "fixed", function))
        {
            RequireFields(record, index, "fixed", function, "where", "x", "y");

            problem = problem.FixWhere(
                Where(context, record, index, "fixed", function),
                Flag(record, "x"),
                Flag(record, "y"));
        }

        foreach ((ScriptRecord record, int index) in Items(traction, "traction", function))
        {
            RequireFields(record, index, "traction", function, "where", "tx", "ty");

            double tx = Number(record, "tx", 0), ty = Number(record, "ty", 0);

            problem = problem.AddTraction(Where(context, record, index, "traction", function), (_, _) => (tx, ty));
        }

        foreach ((ScriptRecord record, int index) in Items(pressure, "pressure", function))
        {
            RequireFields(record, index, "pressure", function, "where", "value");

            problem = problem.AddPressure(Where(context, record, index, "pressure", function), Number(record, "value", 0));
        }

        ElasticitySolution solution = problem.Solve();
        var nodes = Enumerable.Range(0, triangles.NodeCount).ToArray();
        (double reactionX, double reactionY) = solution.TotalReaction;
        (double loadX, double loadY) = solution.TotalLoad;

        return ScriptData.Record(solution,
            ("max_displacement", solution.MaxDisplacement),
            ("max_von_mises", solution.MaxVonMises),
            ("strain_energy", solution.StrainEnergy),
            ("load_x", loadX),
            ("load_y", loadY),
            ("reaction_x", reactionX),
            ("reaction_y", reactionY),
            ("nodes", ScriptData.Table(nodes,
                ("x", n => triangles.X(n)),
                ("y", n => triangles.Y(n)),
                ("ux", n => solution.DisplacementX(n)),
                ("uy", n => solution.DisplacementY(n)),
                ("sxx", n => solution.NodalStress(n).Xx),
                ("syy", n => solution.NodalStress(n).Yy),
                ("sxy", n => solution.NodalStress(n).Xy))),
            ("iterations", solution.Iterations),
            ("converged", solution.Converged));
    }

    /// <summary>
    /// Течение в квадратной каверне с подвижной крышкой.
    /// </summary>
    /// <remarks>
    /// Классическая проверочная задача вязкой жидкости: крышка едет с единичной скоростью, и в
    /// каверне закручивается главный вихрь. Поля — матрицы, строка — y, столбец — x; положение
    /// вихря сверяют с эталоном Гиа. Число Рейнольдса ячейки <c>cell_reynolds</c> больше двух
    /// означает, что сетка для такого течения груба.
    /// </remarks>
    [ScriptFn("cavity", "Течение в каверне с подвижной крышкой: функция тока, вихрь, скорости",
        Example = "let flow = pde.cavity(100, nodes: 41)")]
    public static ScriptRecord Cavity(
        IScriptContext context,
        [ScriptParam("число Рейнольдса")] double reynolds,
        [ScriptParam("узлов по стороне")] int nodes = 65,
        [ScriptParam("порог установления")] double tolerance = 1e-6,
        [ScriptParam("предельное время установления")] double max_time = 200)
    {
        const string function = "pde.cavity";

        ScriptData.Require(reynolds > 0, $"{function}: число Рейнольдса должно быть положительным");
        ScriptData.Require(nodes is >= 5 and <= 257, $"{function}: узлов по стороне — от 5 до 257");

        context.CountAllocation((long)nodes * nodes * 4);

        CavityFlowSolution flow = LidDrivenCavity.Solve(reynolds, nodes, tolerance, max_time);
        (double vortexX, double vortexY, double vortexPsi) = flow.PrimaryVortex;

        return ScriptData.Record(flow,
            ("psi", flow.StreamFunction),
            ("vorticity", flow.Vorticity),
            ("u", flow.VelocityX),
            ("v", flow.VelocityY),
            ("vortex_x", vortexX),
            ("vortex_y", vortexY),
            ("vortex_psi", vortexPsi),
            ("cell_reynolds", flow.CellReynolds),
            ("steps", flow.Steps),
            ("time", flow.Time),
            ("residual", flow.Residual),
            ("converged", flow.Converged));
    }

    // --- внутреннее ---

    private static Func<double, double> Of(IScriptContext context, ScriptCallable f, string what) =>
        value => ScriptCallbacks.Number(context, f, what, value);

    private static Func<double, double> OfOrZero(IScriptContext context, ScriptCallable? f, string what) =>
        f == null ? _ => 0 : Of(context, f, what);

    private static Func<double, double, double> Of2(IScriptContext context, ScriptCallable f, string what) =>
        (a, b) => ScriptCallbacks.Number(context, f, what, a, b);

    private static void RequireTime(double until, int steps, string function)
    {
        ScriptData.Require(until > 0, $"{function}: конечное время должно быть положительным");
        ScriptData.Require(steps >= 1, $"{function}: шагов по времени — хотя бы один");
    }

    /// <summary>Отрезок из вектора двух чисел; по умолчанию единичный.</summary>
    private static (double From, double To) Span(Vector? range, string name, string function)
    {
        if (range == null) return (0, 1);

        if (range.Count != 2 || !(range[1] > range[0]))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: {name} — не отрезок",
                $"отрезок — два числа по возрастанию: {name}: <0, 1>");
        }

        return (range[0], range[1]);
    }

    private static Grid1D Line(Vector? x, int nodes, string function)
    {
        ScriptData.Require(nodes is >= 3 and <= 1_000_000, $"{function}: узлов — от 3 до миллиона");

        (double from, double to) = Span(x, "x", function);

        return new Grid1D(from, to, nodes);
    }

    private static Grid2D Plane(Vector? x, Vector? y, int nodes, string function)
    {
        ScriptData.Require(nodes is >= 3 and <= 2000, $"{function}: узлов по стороне — от 3 до двух тысяч");

        (double left, double right) = Span(x, "x", function);
        (double bottom, double top) = Span(y, "y", function);

        return new Grid2D(left, right, bottom, top, nodes, nodes);
    }

    private static Vector Axis(double from, double to, int count) => new Grid1D(from, to, count).Nodes();

    private static BoundaryCondition Condition(ScriptRecord? record, string side, string function)
    {
        if (record == null) return BoundaryCondition.Fixed(0);

        if (record.Count == 1 && record.TryGet("value", out ScriptValue value))
            return BoundaryCondition.Fixed(value.AsNumber($"{side}.value"));

        if (record.Count == 1 && record.TryGet("flux", out ScriptValue flux))
            return BoundaryCondition.Flux(flux.AsNumber($"{side}.flux"));

        throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{function}: условие {side} — ни значение, ни поток",
            $"условие — запись с одним полем: {side}: {{ value: 0 }} либо {side}: {{ flux: 1 }}");
    }

    private static ScriptRecord MeshRecord(TriangularMesh mesh)
    {
        var triangles = new Matrix(mesh.TriangleCount, 3);

        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            IReadOnlyList<int> corners = mesh.Triangle(t);

            for (int k = 0; k < 3; k++) triangles[t, k] = corners[k];
        }

        var boundary = new Vector(mesh.NodeCount);

        for (int n = 0; n < mesh.NodeCount; n++) boundary[n] = mesh.IsBoundary(n) ? 1 : 0;

        return ScriptData.Plain(
            ("x", Coordinates(mesh, mesh.X)),
            ("y", Coordinates(mesh, mesh.Y)),
            ("triangles", triangles),
            ("boundary", boundary),
            ("nodes", mesh.NodeCount),
            ("elements", mesh.TriangleCount));
    }

    private static Vector Coordinates(TriangularMesh mesh, Func<int, double> coordinate)
    {
        var values = new Vector(mesh.NodeCount);

        for (int n = 0; n < mesh.NodeCount; n++) values[n] = coordinate(n);

        return values;
    }

    /// <summary>Сетка из записи: координаты узлов и тройки номеров узлов.</summary>
    private static TriangularMesh MeshOf(ScriptRecord mesh, string function)
    {
        if (!mesh.TryGet("x", out ScriptValue x) || !mesh.TryGet("y", out ScriptValue y) || !mesh.TryGet("triangles", out ScriptValue t))
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{function}: запись не похожа на сетку",
                "сетку даёт pde.mesh: поля x, y и triangles");
        }

        Matrix corners = t.AsMatrix("triangles");

        ScriptData.Require(corners.Width == 3, $"{function}: треугольник — три номера узла, а в строке {corners.Width}");

        var triangles = new List<IReadOnlyList<int>>(corners.Height);

        for (int row = 0; row < corners.Height; row++)
        {
            var triangle = new int[3];

            for (int k = 0; k < 3; k++)
            {
                double node = corners[row, k];

                ScriptData.Require(node == Math.Round(node) && node >= 0, $"{function}: номер узла {node} в треугольнике {row}");
                triangle[k] = (int)node;
            }

            triangles.Add(triangle);
        }

        return TriangularMesh.Create(x.AsVector("x"), y.AsVector("y"), triangles);
    }

    private static IEnumerable<(ScriptRecord Record, int Index)> Items(ScriptList? list, string parameter, string function)
    {
        if (list == null) yield break;

        for (int i = 0; i < list.Count; i++) yield return (list[i].AsRecord($"{function}: {parameter}[{i}]"), i);
    }

    private static void RequireFields(ScriptRecord record, int index, string parameter, string function, params string[] allowed)
    {
        foreach (string key in record.Keys)
        {
            if (allowed.Contains(key)) continue;

            throw new ScriptError(
                DiagnosticCodes.UnknownArgument,
                $"{function}: в {parameter}[{index}] поле '{key}'",
                $"поля: {string.Join(", ", allowed)}");
        }

        ScriptData.Require(record.Has("where"), $"{function}: у {parameter}[{index}] нет условия where");
    }

    private static Func<double, double, bool> Where(
        IScriptContext context, ScriptRecord record, int index, string parameter, string function)
    {
        _ = record.TryGet("where", out ScriptValue value);

        ScriptCallable where = value.AsCallable($"{parameter}[{index}].where");

        return (px, py) => ScriptCallbacks.Invoke(context, where, ScriptValue.Num(px), ScriptValue.Num(py))
            .AsBool($"{function}: условие {parameter}[{index}].where");
    }

    private static bool Flag(ScriptRecord record, string name) =>
        record.TryGet(name, out ScriptValue value) && value.AsBool(name);

    private static double Number(ScriptRecord record, string name, double fallback) =>
        record.TryGet(name, out ScriptValue value) ? value.AsNumber(name) : fallback;
}
