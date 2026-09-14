#nullable enable
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Связь двух тел. Решается в тех же проходах, что и контакты: по скоростям импульсами без добавок, а уход положения
/// убирается в проходах по положениям сдвигом и поворотом тел. Поэтому уход не делится на шаг, и сколь угодно короткий
/// шаг не разгоняет тела. Точки крепления задаются в осях тела, в метрах от центра масс
/// </summary>
public abstract class Joint
{
    private readonly Vector3 _anchorA;
    private readonly Vector3 _anchorB;

    private protected Joint(Body a, Vector3 anchorA, Body b, Vector3 anchorB)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (ReferenceEquals(a, b))
            throw new ArgumentException("Связь соединяет два разных тела", nameof(b));

        (A, B, _anchorA, _anchorB) = (a, b, anchorA, anchorB);
    }

    /// <summary>Первое тело</summary>
    public Body A { get; }

    /// <summary>Второе тело</summary>
    public Body B { get; }

    /// <summary>Сталкиваются ли соединенные тела друг с другом: обычно нет, петля двери не должна упираться в косяк</summary>
    public bool CollideConnected { get; init; }

    /// <summary>Точка крепления на теле A в мировых осях, м</summary>
    public Vector3 PointA => A.Position + A.Orientation.Rotate(_anchorA);

    /// <summary>Точка крепления на теле B в мировых осях, м</summary>
    public Vector3 PointB => B.Position + B.Orientation.Rotate(_anchorB);

    /// <summary>Начало подшага: сброс накопленного</summary>
    internal virtual void Begin(double dt)
    {
    }

    /// <summary>Проход по скоростям</summary>
    internal abstract void SolveVelocity(double dt);

    /// <summary>Проход по положениям; share это доля ухода, убираемая за проход</summary>
    internal abstract void SolvePosition(double share);

    /// <summary>Податливость пары на поворот вокруг направления: относительная угловая скорость от единичного углового импульса</summary>
    private protected double Turning(Vector3 direction) =>
        A.InverseInertiaTimes(direction).Dot(direction) + B.InverseInertiaTimes(direction).Dot(direction);

    /// <summary>Относительный угловой импульс: B поворачивается вокруг направления вперед, A назад</summary>
    private protected void Twist(Vector3 impulse)
    {
        A.Spin(-impulse);
        B.Spin(impulse);
    }

    /// <summary>Относительный поворот положения: B вперед, A назад</summary>
    private protected void Turn(Vector3 impulse)
    {
        A.Turn(-impulse);
        B.Turn(impulse);
    }
}
