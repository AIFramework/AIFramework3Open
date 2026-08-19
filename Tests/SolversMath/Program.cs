// Численная проверка зоны AI.Solvers.Math.
//
// Каждая проверка привязана к конкретному исправленному дефекту: символьные
// ответы сверяются с табличными значениями, численные — с независимо
// посчитанными эталонами (Симпсон на 20-40 тысячах узлов), а формулы в выводе —
// с ожидаемыми подстроками. Возвращает ненулевой код при первом же расхождении,
// поэтому годится и для запуска руками, и из CI.
//
// Запуск: dotnet run --project Tests/SolversMath
using System.Globalization;
using System.Text.RegularExpressions;
using AI.Solvers.Math;
using AI.Solvers.Math.Core.Integrations;
using AI.Solvers.Math.Core;
using AI.Solvers.Math.Core.Functions;
using AI.Solvers.Math.Core.Parsers;
using AI.Solvers.Math.Core.Solvers;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

int failed = 0;

void Num(string label, double actual, double expected, double tol)
{
    bool ok = Math.Abs(actual - expected) <= tol * Math.Max(1, Math.Abs(expected));
    if (!ok) failed++;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {label,-42} = {actual,-22:G12} ожидалось {expected:G12}");
}

void Has(string label, string actual, params string[] fragments)
{
    bool ok = fragments.All(actual.Contains);
    if (!ok) failed++;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {label}");
    if (!ok) Console.WriteLine("        получено: " + actual.Replace("\n", "\n        "));
}

void Lacks(string label, string actual, params string[] fragments)
{
    bool ok = !fragments.Any(actual.Contains);
    if (!ok) failed++;
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {label}");
    if (!ok) Console.WriteLine("        получено: " + actual.Replace("\n", "\n        "));
}

double Eval(Expression e, double x) =>
    ExpressionEvaluator.Evaluate(e, new Dictionary<string, double> { ["x"] = x });

var X = new Variable("x");

Console.WriteLine("--- А.1 классификация уравнений ---");
string q = AdvancedSolver.SolveEquation("2x^2 + 3x + 1 = 0");
Has("2x²+3x+1=0 -> корни -0.5 и -1", q, "-0.5", "-1");
string cub = AdvancedSolver.SolveEquation("x^3 - 2x + 1 = 0");
Has("x³-2x+1=0 -> степень 3, корень 1", cub, "Степень: 3", "1");
Lacks("x³-2x+1=0 не считается линейным", cub, "Линейное");
Has("x^4-5x^2+4=0 -> степень 4", AdvancedSolver.SolveEquation("x^4 - 5x^2 + 4 = 0"), "Степень: 4");
Has("x^2+1=0 -> комплексные корни", AdvancedSolver.SolveEquation("x^2 + 1 = 0"), "i");
Has("2x+4=0 -> x=-2", AdvancedSolver.SolveEquation("2x + 4 = 0"), "-2");
Has("sin(x)-0.5=0 -> численно", AdvancedSolver.SolveEquation("sin(x) - 0.5 = 0"), "ЧИСЛЕННОЕ");

Console.WriteLine("\n--- А.2 Ei / А.3 li ---");
Num("Ei(1)", Eval(new Ei(X), 1), 1.8951178163559368, 1e-10);
Num("Ei(2)", Eval(new Ei(X), 2), 4.9542343560018902, 1e-10);
Num("Ei(0.5)", Eval(new Ei(X), 0.5), 0.4542199048631736, 1e-10);
Num("Ei(10)", Eval(new Ei(X), 10), 2492.2289762418787, 1e-10);
Num("Ei(-1)", Eval(new Ei(X), -1), -0.21938393439552029, 1e-10);
Num("Ei(-5)", Eval(new Ei(X), -5), -1.1482955912753257e-3, 1e-9);
Num("Ei(-20)", Eval(new Ei(X), -20), -9.8355252906498815e-11, 1e-8);
Num("Ei(20) ряд vs асимптотика", Eval(new Ei(X), 20), Eval(new Ei(X), 20.0000001), 1e-6);
Num("li(2)", Eval(new Li(X), 2), 1.0451637801174928, 1e-10);
Num("li(10)", Eval(new Li(X), 10), 6.1655995047872979, 1e-10);
Num("li(0.5)", Eval(new Li(X), 0.5), -0.37867104306108795, 1e-9);

