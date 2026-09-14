#nullable enable
using AI.Geometry.Collision;
using AI.Geometry.MassProperties;
using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Matrix = AI.DataStructs.Algebraic.Matrix;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Твердое тело с шестью степенями свободы: положение и скорость центра масс, ориентация кватернионом и угловая
/// скорость в мировых осях. Форма это любая выпуклая <see cref="IConvexShape"/> из AI.Geometry.Collision, либо
/// бесконечная плоскость (<see cref="Ground(Plane)"/>) как неподвижная земля или стена.
/// </summary>
/// <remarks>
/// <para>
/// Все величины в СИ: метры, килограммы, секунды, радианы; сила в ньютонах, импульс в Н·с. Тело без массы
/// неподвижно: не отвечает на силы и удары; если у него задана скорость, оно движется по ней, как подвижная платформа.
/// </para>
/// <para>
/// Тело берет положение из формы: центр шара, положение <see cref="Pose"/> коробки, капсулы, цилиндра; у набора точек
/// центр масс его выпуклой оболочки. Тензор инерции шара, коробки, капсулы и цилиндра считает
/// <see cref="InertiaTensor"/>, набора точек <see cref="MeshMass"/> по выпуклой оболочке; для прочих форм его
/// передает вызывающий, например <see cref="SolidProperties.InertiaForMass"/>.
/// </para>
/// <para>
/// Это горячий цикл движка, поэтому тело хранит состояние числами СИ и значимым <see cref="Vector3"/>, а не
/// <see cref="Units.Quantity"/>: так же рекомендует документация AI.Geometry для массовых расчетов.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var ball = new Body(new SphereShape(new Vector3(0, 0, 2), 0.1), 1) { Restitution = 0.8 };
/// </code>
/// </example>
public sealed class Body
{
    private static readonly Vector3 UnitX = new(1, 0, 0);
    private static readonly Vector3 UnitY = new(0, 1, 0);
    private static readonly Vector3 UnitZ = new(0, 0, 1);

    private readonly ShapeModel? _model;
    private readonly SymmetricMatrix3 _inertia;
    private readonly SymmetricMatrix3 _inverseInertia;
    private SymmetricMatrix3 _inverseInertiaWorld;
    private Vector3 _position;
    private Quaternion _orientation = Quaternion.Identity;
    private Vector3 _velocity;
    private Vector3 _angularVelocity;
    private Vector3 _force;
    private Vector3 _torque;
    private IConvexShape? _shape;

    /// <summary>Тело из выпуклой формы; тензор инерции по форме однородного тела</summary>
    /// <param name="shape">Форма в начальном положении тела</param>
    /// <param name="mass">Масса, кг; ноль означает неподвижное тело</param>
    /// <exception cref="ArgumentException">Для формы нет формулы тензора инерции: нужен конструктор с тензором</exception>
    public Body(IConvexShape shape, double mass)
        : this(ShapeModel.Of(shape), mass, null)
    {
    }

    /// <summary>Тело из выпуклой формы с заданным тензором инерции</summary>
    /// <param name="shape">Форма в начальном положении тела; ее центр совпадает с центром масс</param>
    /// <param name="mass">Масса, кг; ноль означает неподвижное тело</param>
    /// <param name="inertia">Тензор инерции 3×3 относительно центра масс в осях формы, кг·м²</param>
    public Body(IConvexShape shape, double mass, Matrix inertia)
        : this(ShapeModel.Of(shape), mass, inertia ?? throw new ArgumentNullException(nameof(inertia)))
    {
    }

