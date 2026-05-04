using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.Core.Integrations
{
    public static class SymbolicIntegrator
    {
        /// <summary>
        /// Символьное интегрирование выражения
        /// </summary>
        public static Expression Integrate(Expression expr, string variable)
        {
            expr = expr.Simplify();

            // Константа: \int c dx = c*x
            if (expr is Constant c)
                return new Multiply(c, new Variable(variable));

            // Переменная интегрирования: \int x dx = x^2/2
            if (expr is Variable v && v.Name == variable)
                return new Multiply(
                    new Constant(0.5),
                    new Power(new Variable(variable), new Constant(2))
                );

            // Другая переменная: \int y dx = y*x
            if (expr is Variable v2)
                return new Multiply(v2, new Variable(variable));

            // Сумма: \int (f + g) dx = \int f dx + \int g dx
            if (expr is Add add)
                return new Add(
                    Integrate(add.Left, variable),
                    Integrate(add.Right, variable)
                );

            // Умножение на константу: \int c*f dx = c*\int f dx
            if (expr is Multiply mult)
            {
                if (mult.Left is Constant c1 && !ContainsVariable(mult.Right, variable))
                    return new Multiply(c1, new Multiply(mult.Right, new Variable(variable)));

                if (mult.Left is Constant c2)
                    return new Multiply(c2, Integrate(mult.Right, variable));

                if (mult.Right is Constant c3)
                    return new Multiply(c3, Integrate(mult.Left, variable));

                // Общий случай умножения - пытаемся применить интегрирование по частям
                return IntegrateByParts(mult, variable);
            }

            // Степень: \int x^n dx = x^(n+1)/(n+1), n ≠ -1
            if (expr is Power pow)
            {
                if (pow.Base is Variable vb && vb.Name == variable &&
                    pow.Exponent is Constant exp)
                {
                    if (System.Math.Abs(exp.Value + 1) < 1e-10)
                    {
                        // \int x^(-1) dx = ln|x|
                        return new Ln(new Variable(variable));
                    }

                    return new Multiply(
                        new Constant(1.0 / (exp.Value + 1)),
                        new Power(
                            new Variable(variable),
                            new Constant(exp.Value + 1)
                        )
                    );
                }
            }

            // Экспонента: \int e^x dx = e^x
            if (expr is Exp exp2)
            {
                if (exp2.Argument is Variable ve && ve.Name == variable)
                    return exp2;

                // \int e^(ax) dx = (1/a)*e^(ax), a != 0
                if (exp2.Argument is Multiply m &&
                    m.Left is Constant ca &&
                    m.Right is Variable vem && vem.Name == variable &&
                    System.Math.Abs(ca.Value) > 1e-12)
                {
                    return new Multiply(
                        new Constant(1.0 / ca.Value),
                        exp2
                    );
                }
            }

            // Синус: \int sin(x) dx = -cos(x)
            if (expr is Sin sin)
            {
                if (sin.Argument is Variable vs && vs.Name == variable)
                    return new Multiply(new Constant(-1), new Cos(sin.Argument));

                // \int sin(ax) dx = -(1/a)*cos(ax), a != 0
                if (sin.Argument is Multiply ms &&
                    ms.Left is Constant cas &&
                    ms.Right is Variable vsm && vsm.Name == variable &&
                    System.Math.Abs(cas.Value) > 1e-12)
                {
                    return new Multiply(
                        new Constant(-1.0 / cas.Value),
                        new Cos(sin.Argument)
                    );
                }
            }

            // Косинус: \int cos(x) dx = sin(x)
            if (expr is Cos cos)
            {
                if (cos.Argument is Variable vc && vc.Name == variable)
                    return new Sin(cos.Argument);

                // \int cos(ax) dx = (1/a)*sin(ax), a != 0
                if (cos.Argument is Multiply mc &&
                    mc.Left is Constant cac &&
                    mc.Right is Variable vcm && vcm.Name == variable &&
                    System.Math.Abs(cac.Value) > 1e-12)
                {
                    return new Multiply(
                        new Constant(1.0 / cac.Value),
                        new Sin(cos.Argument)
                    );
                }
            }

            // 1/x: \int 1/x dx = ln|x|
            if (expr is Power pow2 &&
                pow2.Base is Variable vp && vp.Name == variable &&
                pow2.Exponent is Constant ce && ce.Value == -1)
            {
                return new Ln(new Variable(variable));
            }

            return AdvancedIntegrationEngine.Integrate(expr, variable);
        }

        /// <summary>
        /// Интегрирование по частям: \int u dv = uv - \int v du
        /// </summary>
        private static Expression IntegrateByParts(Multiply expr, string variable)
        {
            // Простейший случай: \int x * e^x dx
            if (expr.Left is Variable v && v.Name == variable && expr.Right is Exp)
            {
                var u = expr.Left;
                var dv = expr.Right;
                var du = u.Derivative(variable);
                var vIntegrated = Integrate(dv, variable);

                return new Add(
                    new Multiply(u, vIntegrated),
                    new Multiply(
                        new Constant(-1),
                        Integrate(new Multiply(vIntegrated, du), variable)
                    )
                ).Simplify();
            }

            throw new NotImplementedException(
                "Интегрирование по частям для данного выражения не реализовано");
        }

        /// <summary>
        /// Проверяет, содержит ли выражение указанную переменную.
        /// Делегирует к <see cref="ExpressionEvaluator.CollectVariables"/>.
        /// </summary>
        private static bool ContainsVariable(Expression expr, string variable)
        {
            var vars = new HashSet<string>();
            ExpressionEvaluator.CollectVariables(expr, vars);
            return vars.Contains(variable);
        }

        /// <summary>
        /// Определённый интеграл (численный расчёт для проверки).
        /// Делегирует вычисление к <see cref="ExpressionEvaluator"/>.
        /// </summary>
        public static double DefiniteIntegral(
            Expression expr,
            string variable,
            double a,
            double b,
            int steps = 10000)
        {
            double h = (b - a) / steps;
            double sum = 0;
            var vars = new Dictionary<string, double>();

            for (int i = 0; i < steps; i++)
            {
                vars[variable] = a + i * h;
                sum += ExpressionEvaluator.Evaluate(expr, vars);
            }

            return sum * h;
        }
    }
}