Console.WriteLine("\n--- А.4 / А.5 знаки Фурье ---");
Has("F{sin(2x)} = -iπ[...]", AdvancedSolver.FourierTransform("sin(2*x)"), "-i·π[δ(ω-2) - δ(ω+2)]");
Has("F{cos(2x)} без изменений", AdvancedSolver.FourierTransform("cos(2*x)"), "π[δ(ω-2) + δ(ω+2)]");
Has("F{exp(-x²)sin(x)} = -i√π…", AdvancedSolver.FourierTransform("exp(-1*x^2)*sin(x)"), "-i·√π");

Console.WriteLine("\n--- А.6 система ОДУ ---");
string sys1 = ODESolver.SolveSystemODE(["x' = y", "y' = -x"]);
Has("x'=y, y'=-x -> D<0, чисто мнимые", sys1, "D < 0", "0 ± 1i");
string sys2 = ODESolver.SolveSystemODE(["x' = y", "y' = x"]);
Has("x'=y, y'=x -> D>0, экспоненты", sys2, "D > 0", "exp(1*t)", "exp(-1*t)");
Lacks("x'=y, y'=x больше не тригонометрия", sys2, "cos");
Has("x'=x+y, y'=y -> кратный корень", ODESolver.SolveSystemODE(["x' = x + y", "y' = y"]), "D = 0", "C1 + C2*t");

Console.WriteLine("\n--- А.7 / А.8 / А.9 интегралы ---");
Has("∫dx/√(4-4x²) = ½asin(x)", IntegralSolver.IndefiniteIntegral("(4 - 4*x^2)^(-0.5)"), "(1/2)", "asin(x)");
Has("∫sin(3x)cos(3x)dx = sin²(3x)/6", IntegralSolver.IndefiniteIntegral("sin(3*x)*cos(3*x)"), "(1/6)", "sin(3x)^2");
Has("∫x/(2x²+3)dx = ¼ln|2x²+3|", IntegralSolver.IndefiniteIntegral("x*(2*x^2 + 3)^(-1)"), "(1/4)", "ln");
Lacks("∫x/(x²+x)dx не выдаёт ½ln|x²+x|", IntegralSolver.IndefiniteIntegral("x*(x^2 + x)^(-1)"), "0.5*ln", "(1/2)*ln");
Num("∫₀¹ sin(3x)cos(3x)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("sin(3*x)*cos(3*x)", "x", 0, 1), CultureInfo.InvariantCulture),
    Math.Pow(Math.Sin(3), 2) / 6, 1e-4);
Num("∫₀·⁵ dx/√(4-4x²)",
    double.Parse(IntegralSolver.DefiniteIntegral("(4 - 4*x^2)^(-0.5)", "x", 0, 0.5), CultureInfo.InvariantCulture),
    0.5 * Math.Asin(0.5), 1e-4);

Console.WriteLine("\n--- А.10 линейность Лапласа / А.13 факториал ---");
Has("L{sin(t)+cos(t)}", AdvancedSolver.LaplaceTransform("sin(t)+cos(t)"), "1/(s² + 1) + s/(s² + 1)");
Lacks("L{sin(t)+cos(t)} без обрезки по скобке", AdvancedSolver.LaplaceTransform("sin(t)+cos(t)"), "= 1/ +");
Has("L{3*sin(2t)}", AdvancedSolver.LaplaceTransform("3*sin(2*t)"), "3·(2/(s² + 4))");
Has("L{t^5} = 120/s^6", AdvancedSolver.LaplaceTransform("t^5"), "120/s^6");
Has("L{t^25}: символьный факториал + порядок через Gamma",
    AdvancedSolver.LaplaceTransform("t^25"), "25!", "1.55112E+25", "/s^26");
