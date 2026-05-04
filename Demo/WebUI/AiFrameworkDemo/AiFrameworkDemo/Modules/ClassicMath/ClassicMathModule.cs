using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.ClassicMath;

public sealed class ClassicMathModule : ILibraryModule
{
    public string Id => "classic-math";
    public string Name => "AI.ClassicMath";
    public string Description => "Линейная алгебра, комбинаторика, ОДУ, специальные функции, калькулятор, метрики";
    public string Color => "violet";
    public string TutorialFolder => "ClassicMath";
    public string IconSvg => """<svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 9h18M3 15h18M9 3v18M15 3v18"/></svg>""";

    public IReadOnlyList<CategoryDef> Categories { get; } =
    [
        new("algebra", "Линейная алгебра: СЛАУ", "Метод Гаусса и Крамера",
        [
            new("gauss", "Гаусс: x", "Gauss.SolvingEquations", "Gauss", "Algebra.md",
                [
                    new("_matrix","Матрица A",0,0,0,0,Hint:"Строки через ';', элементы через пробел",TextDefault:"2 1 -1;-3 -1 2;-2 1 2"),
                    new("_vector","Вектор b",0,0,0,0,Hint:"Правая часть СЛАУ, числа через пробел",TextDefault:"8 -11 -3"),
                ]),
            new("kramer", "Крамер: x", "Kramer.SolvingEquations", "Kramer", "Algebra.md",
                [
                    new("_matrix","Матрица A",0,0,0,0,Hint:"Строки через ';', элементы через пробел",TextDefault:"2 1;5 3"),
                    new("_vector","Вектор b",0,0,0,0,Hint:"Правая часть СЛАУ, числа через пробел",TextDefault:"4 7"),
                ]),
        ]),
        new("combinatorics", "Комбинаторика", "Размещения, сочетания",
        [
            new("placing", "Размещения A(k,n)", "PlacingWithoutRepetition", "CombinatoricsBaseFunction", "Combinatorics.md",
                [
                    new("N","n",1,30,10,1,Hint:"Общее количество элементов множества"),
                    new("K","k",1,20,3,1,Hint:"Размер выборки (k ≤ n)"),
                ]),
            new("num_combos", "Сочетания (double)", "NumberOfCombinations", "CombinatoricsBaseFunction", "Combinatorics.md",
                [
                    new("N","n",1,30,10,1,Hint:"Общее количество элементов множества"),
                    new("K","k",1,20,3,1,Hint:"Размер выборки (k ≤ n)"),
                ]),
            new("combinations_long", "C(n,k) long", "Combinations", "CombinatoricsBaseFunction", "Combinatorics.md",
                [
                    new("N","n",1,30,10,1,Hint:"Общее количество элементов множества"),
                    new("K","k",1,20,3,1,Hint:"Размер выборки (k ≤ n)"),
                ]),
        ]),
        new("calculator", "AdvancedCalculator", "Разбор и вычисление выражений",
        [
            new("calc_eval", "Выполнить скрипт", "AdvancedCalculator / Processor", "AdvancedCalculator", "Calculator.md",
                [new("_expression","Скрипт",0,0,0,0,Hint:"Переменные, if/else, while, for i=1 to N:, break, continue, // и # комментарии",
                    TextDefault:
                        "// Числа Фибоначчи\na = 0\nb = 1\nfor i = 1 to 12:\n    c = a + b\n    a = b\n    b = c\nb\n\n// Факториал 10\nf = 1\nfor i = 2 to 10:\n    f *= i\nf\n\n// Простое ли 97?\nn = 97\nisPrime = 1\nfor d = 2 to n-1:\n    if n % d == 0:\n        isPrime = 0\n        break\nisPrime")]),
        ]),
        new("analysis_regress", "Метрики регрессии", "MAE, MSE, RMSE, MAPE, R²",
        [
            new("mae", "MAE", "MetricsForRegression.MAE", "MetricsForRegression", "Metrics.md",
                [
                    new("_vector","target",0,0,0,0,Hint:"Истинные значения, через пробел",TextDefault:"1 2 3 4 5 6 7 8 9 10"),
                    new("_vector2","output",0,0,0,0,Hint:"Предсказанные значения, через пробел",TextDefault:"1.1 2.3 2.8 4.2 5.1 5.9 7.2 8.0 9.3 9.8"),
                ]),
            new("mse", "MSE", "MetricsForRegression.MSE", "MetricsForRegression", "Metrics.md",
                [
                    new("_vector","target",0,0,0,0,Hint:"Истинные значения, через пробел",TextDefault:"1 2 3 4 5 6 7 8 9 10"),
                    new("_vector2","output",0,0,0,0,Hint:"Предсказанные значения, через пробел",TextDefault:"1.1 2.3 2.8 4.2 5.1 5.9 7.2 8.0 9.3 9.8"),
                ]),
            new("rmse", "RMSE", "MetricsForRegression.RMSE", "MetricsForRegression", "Metrics.md",
                [
                    new("_vector","target",0,0,0,0,Hint:"Истинные значения, через пробел",TextDefault:"1 2 3 4 5 6 7 8 9 10"),
                    new("_vector2","output",0,0,0,0,Hint:"Предсказанные значения, через пробел",TextDefault:"1.1 2.3 2.8 4.2 5.1 5.9 7.2 8.0 9.3 9.8"),
                ]),
            new("r2", "R²", "MetricsForRegression.R2", "MetricsForRegression", "Metrics.md",
                [
                    new("_vector","target",0,0,0,0,Hint:"Истинные значения, через пробел",TextDefault:"1 2 3 4 5 6 7 8 9 10"),
                    new("_vector2","output",0,0,0,0,Hint:"Предсказанные значения, через пробел",TextDefault:"1.1 2.3 2.8 4.2 5.1 5.9 7.2 8.0 9.3 9.8"),
                ]),
        ]),
        new("matrix", "Матричные разложения", "QR, собственные значения",
        [
            new("qr_q", "Матрица Q", "QR.GetQ", "QR", "Algebra.md",
                [new("_matrix","Матрица A",0,0,0,0,Hint:"Строки через ';', элементы через пробел",TextDefault:"1 2 3;4 5 6;7 8 10")]),
            new("qr_r", "Матрица R", "QR.GetR", "QR", "Algebra.md",
                [new("_matrix","Матрица A",0,0,0,0,Hint:"Строки через ';', элементы через пробел",TextDefault:"1 2 3;4 5 6;7 8 10")]),
            new("eigen_val", "Собственные числа", "EigenValuesVectors.Eigenvalues", "EigenValuesVectors", "Algebra.md",
                [
                    new("_matrix","Матрица A",0,0,0,0,Hint:"Квадратная матрица, строки через ';'",TextDefault:"4 1 0;1 3 1;0 1 2"),
                    new("eigenIter","Итераций",10,200,80,10,Hint:"Максимальное число итераций степенного метода"),
                ]),
        ]),
        new("ode", "Дифференциальные уравнения", "Рунге — Кутта 4-го порядка",
        [
            new("rk4", "Рунге — Кутта 4", "RungeKutta4 (dy/dx=-k·y)", "RungeKutta", "ODE.md",
                [
                    new("odeK","k",0.1,5,0.7,0.1,Hint:"Коэффициент k в уравнении dy/dx = -k·y"),
                    new("odeX0","x₀",-5,5,0,0.5,Hint:"Начальное значение аргумента x₀"),
                    new("odeY0","y₀",0.01,10,1,0.1,Hint:"Начальное условие y(x₀) = y₀"),
                    new("odeXf","x_f",1,20,4,0.5,Hint:"Конечное значение аргумента x (правая граница)"),
                    new("odeStep","Шаг",0.005,0.5,0.05,0.005,Hint:"Шаг интегрирования h (меньше — точнее)"),
                ]),
        ]),
        new("special", "Специальные функции", "Эллиптические интегралы",
        [
            new("elliptic", "K(k) эллиптич.", "CompleteEllipticIntegral_I", "EllipticIntegral", "SpecialFunctions.md",
                [new("ellK","k",0,0.99,0.5,0.01,Hint:"Модуль эллиптического интеграла (0 < k < 1). При k->1 интеграл -> ∞")]),
        ]),
    ];

    public DemoResult RunDemo(string algoKey, IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams, DemoSettings settings)
    {
        try
        {
            string result = MathDemoRunner.Run(algoKey, numericParams, textParams);
            return new DemoResult { TextOutput = result };
        }
        catch (Exception ex)
        {
            return new DemoResult { Error = ex.Message };
        }
    }
}