    private Body(ShapeModel model, double mass, Matrix? inertia)
    {
        if (!(mass >= 0) || double.IsInfinity(mass))
            throw new ArgumentOutOfRangeException(nameof(mass), "Масса тела должна быть неотрицательным числом");

        _model = model;
        Mass = mass;
        InverseMass = mass > 0 ? 1 / mass : 0;
        if (mass > 0)
        {
            var tensor = inertia ?? model.Inertia?.Invoke(mass)
                ?? throw new ArgumentException("Для этой формы нет формулы тензора инерции: передайте его явно", nameof(inertia));
            if (tensor.Height != 3 || tensor.Width != 3)
                throw new ArgumentException("Тензор инерции должен быть матрицей 3×3", nameof(inertia));

            var (moments, axes) = InertiaTensor.Principal(tensor);
            if (!(moments.X > 0))
                throw new ArgumentException("Тензор инерции должен быть положительно определен", nameof(inertia));

            _inertia = SymmetricMatrix3.FromMatrix(tensor);
            var inverse = InertiaTensor.FromDiagonal(new Vector3(1 / moments.X, 1 / moments.Y, 1 / moments.Z));
            _inverseInertia = SymmetricMatrix3.FromMatrix(InertiaTensor.Rotate(inverse, axes));
        }

        _position = model.Start.Position;
        _orientation = OrientationOf(model.Start);
        _inverseInertiaWorld = _inverseInertia.Rotated(_orientation);
        Volume = model.Volume;
        Area = model.Area;
        DragCoefficient = model.DragCoefficient;
        Extent = model.Extent;
        Reach = model.Reach;
    }

    private Body(Plane plane)
    {
        var normal = Vector3.FromVector(plane.Normal);
        var length = normal.Length;
        if (!(length > 0) || !double.IsFinite(length) || !double.IsFinite(plane.D))
            throw new ArgumentException("Нормаль плоскости должна быть ненулевой", nameof(plane));

        HalfSpace = plane;
        PlaneNormal = normal / length;
        PlaneOffset = plane.D / length;
        _position = PlaneNormal * -PlaneOffset;
        Restitution = 1;
        Extent = double.PositiveInfinity;
        Reach = double.PositiveInfinity;
    }

    /// <summary>
    /// Выпуклая форма в нынешнем положении тела; <c>null</c> у плоскости
    /// </summary>
    public IConvexShape? Shape => _model is null ? null : _shape ??= _model.Place(Pose);

    /// <summary>
    /// Плоскость земли или стены: твердое полупространство лежит против ее нормали; <c>null</c> у тела с формой
    /// </summary>
    public Plane? HalfSpace { get; }

    /// <summary>Центр масс и оси тела в мировых осях</summary>
    public Pose Pose => new(_position, _orientation.Rotate(UnitX), _orientation.Rotate(UnitY), _orientation.Rotate(UnitZ));

    /// <summary>Масса, кг</summary>
    public double Mass { get; }

    /// <summary>Обратная масса, 1/кг; ноль у неподвижного тела</summary>
    public double InverseMass { get; }

    /// <summary>Объем, м³: для силы Архимеда; ноль, если форма его не знает</summary>
    public double Volume { get; }

    /// <summary>Центр масс, м. Присваивание будит тело</summary>
    public Vector3 Position
    {
        get => _position;
        set
        {
            RequireShape();
            _position = value;
            Moved();
            Wake();
        }
    }

    /// <summary>Ориентация: поворот из осей тела в мировые. Присваивание нормирует кватернион и будит тело</summary>
    public Quaternion Orientation
    {
        get => _orientation;
        set
        {
            RequireShape();
            _orientation = value.Normalize;
            Rotated();
            Wake();
        }
    }

    /// <summary>Скорость центра масс, м/с. Присваивание будит тело</summary>
    public Vector3 Velocity
    {
        get => _velocity;
        set
        {
            RequireShape();
            _velocity = value;
            Wake();
        }
    }

    /// <summary>Угловая скорость в мировых осях, рад/с. Присваивание будит тело</summary>
    public Vector3 AngularVelocity
    {
        get => _angularVelocity;
        set
        {
            RequireShape();
            _angularVelocity = value;
            Wake();
        }
    }

    /// <summary>
    /// Коэффициент восстановления: 0 удар без отскока, 1 упругий. У пары берется меньший: мягкое тело гасит удар
    /// </summary>
    public double Restitution { get; set; } = 0.3;