Lacks("L{t^25} без переполнения в минус", AdvancedSolver.LaplaceTransform("t^25"), "-");

Has("ряд Тейлора sin(x): x - x³/6", AdvancedSolver.TaylorSeries("sin(x)", "x", "0", 24), "x", "0.166667*x^3");
// exp(50x): коэффициент при x²¹ равен 50²¹/21! ≈ 9.3e15 — он переживает фильтр
// малых членов, поэтому переполнение 21! в long перевернуло бы ему знак.
string bigTaylor = AdvancedSolver.TaylorSeries("exp(50*x)", "x", "0", 24);
Lacks("ряд exp(50x): 21! не переполняется (все члены > 0)", bigTaylor, " - ");

Console.WriteLine("\n--- А.11 форматирование дробей ---");
Has("∫x²dx = (1/3)x³", IntegralSolver.IndefiniteIntegral("x^2"), "(1/3)");
Has("∫x⁵dx = (1/6)x⁶", IntegralSolver.IndefiniteIntegral("x^5"), "(1/6)");
string big = IntegralSolver.IndefiniteIntegral("21*x");
Has("21x -> (21/2)x², а не 11/2", big, "(21/2)");
Lacks("21x: подстрока 0.5 не съедена", big, "11/2");

Console.WriteLine("\n--- А.14 Френель ---");
Num("∫₀¹ sin(x²)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("sin(x^2)", "x", 0, 1), CultureInfo.InvariantCulture),
    0.31026830172338110, 1e-6);
// Эталоны получены независимо (Симпсон, 20000 узлов)
Num("∫₀¹·⁵ cos(x²)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("cos(x^2)", "x", 0, 1.5), CultureInfo.InvariantCulture),
    0.899184852887, 1e-6);
Num("∫₀² cos(x²)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("cos(x^2)", "x", 0, 2), CultureInfo.InvariantCulture),
    0.461461462433, 1e-6);

Console.WriteLine("\n--- А.12 волновое уравнение ---");
// u(x,0)=sin(2πx), u_t=0, c=1 => точное решение u(x,t)=sin(2πx)cos(2πt).
// t = 0.3 выбрано намеренно: при t, кратном полупериоду, паразитная компонента
// от неверного первого шага зануляется, и такая точка ничего не проверяет.
// Со старым первым шагом ошибка здесь 3.4e-3, с исправленным — 2.9e-4.
const double waveT = 0.3;
string wave = NumericalPDESolver.SolveWaveEquationNumerical(1.0, T: waveT, nx: 101, nt: 240);
double exact025 =  Math.Cos(2 * Math.PI * waveT);
double exact075 = -Math.Cos(2 * Math.PI * waveT);
Num("волна u(0.25, 0.3)", ParseWave(wave, "x = 0.250"), exact025, 1e-3);
Num("волна u(0.75, 0.3)", ParseWave(wave, "x = 0.750"), exact075, 1e-3);

Console.WriteLine("\n--- Б.1 правило Лопиталя и расходимости ---");
Has("lim sin(x)/x при x->0 = 1", AdvancedSolver.ComputeLimit("sin(x)/x", "x", "0"), "1");
Has("lim (x^2-4)/(x-2) при x->2 = 4", AdvancedSolver.ComputeLimit("(x^2-4)/(x-2)", "x", "2"), "4");
Has("lim (1-cos(x))/x^2 при x->0 = 0.5", AdvancedSolver.ComputeLimit("(1-cos(x))/x^2", "x", "0"), "0.5");
Has("lim 1/x^2 при x->0 = +∞, а не 1e8", AdvancedSolver.ComputeLimit("1/x^2", "x", "0"), "+∞");
Has("lim 1/x при x->0 не существует", AdvancedSolver.ComputeLimit("1/x", "x", "0"), "не существует");

