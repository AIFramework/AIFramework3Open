using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Operators;
using AI.Solvers.Math.Core.Parsers;

namespace AI.Solvers.Math.CAS;

public static partial class AlgebraicSimplifier
{
    #region Сбор подобных членов

    private static Expression CollectLikeTerms(Expression expr)
    {
        if (expr is not Add) return expr;

        var terms = new Dictionary<string, (double coeff, Expression term)>();
        CollectTermsRecursive(expr, terms);

        Expression? result = null;
        foreach (var (_, (coeff, term)) in terms.OrderByDescending(kv => GetTermPriority(kv.Value.term)))
        {
            if (System.Math.Abs(coeff) < 1e-10) continue;

            // Свободный член собран как (коэффициент, единица) — вернуть его произведением
            // значит напечатать «3*1». Мало того что это мусор в ответе: следующий проход
            // свернёт произведение обратно в тройку, этот развернёт снова, и упрощение будет
            // ходить по кругу до предела итераций, отдавая наружу худший из двух видов.
            Expression newTerm = term is Constant unit && System.Math.Abs(unit.Value - 1) < 1e-10
                                   ? new Constant(coeff)
                               : System.Math.Abs(coeff - 1) < 1e-10 ? term
                               : System.Math.Abs(coeff + 1) < 1e-10 ? new Multiply(new Constant(-1), term)
                               : new Multiply(new Constant(coeff), term);
            result = result is null ? newTerm : new Add(result, newTerm);
        }
        return result ?? new Constant(0);
    }

    private static void CollectTermsRecursive(Expression expr, Dictionary<string, (double, Expression)> terms)
    {
        switch (expr)
        {
            case Add add:
                CollectTermsRecursive(add.Left, terms);
                CollectTermsRecursive(add.Right, terms);
                break;
            case Multiply mult:
            {
                var (coeff, remainder) = ExtractCoefficientFromTerm(mult);
                string key = remainder.ToString();
                terms[key] = terms.TryGetValue(key, out var ex) ? (ex.Item1 + coeff, remainder) : (coeff, remainder);
                break;
            }
            case Constant c:
            {
                const string key = "1";
                terms[key] = terms.TryGetValue(key, out var ex) ? (ex.Item1 + c.Value, new Constant(1)) : (c.Value, new Constant(1));
                break;
            }
            default:
            {
                string key = expr.ToString();
                terms[key] = terms.TryGetValue(key, out var ex) ? (ex.Item1 + 1, expr) : (1, expr);
                break;
            }
        }
    }

    private static (double coeff, Expression remainder) ExtractCoefficientFromTerm(Expression expr)
    {
        // Симметрично извлекаем константу как с левой, так и с правой стороны Multiply,
        // чтобы x*3 и 3*x давали одинаковый ключ группировки подобных членов.
        if (expr is Multiply mult)
        {
            if (mult.Left is Constant cl)
            {
                var (inner, rem) = ExtractCoefficientFromTerm(mult.Right);
                return (cl.Value * inner, rem);
            }
            if (mult.Right is Constant cr)
            {
                var (inner, rem) = ExtractCoefficientFromTerm(mult.Left);
                return (cr.Value * inner, rem);
            }
        }
        if (expr is Constant cv) return (cv.Value, new Constant(1));
        return (1.0, expr);
    }

    private static int GetTermPriority(Expression term) => term switch
    {
        Power pow when pow.Base is Variable && pow.Exponent is Constant c => (int)c.Value,
        Variable => 1,
        Constant => 0,
        _        => -1
    };

    #endregion

    #region Факторизация общих множителей

    private static Expression FactorCommonTerms(Expression expr)
    {
        if (expr is not Add) return expr;

        var terms = new List<Expression>();
        CollectAddTerms(expr, terms);
        if (terms.Count < 2) return expr;

        var termFactors = terms.Select(t => { var fs = new List<Expression>(); CollectMultiplyFactors(t, fs); return fs; }).ToList();

        var commonFactors = new List<Expression>();
        foreach (var factor in termFactors[0])
        {
            if (factor is Constant) continue;
            if (termFactors.Skip(1).All(fs => fs.Any(f => ExpressionsEqual(f, factor))) &&
                !commonFactors.Any(cf => ExpressionsEqual(cf, factor)))
                commonFactors.Add(factor);
        }

        if (commonFactors.Count == 0) return expr;

        var remainingTerms = termFactors.Select(factors =>
        {
            var remaining = factors.Where(f => !commonFactors.Any(cf => ExpressionsEqual(cf, f))).ToList();
            if (remaining.Count == 0) return (Expression)new Constant(1);
            return remaining.Aggregate((a, b) => (Expression)new Multiply(a, b));
        }).ToList();

        var commonProduct  = commonFactors.Aggregate((a, b) => (Expression)new Multiply(a, b));
        var remainingSum   = remainingTerms.Aggregate((a, b) => (Expression)new Add(a, b));
        return new Multiply(commonProduct, remainingSum);
    }

    #endregion
}