    /// <summary>Коэффициент трения по Кулону; у пары среднее геометрическое</summary>
    public double Friction { get; set; } = 0.5;

    /// <summary>
    /// Сопротивление качению и верчению, м: плечо, на которое реакция опоры смещается под катящимся телом. У твердого
    /// шара на твердом полу доли миллиметра, у колеса на грунте сантиметры; по умолчанию качение без потерь. У пары
    /// берется большее
    /// </summary>
    public double RollingResistance { get; set; }

    /// <summary>Коэффициент сопротивления среды: у шара 0.47, у кубика 1.05, у прочих форм 1</summary>
    public double DragCoefficient { get; set; }

    /// <summary>Площадь сечения для сопротивления среды, м²: по умолчанию средняя проекция, четверть поверхности</summary>
    public double Area { get; set; }

    /// <summary>Неподвижное тело: масса ноль</summary>
    public bool IsStatic => InverseMass == 0;

    /// <summary>
    /// Спит ли тело: мир его не двигает и не решает, пока его не разбудит касание, связь, сила или присваивание скорости
    /// </summary>
    public bool IsSleeping { get; private set; }

    /// <summary>Кинетическая энергия поступательного движения и вращения, Дж</summary>
    public double KineticEnergy
    {
        get
        {
            var local = _orientation.Conjugate.Rotate(_angularVelocity);
            return (0.5 * Mass * _velocity.LengthSquared) + (0.5 * local.Dot(_inertia.Times(local)));
        }
    }

    /// <summary>Момент импульса относительно центра масс в мировых осях, кг·м²/с</summary>
    public Vector3 AngularMomentum => _orientation.Rotate(_inertia.Times(_orientation.Conjugate.Rotate(_angularVelocity)));

    /// <summary>Мир, в который тело добавлено</summary>
    internal PhysicsWorld? World { get; set; }

    /// <summary>Номер тела в широкой фазе; −1 у плоскости и у тела вне мира</summary>
    internal int Proxy { get; set; } = -1;

    /// <summary>Границы в широкой фазе устарели</summary>
    internal bool BoundsDirty { get; set; } = true;

    /// <summary>Сколько секунд тело подряд медленнее порогов сна</summary>
    internal double RestTime { get; set; }

    /// <summary>Номер тела при сборке островов для сна</summary>
    internal int Island { get; set; }

    /// <summary>Единичная нормаль плоскости</summary>
    internal Vector3 PlaneNormal { get; }

    /// <summary>Свободный член плоскости при единичной нормали</summary>
    internal double PlaneOffset { get; }

    /// <summary>Наименьшая полутолщина, м: быстрое тело за подшаг смещается не больше чем на половину ее</summary>
    internal double Extent { get; }

    /// <summary>Радиус описанной около центра масс сферы, м</summary>
    internal double Reach { get; }

    /// <summary>Тело движется решателем: подвижное и не спит</summary>
    internal bool IsAwake => !IsStatic && !IsSleeping;

    /// <summary>Неподвижное тело с заданной скоростью: подвижная платформа</summary>
    internal bool IsMovingStatic => IsStatic && HalfSpace is null && (_velocity != Vector3.Zero || _angularVelocity != Vector3.Zero);

    /// <summary>Все величины состояния конечны: иначе шаг мира не имеет смысла</summary>
    internal bool IsFinite => Finite(_position) && Finite(_velocity) && Finite(_angularVelocity)
        && double.IsFinite(_orientation.W) && double.IsFinite(_orientation.X) && double.IsFinite(_orientation.Y) && double.IsFinite(_orientation.Z);

    /// <summary>Горизонтальная земля на высоте height, м, ось z вверх</summary>
    /// <param name="height">Высота поверхности, м</param>
    public static Body Ground(double height = 0) => new(Plane.FromGeneral(0, 0, 1, -height));

    /// <summary>
    /// Неподвижная плоскость: твердое полупространство N·x + D &lt; 0 под ней. Сама отскок не гасит: коэффициент
    /// восстановления 1, у пары берется меньший, его задает тело
    /// </summary>
    /// <param name="plane">Плоскость; нормаль смотрит из твердого полупространства наружу</param>
    public static Body Ground(Plane plane)
    {
        ArgumentNullException.ThrowIfNull(plane);
        return new Body(plane);
    }