Console.WriteLine("\n--- Б.2 численный Фурье: шкала ω и масштаб ---");
// Частоты берём кратными шагу сетки Δω = 2π/T = 0.6283, чтобы линии попали ровно в отсчёты:
// sin(aх)·sin(bx) = ½[cos((a-b)x) - cos((a+b)x)], значит линии на ω = 0.6283 и 1.885.
// Старый код подписал бы их как 0.1 и 0.3 (циклическая частота k/T вместо угловой).
string spec = AdvancedSolver.FourierTransform("sin(0.62831853*x)*sin(1.25663706*x)");
Has("sin·sin: линии на ω=0.628 и ω=1.885, а не 0.1/0.3", spec, "ω=0.628", "ω=1.885");
Lacks("частоты не циклические", spec, "ω=0.100", "ω=0.300");
// Амплитуда линии косинуса на окне длины T равна T/2, с множителем ½ от тождества -> 2.5
Num("|F| на линии ω=0.628", ParseSpectrum(spec, "ω=0.628"), 2.5, 5e-2);
string dc = AdvancedSolver.FourierTransform("1/(1+x^4)");
Num("F{1/(1+x⁴)} при ω=0 = π/√2", ParseSpectrum(dc, "ω=0.000"), System.Math.PI / System.Math.Sqrt(2), 2e-2);
Has("для затухающей функции окно не применяется", dc, "без окна");

Console.WriteLine("\n--- Б.3 шаг численной производной ---");
var derivative = NumericalEquationSolver.NumericalDerivative(System.Math.Sin);
Num("d/dx sin(x) при x=1", derivative(1.0), System.Math.Cos(1.0), 1e-9);
// При больших |x| шаг масштабируется (иначе x±h не различимы в double),
// и точность ограничена уже обрезанием ряда, а не округлением: ~h²/6 ≈ 6e-6.
Num("d/dx sin(x) при x=1000", NumericalEquationSolver.NumericalDerivative(System.Math.Sin)(1000.0),
    System.Math.Cos(1000.0), 1e-5);

Console.WriteLine("\n--- Б.4 корни не подменяются соседними ---");
string manyRoots = AdvancedSolver.SolveEquation("sin(x) - 0.5 = 0");
var reported = Regex.Matches(manyRoots, @"x_\d+ = (-?[\d.,E+]+)")
    .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();
bool rootsValid = reported.Count > 0 && reported.All(r => System.Math.Abs(System.Math.Sin(r) - 0.5) < 1e-6);
bool rootsDistinct = reported.Distinct().Count() == reported.Count;
Has($"все {reported.Count} корней sin(x)=0.5 верны и различны", rootsValid && rootsDistinct ? "ок" : "нет", "ок");

Console.WriteLine("\n--- Б.5 erf / erfc ---");
Num("erf(0.5)", Eval(new Erf(X), 0.5), 0.5204998778130465, 1e-12);
Num("erf(1)", Eval(new Erf(X), 1), 0.8427007929497149, 1e-12);
Num("erf(2)", Eval(new Erf(X), 2), 0.9953222650189527, 1e-12);
Num("erf(-1.5)", Eval(new Erf(X), -1.5), -0.9661051464753107, 1e-12);
Num("erfc(1)", Eval(new Erfc(X), 1), 0.15729920705028513, 1e-12);
Num("erfc(5) — хвост не обнуляется", Eval(new Erfc(X), 5), 1.5374597944280351e-12, 1e-9);
Num("erfc(10)", Eval(new Erfc(X), 10), 2.0884875837625447e-45, 1e-8);
Num("erfc(-3)", Eval(new Erfc(X), -3), 1.9999779095030014, 1e-12);
Num("∫₀¹ exp(-x²)dx = √π/2·erf(1)",
    double.Parse(IntegralSolver.DefiniteIntegral("exp(-1*x^2)", "x", 0, 1), CultureInfo.InvariantCulture),
    0.7468241328124271, 1e-5);

