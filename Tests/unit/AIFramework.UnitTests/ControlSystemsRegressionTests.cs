using AI.ControlSystems.Adaptive;
using AI.ControlSystems.Linear;
using AI.ControlSystems.Nonlinear;
using AI.ControlSystems.Observers;
using AI.ControlSystems.Optimal;
using AI.DataStructs.Algebraic;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Регрессии, записанные только через прежний API: на старой версии сборки эти тесты
/// компилируются и падают, на новой проходят.
/// </summary>
public class ControlSystemsRegressionTests
{
    [Fact]
    public void Discretization_StiffSystem_IsExact()
    {
        // ẋ = −50x + u с шагом 1: Ad = e^−50, Bd = (1 − e^−50)/50. Прежний ряд Тейлора терял точность
        Discretization.ZeroOrderHold(new Matrix(new double[,] { { -50 } }), new Matrix(new double[,] { { 1 } }), 1.0,
            out Matrix ad, out Matrix bd);

        Assert.Equal(Math.Exp(-50), ad[0, 0], 15);
        Assert.Equal((1 - Math.Exp(-50)) / 50, bd[0, 0], 12);
    }

    [Fact]
    public void Lqr_NonStabilizablePair_IsRejected()
    {
        // Неустойчивая мода 1.5 не управляется: стабилизирующего регулятора нет, прежде возвращалось усиление
        var a = new Matrix(new double[,] { { 1.5, 0 }, { 0, 0.5 } });
        var b = new Matrix(new double[,] { { 0 }, { 1 } });
        var eye = new Matrix(new double[,] { { 1, 0 }, { 0, 1 } });

        _ = Assert.Throws<InvalidOperationException>(() => DiscreteLqr.Solve(a, b, eye, new Matrix(new double[,] { { 1 } })));
    }

    [Fact]
    public void PolePlacement_SmallScaleControllablePair_IsAccepted()
    {
        // Матрица управляемости обусловлена хорошо, но её определитель −1e-6 и в углу ноль:
        // прежнее обращение через Matrix.GetInvertMatrix объявляло её вырожденной
        var a = new Matrix(new double[,] { { 1, 0.1, 0 }, { 0, 1, 0.1 }, { 0, 0, 0.9 } });
        var b = new Matrix(new double[,] { { 0 }, { 0 }, { 0.1 } });

        // (λ − 0.5)(λ − 0.6)(λ − 0.7) = λ³ − 1.8λ² + 1.07λ − 0.21
        Matrix k = PolePlacement.AckermannGain(a, b, new Vector(new[] { -0.21, 1.07, -1.8 }));
        Matrix m = a - (b * k);

        // Коэффициенты характеристического многочлена 3×3: след, сумма главных миноров, определитель
        double trace = m[0, 0] + m[1, 1] + m[2, 2];
        double minors = (m[0, 0] * m[1, 1]) - (m[0, 1] * m[1, 0])
            + (m[0, 0] * m[2, 2]) - (m[0, 2] * m[2, 0])
            + (m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1]);
        double determinant = (m[0, 0] * ((m[1, 1] * m[2, 2]) - (m[1, 2] * m[2, 1])))
            - (m[0, 1] * ((m[1, 0] * m[2, 2]) - (m[1, 2] * m[2, 0])))
            + (m[0, 2] * ((m[1, 0] * m[2, 1]) - (m[1, 1] * m[2, 0])));

        Assert.Equal(1.8, trace, 10);
        Assert.Equal(1.07, minors, 10);
        Assert.Equal(0.21, determinant, 10);
    }

    [Fact]
    public void Kalman_PreciseSensors_DoNotBreakUpdate()
    {
        // Датчики x₁ и x₁ + x₂ с σ = 1 мм: det S ≈ 1e-12, и прежний абсолютный порог 1e-10 в
        // Matrix.GetInvertMatrix объявлял недиагональную S вырожденной со второго шага
        var eye = new Matrix(new double[,] { { 1, 0 }, { 0, 1 } });
        var sensors = new Matrix(new double[,] { { 1, 0 }, { 1, 1 } });
        var zero = new Matrix(new double[,] { { 0 }, { 0 } });
        var filter = new KalmanFilter(eye, zero, sensors, zero, eye * 1e-8, eye * 1e-6);
        var u = new Vector(new[] { 0.0 });

        for (int k = 0; k < 10; k++)
        {
            filter.Predict(u);
            filter.Update(new Vector(new[] { 0.3, 0.1 }), u);
        }

        Assert.Equal(0.3, filter.State[0], 4);
        Assert.Equal(-0.2, filter.State[1], 4);
    }

    [Fact]
    public void ScalarSlidingMode_DrivesFirstOrderPlantToSetpoint()
    {
        // Объект ẏ = −y + u с положительным усилением; прежний закон u = −k·sign(s) уводил y от уставки
        var smc = new SlidingModeController { Lambda = 0, Gain = 10, SmoothingBoundary = 0.05 };
        double y = 0, dt = 0.001;

        for (int k = 0; k < 3000; k++)
            y += (-y + smc.Compute(1.0, y, dt)) * dt;

        Assert.True(Math.Abs(y - 1) < 0.05, $"y = {y}");
    }

    [Fact]
    public void FirstOrderMrac_TracksUnstablePlant()
    {
        // Объект ẏ = y + 2u неустойчив; прежний MRAC с одним коэффициентом и неверным знаком расходился
        var mrac = new ModelReferenceAdaptiveController { AdaptationGain = 5, ReferencePole = 3, Theta = 0 };
        double y = 0, dt = 0.001, worst = 0;

        for (int k = 0; k < 60_000; k++)
        {
            double t = k * dt;
            double r = Math.Floor(t / 5) % 2 == 0 ? 1 : -1;
            double u = mrac.Compute(r, y, dt);
            y += (y + (2 * u)) * dt;

            if (t > 50)
                worst = Math.Max(worst, Math.Abs(y - mrac.ReferenceOutput));
        }

        Assert.True(worst < 0.05, $"ошибка слежения {worst}");
    }
}
