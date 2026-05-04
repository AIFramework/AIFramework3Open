using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations;

public static partial class AdvancedIntegrationEngine
{
    #region Степени тригонометрических и гиперболических функций

    private static Expression? TryTrigonometricPowers(Expression expr, string variable)
    {
        if (expr is not Power pow || pow.Exponent is not Constant exp) return null;
        var x = new Variable(variable);

        if (pow.Base is Sin sin && sin.Argument is Variable vs && vs.Name == variable)
            return IntegrateSinPower(x, exp.Value);

        if (pow.Base is Cos cos && cos.Argument is Variable vc && vc.Name == variable)
            return IntegrateCosPower(x, exp.Value);

        if (pow.Base is Tan tan && tan.Argument is Variable vt && vt.Name == variable)
            return IntegrateTanPower(x, exp.Value);

        if (pow.Base is Sec sec && sec.Argument is Variable vsec && vsec.Name == variable)
            if (System.Math.Abs(exp.Value - 2) < 1e-10) return new Tan(x);

        if (pow.Base is Csc csc && csc.Argument is Variable vcsc && vcsc.Name == variable)
            if (System.Math.Abs(exp.Value - 2) < 1e-10) return new Multiply(new Constant(-1), new Cot(x));

        if (pow.Base is Sinh sinh && sinh.Argument is Variable vsh && vsh.Name == variable)
            return IntegrateSinhPower(x, exp.Value);

        if (pow.Base is Cosh cosh && cosh.Argument is Variable vch && vch.Name == variable)
            return IntegrateCoshPower(x, exp.Value);

        if (pow.Base is Tanh tanh && tanh.Argument is Variable vth && vth.Name == variable)
            if (System.Math.Abs(exp.Value - 2) < 1e-10)
                return new Add(x, new Multiply(new Constant(-1), new Tanh(x)));

        return null;
    }

    private static Expression? IntegrateSinPower(Variable x, double n)
    {
        if (System.Math.Abs(n - 2) < 1e-10)
            return new Add(
                new Multiply(new Constant(0.5), x),
                new Multiply(new Constant(-0.25), new Sin(new Multiply(new Constant(2), x))));

        if (System.Math.Abs(n - 3) < 1e-10)
            return new Add(
                new Multiply(new Constant(-1), new Cos(x)),
                new Multiply(new Constant(1.0 / 3), new Power(new Cos(x), new Constant(3))));

        if (System.Math.Abs(n - 4) < 1e-10)
            return new Add(
                new Add(
                    new Multiply(new Constant(3.0 / 8), x),
                    new Multiply(new Constant(-0.25), new Sin(new Multiply(new Constant(2), x)))),
                new Multiply(new Constant(1.0 / 32), new Sin(new Multiply(new Constant(4), x))));

        return null;
    }

    private static Expression? IntegrateCosPower(Variable x, double n)
    {
        if (System.Math.Abs(n - 2) < 1e-10)
            return new Add(
                new Multiply(new Constant(0.5), x),
                new Multiply(new Constant(0.25), new Sin(new Multiply(new Constant(2), x))));

        if (System.Math.Abs(n - 3) < 1e-10)
            return new Add(
                new Sin(x),
                new Multiply(new Constant(-1.0 / 3), new Power(new Sin(x), new Constant(3))));

        if (System.Math.Abs(n - 4) < 1e-10)
            return new Add(
                new Add(
                    new Multiply(new Constant(3.0 / 8), x),
                    new Multiply(new Constant(0.25), new Sin(new Multiply(new Constant(2), x)))),
                new Multiply(new Constant(1.0 / 32), new Sin(new Multiply(new Constant(4), x))));

        return null;
    }

    private static Expression? IntegrateTanPower(Variable x, double n)
    {
        if (System.Math.Abs(n - 2) < 1e-10)
            return new Add(new Tan(x), new Multiply(new Constant(-1), x));

        if (System.Math.Abs(n - 3) < 1e-10)
            return new Add(
                new Multiply(new Constant(0.5), new Power(new Tan(x), new Constant(2))),
                new Ln(new Abs(new Cos(x))));

        return null;
    }

    private static Expression? IntegrateSinhPower(Variable x, double n)
    {
        if (System.Math.Abs(n - 2) < 1e-10)
            return new Add(
                new Multiply(new Constant(0.25), new Sinh(new Multiply(new Constant(2), x))),
                new Multiply(new Constant(-0.5), x));
        return null;
    }

    private static Expression? IntegrateCoshPower(Variable x, double n)
    {
        if (System.Math.Abs(n - 2) < 1e-10)
            return new Add(
                new Multiply(new Constant(0.25), new Sinh(new Multiply(new Constant(2), x))),
                new Multiply(new Constant(0.5), x));
        return null;
    }

    #endregion
}