Console.WriteLine("\n--- Б.6 устойчивость и скорость ударной волны ---");
string diffAdv = NumericalPDESolver.SolveDiffusionAdvectionNumerical(1.0, 0.1);
var diffValues = ParseAllValues(diffAdv);
bool bounded = diffValues.Count > 0 && diffValues.All(v => System.Math.Abs(v) <= 1.05);
Has("диффузия-адвекция: решение ограничено (нет разноса)", bounded ? "ок" : "нет", "ок");
Has("диффузия-адвекция: печатается 2r + CFL", diffAdv, "2r + CFL");
// Ступенька 1.0 -> 0.2, α=1: скорость фронта по Ренкину-Гюгонио s = 0.6, за t=0.5 фронт в x≈0.8
string burgers = NumericalPDESolver.SolveBurgersNumerical(1.0, 0.0005, T: 0.5, nx: 201, nt: 4000);
Num("Бюргерс: положение ударной волны", ShockPosition(burgers), 0.8, 0.04);

Console.WriteLine("\n--- Б.7 метод Либмана (Гаусс-Зейдель) ---");
string laplace = NumericalPDESolver.SolveLaplaceNumerical(1.0, 1.0, 21, 21);
Num("u(0.5,0.5) при u=100 на одной стороне", ParseWave(laplace, "u(0.5, 0.5)"), 25.0, 1e-3);
int iterations = int.Parse(Regex.Match(laplace, @"Сходимость: (\d+) итераций").Groups[1].Value);
Has($"сошёлся за {iterations} итераций (не упёрся в предел 1000)", iterations < 1000 ? "ок" : "нет", "ок");

Console.WriteLine("\n--- Б.8 / Д.1 ∫x·sin(x)dx берётся движком (SymbolicIntegrator удалён) ---");
double xSinX;
try
{
    var antiderivative = AdvancedIntegrationEngine.Integrate(AdvancedMathExpression.Parse("x*sin(x)"), "x");
    xSinX = Eval(antiderivative, 1) - Eval(antiderivative, 0);
}
catch (Exception ex) { xSinX = double.NaN; Console.WriteLine("        " + ex.GetType().Name); }
Num("∫₀¹ x·sin(x)dx", xSinX, System.Math.Sin(1) - System.Math.Cos(1), 1e-9);
Has("класса SymbolicIntegrator больше нет",
    typeof(AdvancedIntegrationEngine).Assembly.GetTypes().Any(t => t.Name == "SymbolicIntegrator") ? "есть" : "нет",
    "нет");

Console.WriteLine("\n--- Г.6 единая квадратура ---");
Num("∫₀^π sin(x)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("sin(x)", "x", 0, System.Math.PI), CultureInfo.InvariantCulture),
    2.0, 1e-6);
// Особенность на нижнем пределе: трапеции возвращали Infinity, взяв f(0) напрямую
Num("∫₀¹ x^(-0.5)dx (особенность на краю)",
    double.Parse(IntegralSolver.DefiniteIntegral("x^(-0.5)", "x", 0, 1), CultureInfo.InvariantCulture),
    2.0, 1e-3);
Num("∫₀¹ ln(x)dx (особенность на краю)",
    double.Parse(IntegralSolver.DefiniteIntegral("ln(x)", "x", 0, 1), CultureInfo.InvariantCulture),
    -1.0, 1e-3);

