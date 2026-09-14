#nullable enable
using AI.Geometry.Collision;
using AI.Geometry.Primitives;
using AI.Geometry.Spatial;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Мир твердых тел: силы, интегрирование с фиксированным шагом (полунеявный Эйлер), контакты и связи
/// последовательными импульсами, сон покоящихся тел. Все в СИ.
/// </summary>
/// <remarks>
/// <para>
/// Подшаг: широкая фаза по <see cref="DynamicAabbTree{T}"/> (плоскости проверяются со всеми подвижными телами) →
/// пробуждение спящих тел, которых касаются бодрствующие → силы → узкая фаза <see cref="CollisionDispatcher"/>
/// с переносом импульсов прошлого подшага по номеру элемента касания → скорости → прогрев → проходы по скоростям
/// (удар с восстановлением, трение по двум касательным в пределах μN, сопротивление качению и верчению, связи) →
/// положения → проверка быстрых тел по моменту касания <see cref="TimeOfImpact"/> → проходы по положениям
/// (проникновение и уход связей убираются сдвигом и поворотом, а не добавкой скорости, поэтому ни один короткий шаг не
/// разгоняет тела) → сон.
/// </para>
/// <para>
/// Шаг дробится так, чтобы тело за подшаг смещалось и поворачивалось краем не больше чем на половину своей толщины.
/// Тело, которое за подшаг все равно прошло больше своей толщины, останавливается в момент первого касания.
/// </para>
/// <para>
/// Сон: тело, которое вместе со всем своим островом (телами, связанными касаниями и связями) дольше
/// <see cref="TimeToSleep"/> медленнее порогов, засыпает и не считается. Его будит касание бодрствующего тела,
/// связь с ним, приложенная сила или импульс, присваивание положения или скорости.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var world = new PhysicsWorld();
/// world.Add(Body.Ground());
/// var box = world.Add(new Body(new BoxShape(Pose.At(new Vector3(0, 0, 1)), new Vector3(0.2, 0.2, 0.2)), 2));
/// world.AddForce(Forces.Gravity());
/// world.Run(duration: 3, dt: 1e-3);
/// </code>
/// </example>
public sealed class PhysicsWorld
{
    /// <summary>Запас границ в широкой фазе, м</summary>
    private const double TreeMargin = 0.05;

    /// <summary>Точки касания той же пары ближе этого, м, считаются той же точкой, если номера элемента нет</summary>
    private const double SamePoint = 0.02;

    /// <summary>Наибольший сдвиг за проход по положениям, м: глубокое проникновение убирается за несколько подшагов</summary>
    private const double MaxCorrection = 0.2;

    private readonly List<Body> _bodies = [];
    private readonly List<Body> _planes = [];
    private readonly List<Joint> _joints = [];
    private readonly List<Action<IReadOnlyList<Body>>> _forces = [];
    private readonly HashSet<(Body, Body)> _connected = [];
    private readonly DynamicAabbTree<Body> _tree = new(TreeMargin);
    private readonly List<(int A, int B)> _pairs = [];
    private readonly List<(Body A, Body B)> _candidates = [];
    private readonly List<int> _hits = [];
    private readonly List<Body> _awake = [];
    private readonly List<Joint> _activeJoints = [];
    private readonly List<(Body Body, IConvexShape Start, Vector3 From)> _fast = [];
    private Dictionary<(Body, Body), List<ContactConstraint>> _previous = [];

    /// <summary>Тела мира</summary>
    public IReadOnlyList<Body> Bodies => _bodies;

    /// <summary>Связи мира</summary>
    public IReadOnlyList<Joint> Joints => _joints;

    /// <summary>Время мира, с</summary>
    public double Time { get; private set; }

    /// <summary>Проходов по скоростям контактов и связей за подшаг: больше означает точнее стопки, цепи и покой</summary>
    public int Iterations { get; set; } = 16;

    /// <summary>Проходов по положениям за подшаг</summary>
    public int PositionIterations { get; set; } = 4;

    /// <summary>Доля проникновения, убираемая за проход по положениям</summary>
    public double Correction { get; set; } = 0.2;

    /// <summary>Доля ухода связей, убираемая за проход по положениям</summary>
    public double JointCorrection { get; set; } = 0.5;

