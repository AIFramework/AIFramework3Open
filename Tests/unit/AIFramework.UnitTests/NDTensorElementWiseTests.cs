using System.Reflection;
using AI.DataStructs.Algebraic;
using AI.DataStructs.Shapes;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Поэлементная арифметика <see cref="NDTensor"/>: каждая публичная операция сверяется
/// с простым поэлементным эталоном на нескольких формах и наборах значений.
/// Храповик: новая публичная операция NDTensor без строки в таблице проверки роняет тест.
/// </summary>
public class NDTensorElementWiseTests
{
    // Формы проверки. Ранга 1 здесь нет: конструктор NDTensor на нём падает
    // (IndexOutOfRangeException при заполнении _volumes) — это отдельная ошибка конструктора,
    // а не поэлементных операций; поэлементный проход от ранга не зависит.
    private static readonly int[][] Shapes =
    [
        [1, 1],
        [2, 3],
        [3, 1],
        [2, 2, 2],
        [2, 3, 1, 2],
        [0, 3],
    ];

    // Таблица поэлементных операций: сигнатура метода → эталон над парой чисел
    private static readonly Dictionary<string, Func<double, double, double>> ElementWise = new()
    {
        ["op_Addition(NDTensor, NDTensor)"] = (a, b) => a + b,
        ["op_Addition(Double, NDTensor)"] = (a, b) => a + b,
        ["op_Addition(NDTensor, Double)"] = (a, b) => a + b,
        ["op_Subtraction(NDTensor, NDTensor)"] = (a, b) => a - b,
        ["op_Subtraction(Double, NDTensor)"] = (a, b) => a - b,
        ["op_Subtraction(NDTensor, Double)"] = (a, b) => a - b,
        ["op_Multiply(NDTensor, NDTensor)"] = (a, b) => a * b,
        ["op_Multiply(Double, NDTensor)"] = (a, b) => a * b,
        ["op_Multiply(NDTensor, Double)"] = (a, b) => a * b,
        ["op_Division(NDTensor, NDTensor)"] = (a, b) => a / b,
        ["op_Division(Double, NDTensor)"] = (a, b) => a / b,
        ["op_Division(NDTensor, Double)"] = (a, b) => a / b,
        ["op_Modulus(NDTensor, NDTensor)"] = (a, b) => a % b,
        ["op_Modulus(Double, NDTensor)"] = (a, b) => a % b,
        ["op_Modulus(NDTensor, Double)"] = (a, b) => a % b,
    };

    // Остальные публичные члены: не поэлементная арифметика, причина у каждого
    private static readonly Dictionary<string, string> OtherMembers = new()
    {
        ["get_Data()"] = "доступ к данным",
        ["get_Shape()"] = "доступ к форме",
        ["get_Item(Int32[])"] = "индексатор, доступ к элементу",
        ["set_Item(Int32[], Double)"] = "индексатор, запись элемента",
        ["ToVector()"] = "преобразование типа",
        ["ToTensor()"] = "преобразование типа",
        ["ToMatrix()"] = "преобразование типа",
        ["Reshape(Shape)"] = "изменение формы без пересчёта значений",
        ["FromIAlgStruct(IAlgebraicStructure`1)"] = "создание из другой структуры",
        ["op_Equality(NDTensor, NDTensor)"] = "сравнение, результат bool",
        ["op_Inequality(NDTensor, NDTensor)"] = "сравнение, результат bool",
        ["Equals(Object)"] = "сравнение, результат bool",
        ["Equals(NDTensor)"] = "сравнение, результат bool",
        ["GetHashCode()"] = "хэш-код",
        ["Save(String)"] = "сериализация",
        ["Save(Stream)"] = "сериализация",
        ["GetBytes()"] = "сериализация",
        ["Load(String)"] = "десериализация",
        ["Load(Stream)"] = "десериализация",
        ["FromBytes(Byte[])"] = "десериализация",
        ["FromDataStream(InMemoryDataStream)"] = "десериализация",
    };

    /// <summary>
    /// Сигнатуры поэлементных операций для теории.
    /// </summary>
    public static TheoryData<string> ElementWiseOperations()
    {
        var data = new TheoryData<string>();

        foreach (string signature in ElementWise.Keys)
            data.Add(signature);

        return data;
    }

    [Theory]
    [MemberData(nameof(ElementWiseOperations))]
    public void Operator_AcrossShapesAndValues_MatchesElementWiseReference(string signature)
    {
        MethodInfo method = PublicMembers()[signature];
        Func<double, double, double> reference = ElementWise[signature];
        ParameterInfo[] parameters = method.GetParameters();
        bool leftIsTensor = parameters[0].ParameterType == typeof(NDTensor);
        bool rightIsTensor = parameters[1].ParameterType == typeof(NDTensor);
        var failures = new List<string>();
        int checkedCases = 0;

        foreach (int[] dims in Shapes)
        {
            int count = new Shape(dims).Count;
            double[][] valueSets = ValueSets(count);
            double[] scalars = [0, -2.5, count + 10.75];

            IEnumerable<object> leftOperands = leftIsTensor ? valueSets.Select(values => (object)values) : scalars.Select(s => (object)s);
            IEnumerable<object> rightOperands = rightIsTensor ? valueSets.Select(values => (object)values) : scalars.Select(s => (object)s);

            foreach (object leftValue in leftOperands)
            {
                foreach (object rightValue in rightOperands)
                {
                    checkedCases++;
                    string? failure = CheckCase(method, reference, dims, leftValue, rightValue);

                    if (failure != null)
                        failures.Add(failure);
                }
            }
        }

        Assert.True(checkedCases > 0);
        Assert.True(failures.Count == 0,
            $"{signature}: расхождений с эталоном {failures.Count} из {checkedCases} случаев. Первые:\n"
            + string.Join("\n", failures.Take(5)));
    }