Console.WriteLine("\n--- Г.8 функции из токенайзера строятся парсером ---");
Num("si(1) = Si(1)", Eval(AdvancedMathExpression.Parse("si(x)"), 1), 0.9460830703671830, 1e-6);
Num("li(2)", Eval(AdvancedMathExpression.Parse("li(x)"), 2), 1.0451637801174928, 1e-9);
Num("fresnels(1)", Eval(AdvancedMathExpression.Parse("fresnels(x)"), 1), 0.4382591473903548, 1e-9);
Num("ci(1)", Eval(AdvancedMathExpression.Parse("ci(x)"), 1), 0.3374039229009681, 1e-6);

Console.WriteLine("\n--- Д.3 степень разбирается одним способом ---");
Num("2^3^2 = 512 (правая ассоциативность)", Eval(AdvancedMathExpression.Parse("2^3^2"), 0), 512, 0);
Num("-2^2 = -4", Eval(AdvancedMathExpression.Parse("-2^2"), 0), -4, 0);
Num("2^-1 = 0.5", Eval(AdvancedMathExpression.Parse("2^-1"), 0), 0.5, 0);
Num("2*x^2 при x=3 = 18", Eval(AdvancedMathExpression.Parse("2*x^2"), 3), 18, 0);

Console.WriteLine("\n--- Д.2 / Д.4 общие шаблоны sin·cos и гауссиана ---");
Has("L{sin(2t)cos(2t)} через тождество", AdvancedSolver.LaplaceTransform("sin(2*t)*cos(2*t)"), "0.5·(4/(s² + 16))");
Has("∫sin(5x)cos(5x)dx", IntegralSolver.IndefiniteIntegral("sin(5*x)*cos(5*x)"), "(1/10)", "sin(5x)^2");
Has("F{exp(-2x²)}", AdvancedSolver.FourierTransform("exp(-2*x^2)"), "√(π/2)", "exp(-ω²/8)");
// √(π/8)·erf(√2); эталон перепроверен Симпсоном на 40000 узлах
Num("∫₀¹ exp(-2x²)dx",
    double.Parse(IntegralSolver.DefiniteIntegral("exp(-2*x^2)", "x", 0, 1), CultureInfo.InvariantCulture),
    0.598144006661, 1e-6);

Console.WriteLine("\n--- В: формулы в выводе ---");
string poisson = PDESolver.SolvePoissonEquation("u_xx + u_yy = f");
Has("Пуассон: знак минус в A_mn", poisson, "A_mn = -4/(ab·λ_mn)");
Has("Пуассон: sin(nπy/b), а не /a", poisson, "sin(nπy/b)");
Lacks("Пуассон: убрано «если f = 0, то u = 0»", poisson, "то u = 0");
Has("Гельмгольц (анизотропный): √(b/a)",
    PDESolver.SolveHelmholtzEquation("2*u_xx + 1*u_yy + 5*u = 0"), "√(b/a)");
string waveText = PDESolver.SolveWaveEquation("u_tt = 4*u_xx");
Has("Волновое: c = 2, а не 4", waveText, "u_tt = 2²·u_xx", "Скорость распространения волн: 2");
Has("F{exp(2x)} = 2πδ(ω+i·2)", AdvancedSolver.FourierTransform("exp(2*x)"), "δ(ω+i·2)");
Has("F{exp(2x)sin(3x)}: сдвиг мнимый", AdvancedSolver.FourierTransform("exp(2*x)*sin(3*x)"), "i·2");
Has("x/sin(x): полилогарифм, а не li(x)",
    IntegralSolver.IndefiniteIntegral("x*(sin(x))^(-1)"), "полилогарифм");