    /// <summary>Допустимое проникновение, м: оно держит контакт лежащего тела от шага к шагу</summary>
    public double Slop { get; set; } = 0.001;

    /// <summary>Скорость удара, м/с, ниже которой тело не отскакивает: иначе лежащее тело дрожало бы от тяжести</summary>
    public double BounceThreshold { get; set; } = 0.5;

    /// <summary>Наибольшее число подшагов за шаг</summary>
    public int MaxSubsteps { get; set; } = 500;

    /// <summary>Засыпают ли покоящиеся тела</summary>
    public bool SleepingEnabled { get; set; } = true;

    /// <summary>Скорость центра масс, м/с, ниже которой тело считается покоящимся</summary>
    public double SleepLinearSpeed { get; set; } = 0.05;

    /// <summary>Угловая скорость, рад/с, ниже которой тело считается покоящимся</summary>
    public double SleepAngularSpeed { get; set; } = 0.05;

    /// <summary>Сколько секунд остров должен покоиться, чтобы уснуть</summary>
    public double TimeToSleep { get; set; } = 0.5;

    /// <summary>Добавляет тело</summary>
    /// <param name="body">Тело, еще не добавленное ни в какой мир</param>
    /// <returns>То же тело</returns>
    public Body Add(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.World is not null)
            throw new InvalidOperationException("Тело уже добавлено в мир");

        body.World = this;
        _bodies.Add(body);
        if (body.HalfSpace is not null)
        {
            _planes.Add(body);
        }
        else
        {
            var (min, max) = body.Bounds();
            body.Proxy = _tree.Insert(min, max, body);
            body.BoundsDirty = false;
        }

