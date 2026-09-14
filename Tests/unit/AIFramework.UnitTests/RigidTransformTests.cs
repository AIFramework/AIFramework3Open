#nullable enable

using AI.Geometry.Primitives;
using AI.Geometry.Transforms;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Движение твердого тела <see cref="RigidTransform"/>: экспонента и логарифм, композиция и обращение,
/// поворот вокруг прямой, винтовая интерполяция.
/// </summary>
public class RigidTransformTests
{
    private const double Tolerance = 1e-12;

    private static void Near(Vector3 expected, Vector3 actual, double tolerance = Tolerance) =>
        Assert.True(expected.DistanceTo(actual) <= tolerance, $"ожидалось {expected}, получено {actual}");

    [Fact]
    public void RigidTransform_Log_InvertsExp()
    {
        var angular = new Vector3(0.3, -0.5, 0.8);
        var linear = new Vector3(1, 2, -0.5);

        var (w, v) = RigidTransform.Exp(angular, linear).Log();

        Near(angular, w);
        Near(linear, v);
    }

    [Theory]
    [InlineData(1e-12)]
    [InlineData(1e-6)]
    [InlineData(0.0099)]
    [InlineData(0.0101)]
    [InlineData(3.0)]
    public void RigidTransform_ExpLog_RoundTripIsStableForAnyAngle(double angle)
    {
        Vector3 angular = new Vector3(1, 2, 2) * (angle / 3);
        var linear = new Vector3(-0.7, 0.4, 1.1);

        var (w, v) = RigidTransform.Exp(angular, linear).Log();

        Near(angular, w);
        Near(linear, v, 1e-11);
    }

    [Fact]
    public void RigidTransform_Exp_SmallAngleMatchesFirstOrder()
    {
        var angular = new Vector3(0, 0, 1e-9);
        var linear = new Vector3(1, 0, 0);

        RigidTransform motion = RigidTransform.Exp(angular, linear);

        // Первый порядок: t = v + ½·ω × v
        Near(linear + (angular.Cross(linear) * 0.5), motion.Translation, 1e-18);
    }

    [Fact]
    public void RigidTransform_Compose_AppliesSecondThenFirst()
    {
        RigidTransform first = RigidTransform.Exp(new Vector3(0.2, 0.1, -0.4), new Vector3(1, 0, 2));
        RigidTransform second = RigidTransform.Exp(new Vector3(-0.6, 0.3, 0.5), new Vector3(0, -1, 0.5));
        var point = new Vector3(0.3, -1.2, 2.5);

        Near(first.Apply(second.Apply(point)), (first * second).Apply(point));
        Near(first.ApplyVector(point), first.Apply(point) - first.Translation);
    }

    [Fact]
    public void RigidTransform_Inverse_UndoesMotion()
    {
        RigidTransform motion = RigidTransform.Exp(new Vector3(1.1, -0.4, 0.7), new Vector3(3, -2, 1));
        var point = new Vector3(-1, 4, 0.5);

        Near(point, motion.Inverse.Apply(motion.Apply(point)));
        Near(point, (motion * motion.Inverse).Apply(point));
    }

    [Fact]
    public void RigidTransform_AboutAxis_RotatesAroundOffsetLine()
    {
        // Ось через (1, 0, 0) вдоль z, поворот на 90°: точка (2, 0, 5) переходит в (1, 1, 5)
        RigidTransform turn = RigidTransform.AboutAxis(new Vector3(1, 0, 0), new Vector3(0, 0, 3), Math.PI / 2);

        Near(new Vector3(1, 1, 5), turn.Apply(new Vector3(2, 0, 5)));
        Near(new Vector3(1, 0, 7), turn.Apply(new Vector3(1, 0, 7)));
    }

    [Fact]
    public void RigidTransform_Interpolate_FollowsScrew()
    {
        // Винт вдоль z: поворот на 90° и подъем на 2; на полпути поворот на 45° и подъем на 1
        RigidTransform from = RigidTransform.Identity;
        RigidTransform to = RigidTransform.Exp(new Vector3(0, 0, Math.PI / 2), new Vector3(0, 0, 2));
        var point = new Vector3(1, 0, 0);

        Near(from.Apply(point), RigidTransform.Interpolate(from, to, 0).Apply(point));
        Near(to.Apply(point), RigidTransform.Interpolate(from, to, 1).Apply(point));
        Near(new Vector3(Math.Sqrt(0.5), Math.Sqrt(0.5), 1), RigidTransform.Interpolate(from, to, 0.5).Apply(point));
    }

    [Fact]
    public void RigidTransform_ToAffine_AppliesSameMotion()
    {
        RigidTransform motion = RigidTransform.Exp(new Vector3(0.5, -0.2, 0.9), new Vector3(1, 2, 3));
        var point = new Vector3(-2, 0.5, 1);

        Near(motion.Apply(point), Vector3.FromVector(motion.ToAffine().Apply(point.ToVector())));
    }
}