    [Fact]
    public void Operator_TensorsOfDifferentShapes_ThrowsArgumentException()
    {
        var left = new NDTensor(new Shape(2, 3));
        var right = new NDTensor(new Shape(3, 2));

        foreach (string signature in ElementWise.Keys.Where(key => key.EndsWith("(NDTensor, NDTensor)", StringComparison.Ordinal)))
        {
            var error = Assert.Throws<TargetInvocationException>(() => PublicMembers()[signature].Invoke(null, [left, right]));
            Assert.IsType<ArgumentException>(error.InnerException);
        }
    }

    [Fact]
    public void PublicMembers_OfNDTensor_AreAllClassified()
    {
        var members = PublicMembers();

        string[] unclassified = members.Keys
            .Where(signature => !ElementWise.ContainsKey(signature) && !OtherMembers.ContainsKey(signature))
            .Order(StringComparer.Ordinal)
            .ToArray();

        string[] stale = ElementWise.Keys.Concat(OtherMembers.Keys)
            .Where(signature => !members.ContainsKey(signature))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(unclassified.Length == 0,
            "Новые публичные члены NDTensor не внесены ни в таблицу поэлементной проверки (ElementWise), "
            + "ни в список прочих членов (OtherMembers): " + string.Join(", ", unclassified));
        Assert.True(stale.Length == 0, "Строки таблиц без публичного члена NDTensor: " + string.Join(", ", stale));
    }

    // Один случай: операция над заданными операндами против поэлементного эталона.
    // Возвращает описание расхождения или null
    private static string? CheckCase(MethodInfo method, Func<double, double, double> reference, int[] dims, object leftValue, object rightValue)
    {
        var shape = new Shape(dims);
        object left = leftValue is double[] leftData ? Tensor(shape, leftData) : leftValue;
        object right = rightValue is double[] rightData ? Tensor(shape, rightData) : rightValue;
        double[]? leftBefore = (left as NDTensor)?.Data.ToArray();
        double[]? rightBefore = (right as NDTensor)?.Data.ToArray();
        string where = $"форма [{string.Join(", ", dims)}], левый {Describe(leftValue)}, правый {Describe(rightValue)}";

        NDTensor result;

        try
        {
            result = (NDTensor)method.Invoke(null, [left, right])!;
        }
        catch (TargetInvocationException error)
        {
            return $"{where}: исключение {error.InnerException!.GetType().Name}: {error.InnerException.Message}";
        }

        if (result.Shape != shape)
            return $"{where}: форма результата {result.Shape}";

        if (result.Data.Length != shape.Count)
            return $"{where}: в результате {result.Data.Length} элементов вместо {shape.Count}";

        for (int i = 0; i < shape.Count; i++)
        {
            double a = left is NDTensor ? leftBefore![i] : (double)left;
            double b = right is NDTensor ? rightBefore![i] : (double)right;
            double expected = reference(a, b);

            if (!expected.Equals(result.Data[i]))
                return $"{where}: элемент {i}: ожидалось {expected}, получено {result.Data[i]}";
        }

        if (left is NDTensor leftTensor && !leftTensor.Data.SequenceEqual(leftBefore!))
            return $"{where}: операция изменила левый операнд";

        if (right is NDTensor rightTensor && !rightTensor.Data.SequenceEqual(rightBefore!))
            return $"{where}: операция изменила правый операнд";

        return null;
    }

    // Наборы значений тензора из count элементов: нули, отрицательные, больше длины, смешанные
    private static double[][] ValueSets(int count) =>
    [
        new double[count],
        Enumerable.Range(0, count).Select(i => -1.5 * (i + 1)).ToArray(),
        Enumerable.Range(0, count).Select(i => count + 7 + 3.25 * i).ToArray(),
        Enumerable.Range(0, count).Select(i => i % 3 == 0 ? 0 : (i % 2 == 0 ? 1 : -1) * (0.5 + i)).ToArray(),
    ];

    private static NDTensor Tensor(Shape shape, double[] values)
    {
        var tensor = new NDTensor(shape);
        values.CopyTo(tensor.Data, 0);
        return tensor;
    }

    private static string Describe(object operand) =>
        operand is double[] values ? $"тензор [{string.Join("; ", values)}]" : $"скаляр {operand}";

    // Публичные члены NDTensor (без унаследованных от object) по сигнатуре «Имя(Тип, Тип)»
    private static Dictionary<string, MethodInfo> PublicMembers() =>
        typeof(NDTensor)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToDictionary(
                method => $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})",
                method => method);
}