    /// <summary>Сила в центре масс, Н, до конца шага. Будит тело</summary>
    /// <param name="force">Сила, Н</param>
    public void ApplyForce(Vector3 force)
    {
        if (IsStatic)
            return;

        _force += force;
        Wake();
    }

    /// <summary>Сила в мировой точке, Н: кроме толчка центра масс дает момент. Будит тело</summary>
    /// <param name="force">Сила, Н</param>
    /// <param name="point">Точка приложения в мировых осях, м</param>
    public void ApplyForce(Vector3 force, Vector3 point)
    {
        if (IsStatic)
            return;

        _force += force;
        _torque += (point - _position).Cross(force);
        Wake();
    }

    /// <summary>Момент силы в мировых осях, Н·м. Будит тело</summary>
    /// <param name="torque">Момент, Н·м</param>
    public void ApplyTorque(Vector3 torque)
    {
        if (IsStatic)
            return;

        _torque += torque;
        Wake();
    }

    /// <summary>Мгновенный импульс в мировой точке, Н·с: удар, толчок. Будит тело</summary>
    /// <param name="impulse">Импульс, Н·с</param>
    /// <param name="point">Точка приложения в мировых осях, м</param>
    public void ApplyImpulse(Vector3 impulse, Vector3 point)
    {
        if (IsStatic)
            return;

        Push(impulse, point);
        Wake();
    }

    /// <summary>Скорость точки тела в мировых осях, м/с</summary>
    /// <param name="point">Точка в мировых осях, м</param>
    public Vector3 VelocityAt(Vector3 point) => _velocity + _angularVelocity.Cross(point - _position);

    /// <summary>
    /// Будит спящее тело. У бодрствующего тела ничего не меняется: иначе тяжесть, приложенная на каждом подшаге,
    /// не дала бы уснуть ни одному телу
    /// </summary>
    public void Wake()
    {
        if (!IsSleeping)
            return;

        IsSleeping = false;
        RestTime = 0;
    }

    /// <summary>Осеориентированные границы выпуклой формы по шести опорным точкам</summary>
    internal static (Vector3 Min, Vector3 Max) BoundsOf(IConvexShape shape) => (
        new Vector3(shape.Support(-UnitX).X, shape.Support(-UnitY).Y, shape.Support(-UnitZ).Z),
        new Vector3(shape.Support(UnitX).X, shape.Support(UnitY).Y, shape.Support(UnitZ).Z));

    /// <summary>Границы тела с формой</summary>
    internal (Vector3 Min, Vector3 Max) Bounds() => BoundsOf(Shape!);

    /// <summary>Погруженный объем, м³, и его центр под горизонтальной поверхностью на высоте surface</summary>
    internal (double Volume, Vector3 Centroid) Immersed(double surface) => _model?.Immersed(Pose, surface) ?? (0, _position);

    /// <summary>Обратный тензор инерции в мировых осях на вектор: как момент импульса меняет угловую скорость</summary>
    internal Vector3 InverseInertiaTimes(Vector3 v) => _inverseInertiaWorld.Times(v);

    /// <summary>Импульс решателя в мировой точке; не будит</summary>
    internal void Push(Vector3 impulse, Vector3 point)
    {
        if (IsStatic)
            return;

        _velocity += impulse * InverseMass;
        _angularVelocity += _inverseInertiaWorld.Times((point - _position).Cross(impulse));
    }

    /// <summary>Момент импульса решателя: поворот без толчка центра масс; не будит</summary>
    internal void Spin(Vector3 angularImpulse)
    {
        if (!IsStatic)
            _angularVelocity += _inverseInertiaWorld.Times(angularImpulse);
    }

