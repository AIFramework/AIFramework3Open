#nullable enable
using AI.Geometry.Collision;
using AI.Geometry.Hull;
using AI.Geometry.MassProperties;
using AI.Geometry.Primitives;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Что тело знает о своей форме: как поставить ее в положение тела, начальное положение центра масс, тензор инерции
/// по массе, объем, средняя площадь сечения, коэффициент сопротивления, размеры для подшага и погруженный объем.
/// Шар, коробка, капсула и цилиндр считаются по замкнутым формулам <see cref="InertiaTensor"/>, набор точек по
/// выпуклой оболочке и <see cref="MeshMass"/>, прочие формы только с тензором инерции от вызывающего
/// </summary>
internal sealed class ShapeModel
{
    /// <summary>Ячеек на ребро при подсчете погруженного объема</summary>
    private const int Cells = 8;

    /// <summary>Коэффициент сопротивления плохо обтекаемого тела, когда точного значения для формы нет</summary>
    private const double BluffDrag = 1.0;

    private readonly Vector3[] _cells;
    private readonly Vector3 _cellHalf;
    private readonly double? _sphereRadius;

    private ShapeModel(Func<Pose, IConvexShape> place, Pose start, Func<double, Matrix>? inertia, double volume, double area,
        double drag, double reach, Func<Vector3, bool>? contains, double? sphereRadius = null)
    {
        Place = place;
        Start = start;
        Inertia = inertia;
        Volume = volume;
        Area = area;
        DragCoefficient = drag;
        _sphereRadius = sphereRadius;

        // Толщина и описанный размер по опорной функции в осях тела: годится для любой выпуклой формы
        var local = place(Pose.Identity);
        var extent = double.PositiveInfinity;
        Vector3[] axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];
        var reachAxes = new double[3];
        for (var k = 0; k < 3; k++)
        {
            var plus = local.Support(axes[k]).Dot(axes[k]);
            var minus = -local.Support(-axes[k]).Dot(axes[k]);
            extent = Math.Min(extent, 0.5 * (plus + minus));
            reachAxes[k] = Math.Max(Math.Abs(plus), Math.Abs(minus));
        }

        var half = new Vector3(reachAxes[0], reachAxes[1], reachAxes[2]);
        Extent = extent;
        Reach = reach > 0 ? reach : half.Length;

        _cellHalf = half / Cells;
        var cells = new List<Vector3>();
        if (contains is not null && volume > 0)
        {
            for (var i = 0; i < Cells; i++)
            {
                for (var j = 0; j < Cells; j++)
                {
                    for (var k = 0; k < Cells; k++)
                    {
                        var center = new Vector3(
                            -half.X + (((2 * i) + 1) * _cellHalf.X),
                            -half.Y + (((2 * j) + 1) * _cellHalf.Y),
                            -half.Z + (((2 * k) + 1) * _cellHalf.Z));
                        if (contains(center))
                            cells.Add(center);
                    }
                }
            }
        }