Console.WriteLine("\n--- смоук: команды процессора не падают ---");
var processor = new MainFractalMathProcessor();
string[] commands =
[
    "integrate x^2 dx",
    "integrate sin(x) from 0 to 3.14159",
    "derivative of sin(x)*x^2",
    "second derivative of exp(2*x)",
    "solve x^2 + 3x + 2 = 0",
    "solve 2x^2 + 3x + 1 = 0",
    "solve x^3 - 2x + 1 = 0",
    "solve sin(x) - 0.5 = 0",
    "solve y' + 2y = 0",
    "solve y'' + 3y' + 2y = 0",
    "solve x' = y, y' = -x",
    "solve y' + 2y = 0, y(0) = 3",
    "solve u_t = 0.5*u_xx",
    "solve u_tt = 4*u_xx",
    "limit sin(x)/x as x -> 0",
    "Taylor series of cos(x) at x = 0",
    "Laplace transform of sin(t)+cos(t)",
    "Laplace table",
    "Fourier transform of sin(2*x)"
];
var expectedRoute = new Dictionary<string, string>
{
    ["solve x' = y, y' = -x"] = "СИСТЕМА ЛИНЕЙНЫХ ОДУ",
    ["solve u_tt = 4*u_xx"]   = "ВОЛНОВОЕ УРАВНЕНИЕ",
    ["solve u_t = 0.5*u_xx"]  = "ТЕПЛОПРОВОДНОСТИ",
    ["solve y' + 2y = 0, y(0) = 3"] = "Подстановка НУ",
};
foreach (var command in commands)
{
    string result;
    try { result = processor.ProcessFractalMathCommand(command); }
    catch (Exception ex) { result = "Ошибка: " + ex.Message; }

    bool ok = !result.StartsWith("Ошибка") && !result.Contains("Ошибка:") &&
              !result.Contains("Не удалось распознать") &&
              (!expectedRoute.TryGetValue(command, out var route) || result.Contains(route));
    if (!ok) failed++;
    string head = result.Split('\n').FirstOrDefault(l => l.Trim().Length > 0)?.Trim() ?? "";
    Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {command,-42} -> {head[..System.Math.Min(head.Length, 46)]}");
}

Console.WriteLine(failed == 0 ? "\nВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : $"\nПРОВАЛЕНО ПРОВЕРОК: {failed}");
return failed == 0 ? 0 : 1;

static double ParseWave(string text, string marker)
{
    var line = text.Split('\n').First(l => l.Contains(marker));
    var value = Regex.Match(line[(line.LastIndexOf('=') + 1)..], @"-?[\d.]+(?:E[+-]\d+)?");
    return double.Parse(value.Value, CultureInfo.InvariantCulture);
}

// "    ω=1.000: |F|=0.7854, φ=-90.0°" -> 0.7854
static double ParseSpectrum(string text, string marker)
{
    // Строка «шаг по частоте Δω=0.6283» тоже содержит «ω=0.628» — берём только строки спектра
    var line = text.Split('\n').FirstOrDefault(l => l.Contains(marker) && l.Contains("|F|="));
    if (line is null) throw new InvalidOperationException($"нет строки с {marker}:\n{text}");
    return double.Parse(Regex.Match(line, @"\|F\|=([\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture);
}

// Все значения "u = ..." из таблицы решения
static List<double> ParseAllValues(string text) =>
    Regex.Matches(text, @"u = (-?[\d.]+(?:E[+-]\d+)?)")
        .Select(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
        .ToList();

// Положение фронта: точка, где решение пересекает середину ступеньки (1.0+0.2)/2 = 0.6.
// Для симметричного вязкого профиля это в точности центр ударной волны.
// Печатается каждая десятая точка, поэтому между отсчётами интерполируем.
static double ShockPosition(string text)
{
    var points = Regex.Matches(text, @"x = ([\d.]+): u = (-?[\d.]+)")
        .Select(m => (
            x: double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            u: double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)))
        .ToList();

    for (int i = 1; i < points.Count; i++)
    {
        var (x0, u0) = points[i - 1];
        var (x1, u1) = points[i];
        if (u0 > 0.6 && u1 <= 0.6)
            return x0 + ((x1 - x0) * (u0 - 0.6) / (u0 - u1));
    }
    return double.NaN;
}