    /// <summary>Сдвиг и поворот от импульса положения в мировой точке: так убирается проникновение и уход связей</summary>
    internal void Displace(Vector3 impulse, Vector3 point)
    {
        if (IsStatic)
            return;

        var arm = point - _position;
        _position += impulse * InverseMass;
        _orientation = _orientation.Integrate(_inverseInertiaWorld.Times(arm.Cross(impulse)), 1);
        Rotated();
    }

    /// <summary>Поворот от углового импульса положения: так убирается уход осей связей</summary>
    internal void Turn(Vector3 angularImpulse)
    {
        if (IsStatic)
            return;

        _orientation = _orientation.Integrate(_inverseInertiaWorld.Times(angularImpulse), 1);
        Rotated();
    }

    /// <summary>Перенос центра масс без пробуждения: остановка быстрого тела перед преградой</summary>
    internal void MoveTo(Vector3 position)
    {
        _position = position;
        Moved();
    }

    /// <summary>Скорости по накопленным силам и моментам; накопленное обнуляется</summary>
    internal void IntegrateVelocity(double dt)
    {
        _velocity += _force * (InverseMass * dt);
        _angularVelocity = Gyroscopic(dt) + (_inverseInertiaWorld.Times(_torque) * dt);
        (_force, _torque) = (Vector3.Zero, Vector3.Zero);
    }

    /// <summary>Положение и ориентация по скоростям; тело без скорости стоит</summary>
    internal void IntegratePosition(double dt)
    {
        if (_velocity == Vector3.Zero && _angularVelocity == Vector3.Zero)
            return;

        _position += _velocity * dt;
        _orientation = _orientation.Integrate(_angularVelocity, dt);
        Rotated();
    }

    /// <summary>Засыпание: скорости и накопленные силы обнуляются</summary>
    internal void Sleep()
    {
        IsSleeping = true;
        (_velocity, _angularVelocity, _force, _torque) = (Vector3.Zero, Vector3.Zero, Vector3.Zero, Vector3.Zero);
    }

    private static bool Finite(Vector3 v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);

    /// <summary>Кватернион по осям положения</summary>
    private static Quaternion OrientationOf(Pose pose)
    {
        if (pose.AxisX == UnitX && pose.AxisY == UnitY && pose.AxisZ == UnitZ)
            return Quaternion.Identity;

        var rotation = new Matrix(3, 3);
        for (var k = 0; k < 3; k++)
        {
            var axis = pose.Axis(k);
            (rotation[0, k], rotation[1, k], rotation[2, k]) = (axis.X, axis.Y, axis.Z);
        }

        return Quaternion.FromRotationMatrix(rotation).Normalize;
    }

    private void RequireShape()
    {
        if (HalfSpace is not null)
            throw new InvalidOperationException("Плоскость неподвижна: ее положение, поворот и скорость не меняются");
    }

    private void Moved()
    {
        _shape = null;
        BoundsDirty = true;
    }

    private void Rotated()
    {
        _inverseInertiaWorld = _inverseInertia.Rotated(_orientation);
        Moved();
    }

    /// <summary>
    /// Свободное вращение по уравнениям Эйлера в осях тела I·ω̇ = −ω × Iω шагом метода Рунге-Кутты четвертого порядка.
    /// Явный шаг Эйлера накачивает энергию несимметричного тела, неявный гасит ее; при подшаге, за который тело
    /// поворачивается меньше чем на полрадиана, этот шаг держит энергию и момент импульса до долей процента за минуты.
    /// Считается прямо, а не общим решателем: это самое частое место движка, и лишние массивы здесь дороги
    /// </summary>
    private Vector3 Gyroscopic(double dt)
    {
        Vector3 Rate(Vector3 w) => _inverseInertia.Times(-w.Cross(_inertia.Times(w)));

        var start = _orientation.Conjugate.Rotate(_angularVelocity);
        var first = Rate(start);
        var second = Rate(start + (first * (dt / 2)));
        var third = Rate(start + (second * (dt / 2)));
        var fourth = Rate(start + (third * dt));
        return _orientation.Rotate(start + ((first + (second * 2) + (third * 2) + fourth) * (dt / 6)));
    }
}