        _cells = [.. cells];
    }

    /// <summary>Форма в положении тела</summary>
    public Func<Pose, IConvexShape> Place { get; }

    /// <summary>Начальное положение: центр масс и оси тела</summary>
    public Pose Start { get; }

    /// <summary>Тензор инерции по массе в осях тела; <c>null</c>, если замкнутой формулы нет</summary>
    public Func<double, Matrix>? Inertia { get; }

    /// <summary>Объем, м³; ноль, если неизвестен</summary>
    public double Volume { get; }

    /// <summary>Средняя площадь проекции, м²: четверть поверхности выпуклого тела (теорема Коши)</summary>
    public double Area { get; }

    /// <summary>Коэффициент сопротивления по умолчанию</summary>
    public double DragCoefficient { get; }

    /// <summary>Наименьшая полутолщина по осям тела, м</summary>
    public double Extent { get; }

    /// <summary>Радиус описанной около центра масс сферы, м</summary>
    public double Reach { get; }

    /// <summary>Модель формы; начальное положение тела берется из положения формы</summary>
    public static ShapeModel Of(IConvexShape shape) => shape switch
    {
        SphereShape sphere => Sphere(sphere),
        BoxShape box => Box(box),
        CapsuleShape capsule => Capsule(capsule),
        CylinderShape cylinder => Cylinder(cylinder),
        ConvexPointSet points => PointSet(points) ?? Generic(points),
        null => throw new ArgumentNullException(nameof(shape)),
        _ => Generic(shape),
    };

    /// <summary>
    /// Объем под горизонтальной поверхностью жидкости на высоте surface (ось z вверх), м³, и его центр в мировых осях.
    /// У шара это точный шаровой сегмент. Прочие формы режутся на ячейки, у каждой под водой доля ее высоты по
    /// мировой вертикали: у ровной коробки это точно, у наклоненной центр погруженного объема смещается к нижнему
    /// краю, и сила Архимеда дает восстанавливающий момент
    /// </summary>
    public (double Volume, Vector3 Centroid) Immersed(Pose pose, double surface)
    {
        if (_sphereRadius is { } radius)
        {
            // Шаровой сегмент высотой d: объем π·d²·(3R − d)/3, центр ниже центра шара на 3(2R − d)²/(4(3R − d))
            var depth = Math.Clamp(radius + surface - pose.Position.Z, 0, 2 * radius);
            if (depth <= 0)
                return (0, pose.Position);

            var below = 3 * Math.Pow((2 * radius) - depth, 2) / (4 * ((3 * radius) - depth));
            return (Math.PI * depth * depth * ((3 * radius) - depth) / 3, pose.Position - new Vector3(0, 0, below));
        }

        if (_cells.Length == 0)
            return (0, pose.Position);

        var (ex, ey, ez) = (pose.AxisX.Z, pose.AxisY.Z, pose.AxisZ.Z);
        var reach = Math.Max((_cellHalf.X * Math.Abs(ex)) + (_cellHalf.Y * Math.Abs(ey)) + (_cellHalf.Z * Math.Abs(ez)), 1e-300);
        var (share, weighted) = (0.0, Vector3.Zero);
        foreach (var cell in _cells)
        {
            var height = pose.Position.Z + (cell.X * ex) + (cell.Y * ey) + (cell.Z * ez);
            var wet = Math.Clamp(((surface - height) / (2 * reach)) + 0.5, 0, 1);
            share += wet;
            weighted += cell * wet;
        }

        return share <= 0 ? (0, pose.Position) : (Volume * share / _cells.Length, pose.ToWorld(weighted / share));
    }

    private static ShapeModel Sphere(SphereShape sphere)
    {
        var r = sphere.Radius;
        return new ShapeModel(
            pose => new SphereShape(pose.Position, r),
            Pose.At(sphere.Center),
            mass => InertiaTensor.Sphere(mass, r),
            4 * Math.PI * r * r * r / 3,
            Math.PI * r * r,
            0.47,
            r,
            null,
            r);
    }

    private static ShapeModel Box(BoxShape box)
    {
        var h = box.HalfExtents;
        return new ShapeModel(
            pose => new BoxShape(pose, h),
            box.Pose,
            mass => InertiaTensor.Box(mass, h),
            8 * h.X * h.Y * h.Z,
            2 * ((h.X * h.Y) + (h.Y * h.Z) + (h.Z * h.X)),
            1.05,
            h.Length,
            p => Math.Abs(p.X) <= h.X && Math.Abs(p.Y) <= h.Y && Math.Abs(p.Z) <= h.Z);
    }

    private static ShapeModel Capsule(CapsuleShape capsule)
    {
        var (half, r) = (capsule.HalfLength, capsule.Radius);
        return new ShapeModel(
            pose => new CapsuleShape(pose, half, r),
            capsule.Pose,
            mass => InertiaTensor.Capsule(mass, r, 2 * half),
            (Math.PI * r * r * 2 * half) + (4 * Math.PI * r * r * r / 3),
            (Math.PI * r * r) + (Math.PI * r * half),
            BluffDrag,
            half + r,
            p =>
            {
                var axial = p.Z - Math.Clamp(p.Z, -half, half);
                return (p.X * p.X) + (p.Y * p.Y) + (axial * axial) <= r * r;
            });
    }

    private static ShapeModel Cylinder(CylinderShape cylinder)
    {
        var (half, r) = (cylinder.HalfHeight, cylinder.Radius);
        return new ShapeModel(
            pose => new CylinderShape(pose, half, r),
            cylinder.Pose,
            mass => InertiaTensor.Cylinder(mass, r, 2 * half),
            Math.PI * r * r * 2 * half,
            Math.PI * r * (r + (2 * half)) / 2,
            BluffDrag,
            Math.Sqrt((half * half) + (r * r)),
            p => (p.X * p.X) + (p.Y * p.Y) <= r * r && Math.Abs(p.Z) <= half);
    }

    /// <summary>
    /// Набор точек: выпуклая оболочка дает объем, центр масс и тензор инерции через <see cref="MeshMass"/>; центр масс
    /// становится началом осей тела. <c>null</c>, если оболочка плоская
    /// </summary>
    private static ShapeModel? PointSet(ConvexPointSet points)
    {
        ConvexHull3D hull;
        try
        {
            hull = ConvexHull3D.Build(points.Points);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var solid = MeshMass.Compute(hull.Vertices, hull.Triangles);
        var centroid = solid.Centroid;
        var shifted = points.Points.Select(point => point - centroid).ToArray();
        var surface = hull.Triangles.Sum(t => (hull.Vertices[t.B] - hull.Vertices[t.A]).Cross(hull.Vertices[t.C] - hull.Vertices[t.A]).Length / 2);
        var start = points.Pose with { Position = points.Pose.ToWorld(centroid) };
        return new ShapeModel(
            pose => new ConvexPointSet(pose, shifted),
            start,
            solid.InertiaForMass,
            solid.Volume,
            surface / 4,
            BluffDrag,
            shifted.Max(point => point.Length),
            p => hull.Contains(p + centroid, 1e-12 * (1 + centroid.Length)));
    }

    /// <summary>Любая другая форма: центр масс в ее центре, тензор инерции задает вызывающий</summary>
    private static ShapeModel Generic(IConvexShape shape)
    {
        var origin = shape.Center;
        return new ShapeModel(pose => new PlacedShape(shape, origin, pose), Pose.At(origin), null, 0, 0, 0, 0, null);
    }
}
