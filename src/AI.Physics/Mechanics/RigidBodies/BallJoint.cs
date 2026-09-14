#nullable enable
using AI.ClassicMath.MatrixUtils;
using AI.DataStructs.Algebraic;
using AI.Geometry.Primitives;

namespace AI.Physics.Mechanics.RigidBodies;

/// <summary>
/// Шаровой шарнир: точки крепления совпадают, вращение свободное. Три направления решаются вместе, системой 3×3
/// (<see cref="LU"/> из AI.ClassicMath): у маленького груза на длинном рычаге оси сильно связаны, и порознь связь
/// сходится плохо
/// </summary>
/// <param name="a">Первое тело</param>
/// <param name="anchorA">Точка крепления в осях первого тела, м</param>
/// <param name="b">Второе тело</param>
/// <param name="anchorB">Точка крепления в осях второго тела, м</param>
public sealed class BallJoint(Body a, Vector3 anchorA, Body b, Vector3 anchorB) : Joint(a, anchorA, b, anchorB)
{
    private static readonly Vector3[] Axes = [new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)];

    internal override void SolveVelocity(double dt)
    {
        if (A.IsStatic && B.IsStatic)
            return;

        var (pointA, pointB) = (PointA, PointB);
        var impulse = Solve(pointA - A.Position, pointB - B.Position, A.VelocityAt(pointA) - B.VelocityAt(pointB));
        A.Push(-impulse, pointA);
        B.Push(impulse, pointB);
    }

    internal override void SolvePosition(double share)
    {
        var (pointA, pointB) = (PointA, PointB);
        var error = pointB - pointA;
        if ((A.IsStatic && B.IsStatic) || error.Length < 1e-12)
            return;

        var impulse = Solve(pointA - A.Position, pointB - B.Position, -error * share);
        A.Displace(-impulse, pointA);
        B.Displace(impulse, pointB);
    }

    /// <summary>
    /// Импульс в точке, меняющий относительную скорость точек крепления на target: K·P = target, где столбец K для
    /// оси e это e·(1/m_A + 1/m_B) + (I_A⁻¹(r_A × e)) × r_A + (I_B⁻¹(r_B × e)) × r_B
    /// </summary>
    private Vector3 Solve(Vector3 ra, Vector3 rb, Vector3 target)
    {
        var matrix = new Matrix(3, 3);
        for (var k = 0; k < 3; k++)
        {
            var e = Axes[k];
            var column = (e * (A.InverseMass + B.InverseMass)) + A.InverseInertiaTimes(ra.Cross(e)).Cross(ra) + B.InverseInertiaTimes(rb.Cross(e)).Cross(rb);
            (matrix[0, k], matrix[1, k], matrix[2, k]) = (column.X, column.Y, column.Z);
        }

        var solution = LU.Solve(matrix, new Vector(target.X, target.Y, target.Z));
        return new Vector3(solution[0], solution[1], solution[2]);
    }
}