        return body;
    }

    /// <summary>Убирает тело вместе с его связями и будит остальные тела: они могли на нем лежать</summary>
    /// <param name="body">Тело</param>
    /// <returns>Было ли тело в этом мире</returns>
    public bool Remove(Body body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.World != this)
            return false;

        _bodies.Remove(body);
        _planes.Remove(body);
        if (body.Proxy >= 0)
            _tree.Remove(body.Proxy);

        body.Proxy = -1;
        body.World = null;
        body.BoundsDirty = true;
        _joints.RemoveAll(joint => joint.A == body || joint.B == body);
        _connected.Clear();
        foreach (var joint in _joints)
            Exclude(joint);

        _previous = _previous.Where(pair => pair.Key.Item1 != body && pair.Key.Item2 != body).ToDictionary(pair => pair.Key, pair => pair.Value);
        foreach (var other in _bodies)
            other.Wake();

        return true;
    }

    /// <summary>Связь двух тел; соединенные тела друг с другом не сталкиваются, если связь этого не просит</summary>
    /// <param name="joint">Связь</param>
    public void Connect(Joint joint)
    {
        ArgumentNullException.ThrowIfNull(joint);
        _joints.Add(joint);
        Exclude(joint);
        joint.A.Wake();
        joint.B.Wake();
    }

    /// <summary>Сила, прикладываемая перед каждым подшагом к бодрствующим подвижным телам, например из <see cref="Forces"/></summary>
    /// <param name="force">Сила</param>
    public void AddForce(Action<IReadOnlyList<Body>> force)
    {
        ArgumentNullException.ThrowIfNull(force);
        _forces.Add(force);
    }

    /// <summary>
    /// Шаг времени. Тело за подшаг смещается и поворачивается краем не больше чем на половину своей толщины, а быстрее
    /// летящее тело дополнительно останавливается в момент касания, поэтому пуля не проскакивает лист тоньше себя
    /// </summary>
    /// <param name="dt">Шаг, с</param>
    public void Step(double dt)
    {
        if (!(dt > 0) || !double.IsFinite(dt))
            throw new ArgumentOutOfRangeException(nameof(dt), "Шаг времени должен быть положительным числом");

        foreach (var body in _bodies)
        {
            if (!body.IsFinite)
                throw new InvalidOperationException($"Состояние тела не число: положение {body.Position}, скорость {body.Velocity}");
        }

        var fastest = 1.0;
        foreach (var body in _bodies)
        {
            if (body.IsAwake)
                fastest = Math.Max(fastest, (body.Velocity.Length + (body.AngularVelocity.Length * body.Reach)) * dt / (0.5 * body.Extent));
        }

        var substeps = (int)Math.Clamp(Math.Ceiling(fastest), 1, Math.Max(1, MaxSubsteps));
        for (var i = 0; i < substeps; i++)
            Advance(dt / substeps);
    }

    /// <summary>Шаги до конца срока или до условия остановки, проверяемого после каждого шага</summary>
    /// <param name="duration">Срок, с</param>
    /// <param name="dt">Шаг, с</param>
    /// <param name="stop">Условие остановки</param>
    public void Run(double duration, double dt, Func<PhysicsWorld, bool>? stop = null)
    {
        var end = Time + duration;
        while (Time < end)
        {
            // Хвост короче миллионной доли шага ничего не меняет: время просто доходит до конца
            var step = Math.Min(dt, end - Time);
            if (step < dt * 1e-6)
            {
                Time = end;
                return;
            }

            Step(step);
            if (stop?.Invoke(this) == true)
                return;
        }
    }

    /// <summary>Приведенная масса пары тел вдоль направления в точках приложения, кг</summary>
    internal static double EffectiveMass(Body a, Body b, Vector3 ra, Vector3 rb, Vector3 direction)
    {
        var inverse = a.InverseMass + b.InverseMass
            + a.InverseInertiaTimes(ra.Cross(direction)).Cross(ra).Dot(direction)
            + b.InverseInertiaTimes(rb.Cross(direction)).Cross(rb).Dot(direction);
        return inverse > 0 ? 1 / inverse : 0;
    }

    private void Advance(double dt)
    {
        foreach (var body in _bodies)
        {
            if (!SleepingEnabled && body.IsSleeping)
                body.Wake();

            if (body.Proxy >= 0 && body.BoundsDirty)
            {
                var (min, max) = body.Bounds();
                _tree.Update(body.Proxy, min, max);
                body.BoundsDirty = false;
            }
        }

        FindCandidates();
        WakeTouched();

        _awake.Clear();
        foreach (var body in _bodies)
        {
            if (body.IsAwake)
                _awake.Add(body);
        }

        foreach (var force in _forces)
            force(_awake);

        // Касания и скорость удара берутся до того, как силы подшага разогнали тела: иначе отскок отражал бы и прибавку
        // тяжести за подшаг, и упругий мяч с каждым ударом прыгал бы выше
        var contacts = Contacts();
        foreach (var body in _awake)
            body.IntegrateVelocity(dt);

        _activeJoints.Clear();
        foreach (var joint in _joints)
        {
            if (joint.A.IsAwake || joint.B.IsAwake)
                _activeJoints.Add(joint);
        }

        foreach (var contact in contacts)
            contact.WarmStart();
        foreach (var joint in _activeJoints)
            joint.Begin(dt);

        for (var i = 0; i < Iterations; i++)
        {
            foreach (var contact in contacts)
                contact.Resolve();
            foreach (var joint in _activeJoints)
                joint.SolveVelocity(dt);
        }

        _fast.Clear();
        foreach (var body in _awake)
        {
            if (body.Velocity.Length * dt > body.Extent)
                _fast.Add((body, body.Shape!, body.Position));
        }

        foreach (var body in _bodies)
        {
            if (body.IsAwake || body.IsMovingStatic)
                body.IntegratePosition(dt);
        }

        foreach (var (body, start, from) in _fast)
            Sweep(body, start, from);

        for (var i = 0; i < PositionIterations; i++)
        {
            foreach (var contact in contacts)
                contact.Correct(Correction, Slop, MaxCorrection);
            foreach (var joint in _activeJoints)
                joint.SolvePosition(JointCorrection);
        }

        Remember(contacts);
        Sleep(dt, contacts);
        Time += dt;
    }

    /// <summary>
    /// Пары-кандидаты: пересечения границ в дереве, кроме пар неподвижных тел и соединенных связью, и каждая плоскость
    /// с каждым подвижным телом
    /// </summary>
    private void FindCandidates()
    {
        _candidates.Clear();
        _pairs.Clear();
        _tree.QueryPairs(_pairs);
        foreach (var (first, second) in _pairs)
        {
            var (a, b) = (_tree[first], _tree[second]);
            if ((a.IsStatic && b.IsStatic) || _connected.Contains((a, b)))
                continue;

            _candidates.Add((a, b));
        }

        foreach (var plane in _planes)
        {
            foreach (var body in _bodies)
            {
                if (!body.IsStatic && !_connected.Contains((plane, body)))
                    _candidates.Add((plane, body));
            }
        }
    }

    /// <summary>Спящие тела, границы которых задевает бодрствующее тело или подвижная платформа, и спящие концы связей просыпаются</summary>
    private void WakeTouched()
    {
        if (!SleepingEnabled)
            return;

        bool changed;
        do
        {
            changed = false;
            foreach (var (a, b) in _candidates)
                changed |= Touch(a, b) | Touch(b, a);
            foreach (var joint in _joints)
                changed |= Touch(joint.A, joint.B) | Touch(joint.B, joint.A);
        }
        while (changed);

        static bool Touch(Body source, Body target)
        {
            if (!target.IsSleeping || !(source.IsAwake || source.IsMovingStatic))
                return false;

            target.Wake();
            return true;
        }
    }

    /// <summary>Точки касания пар, где хотя бы одно тело бодрствует; импульсы той же точки прошлого подшага переносятся</summary>
    private List<ContactConstraint> Contacts()
    {
        var contacts = new List<ContactConstraint>();
        foreach (var (a, b) in _candidates)
        {
            if (!a.IsAwake && !b.IsAwake)
                continue;

            // Касание с плоскостью дается с нормалью от формы к плоскости; решателю нужна от A к B
            var manifold = a.HalfSpace is { } plane
                ? CollisionDispatcher.Collide(b.Shape!, plane).Flipped()
                : CollisionDispatcher.Collide(a.Shape!, b.Shape!);
            if (!manifold.HasContact)
                continue;

            _previous.TryGetValue((a, b), out var earlier);
            foreach (var point in manifold.Points)
            {
                var contact = new ContactConstraint(a, b, point, manifold.Normal, Bounce(a, b, point.Position, manifold.Normal));
                if (earlier is not null && Match(earlier, contact) is { } same)
                    contact.Inherit(same);

                contacts.Add(contact);
            }
        }

        return contacts;
    }

    /// <summary>
    /// Та же точка прошлого подшага: по номеру пары элементов касания, а если его нет, по близости
    /// </summary>
    private static ContactConstraint? Match(List<ContactConstraint> earlier, ContactConstraint contact)
    {
        if (contact.Feature is { } feature)
            return earlier.Find(old => old.Feature == feature);

        var reach = Math.Min(SamePoint, 0.25 * Math.Min(contact.A.Extent, contact.B.Extent));
        return earlier
            .Where(old => old.Feature is null && (old.Point - contact.Point).Length < reach)
            .MinBy(old => (old.Point - contact.Point).LengthSquared);
    }

    /// <summary>Скорость разлета после удара: −e·vₙ для удара быстрее порога, e меньший у пары</summary>
    private double Bounce(Body a, Body b, Vector3 point, Vector3 normal)
    {
        var approach = (b.VelocityAt(point) - a.VelocityAt(point)).Dot(normal);
        return approach < -BounceThreshold ? -Math.Min(a.Restitution, b.Restitution) * approach : 0;
    }

    /// <summary>
    /// Касания подшага для теплого старта следующего; касания спящих пар сохраняются до пробуждения
    /// </summary>
    private void Remember(List<ContactConstraint> contacts)
    {
        var next = new Dictionary<(Body, Body), List<ContactConstraint>>();
        foreach (var contact in contacts)
        {
            var key = (contact.A, contact.B);
            if (!next.TryGetValue(key, out var list))
                next[key] = list = [];

            list.Add(contact);
        }

        foreach (var (key, list) in _previous)
        {
            if (!key.Item1.IsAwake && !key.Item2.IsAwake && (key.Item1.IsSleeping || key.Item2.IsSleeping))
                next.TryAdd(key, list);
        }

        _previous = next;
    }

    /// <summary>
    /// Быстрое тело, прошедшее за подшаг больше своей толщины, проверяется по моменту первого касания при
    /// поступательном движении из начала подшага против всех тел в заметенных границах и всех плоскостей. Первая
    /// преграда останавливает его в момент касания с малым проникновением, и касание решается на следующем подшаге
    /// </summary>
    private void Sweep(Body body, IConvexShape start, Vector3 from)
    {
        var path = body.Position - from;
        var length = path.Length;
        if (length <= body.Extent)
            return;

        var first = double.PositiveInfinity;
        var (startMin, startMax) = Body.BoundsOf(start);
        var (endMin, endMax) = body.Bounds();
        _hits.Clear();
        _tree.Query(Vector3.Min(startMin, endMin), Vector3.Max(startMax, endMax), _hits);
        foreach (var proxy in _hits)
        {
            var other = _tree[proxy];
            if (!ReferenceEquals(other, body) && !_connected.Contains((body, other)) && Impact(start, path, other) is { } t && t < first)
                first = t;
        }

        foreach (var plane in _planes)
        {
            if (!_connected.Contains((body, plane)) && Impact(start, path, plane) is { } t && t < first)
                first = t;
        }

        if (!double.IsFinite(first))
            return;

        var overlap = Math.Min(0.5 * Slop, 0.25 * body.Extent);
        body.MoveTo(from + (path * Math.Min(1, first + (overlap / length))));
    }

    /// <summary>
    /// Доля пути до первого касания; <c>null</c>, если касания нет или тела касаются уже в начале: такое касание
    /// решает обычный контакт
    /// </summary>
    private static double? Impact(IConvexShape start, Vector3 path, Body other)
    {
        double? t;
        if (other.HalfSpace is { } plane)
        {
            if (start is SphereShape sphere)
            {
                t = TimeOfImpact.SpherePlane(sphere, path, plane);
            }
            else
            {
                var normal = other.PlaneNormal;
                var gap = normal.Dot(start.Support(-normal)) + other.PlaneOffset;
                var approach = -normal.Dot(path);
                t = gap > 0 && approach > 0 && gap <= approach ? gap / approach : null;
            }
        }
        else
        {
            var target = other.Shape!;
            t = (start, target) switch
            {
                (SphereShape sphere, SphereShape ball) => TimeOfImpact.SphereSphere(sphere, path, ball),
                (SphereShape sphere, BoxShape box) => TimeOfImpact.SphereBox(sphere, path, box),
                _ => TimeOfImpact.Translational(start, path, target, Vector3.Zero),
            };
        }

        return t > 0 ? t : null;
    }

    /// <summary>
    /// Сон по островам: тела, связанные касаниями и связями, засыпают вместе, когда самое беспокойное из них дольше
    /// <see cref="TimeToSleep"/> медленнее порогов. Касание подвижной платформы или связь с ней не дают уснуть
    /// </summary>
    private void Sleep(double dt, List<ContactConstraint> contacts)
    {
        if (!SleepingEnabled || _awake.Count == 0)
            return;

        var parent = new int[_awake.Count];
        for (var i = 0; i < _awake.Count; i++)
        {
            var body = _awake[i];
            body.Island = i;
            parent[i] = i;
            var slow = body.Velocity.Length < SleepLinearSpeed && body.AngularVelocity.Length < SleepAngularSpeed;
            body.RestTime = slow ? body.RestTime + dt : 0;
        }

        foreach (var contact in contacts)
            Link(contact.A, contact.B);
        foreach (var joint in _activeJoints)
            Link(joint.A, joint.B);

        var rest = new double[_awake.Count];
        Array.Fill(rest, double.PositiveInfinity);
        for (var i = 0; i < _awake.Count; i++)
        {
            var root = Find(i);
            rest[root] = Math.Min(rest[root], _awake[i].RestTime);
        }

        for (var i = 0; i < _awake.Count; i++)
        {
            if (rest[Find(i)] >= TimeToSleep)
                _awake[i].Sleep();
        }

        void Link(Body a, Body b)
        {
            if (a.IsMovingStatic)
                b.RestTime = 0;
            if (b.IsMovingStatic)
                a.RestTime = 0;
            if (Member(a) && Member(b))
                parent[Find(a.Island)] = Find(b.Island);
        }

        bool Member(Body body) => body.Island >= 0 && body.Island < _awake.Count && ReferenceEquals(_awake[body.Island], body);

        int Find(int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }
    }

    private void Exclude(Joint joint)
    {
        if (!joint.CollideConnected)
            _connected.UnionWith([(joint.A, joint.B), (joint.B, joint.A)]);
    }
}
