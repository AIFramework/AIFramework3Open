using AI.Script.Binding;
using AI.Script.Docs;
using AI.Script.Hosting;
using AI.Script.Llm;
using AI.Script.Semantics;
using AI.Script.Syntax;
using AI.Script.Syntax.Ast;
using System.Text;

namespace AI.Script.UnitTests;

/// <summary>
/// Единообразие языка: то, что делает его понятным и человеку, и модели.
/// </summary>
/// <remarks>
/// Понятность — не свойство, о котором договариваются на словах: с ростом библиотеки она
/// теряется по одной функции за раз, и каждая потеря по отдельности выглядит безобидно.
/// Здесь она закреплена проверками, которые считают, а не рассуждают.
/// <para>
/// Самая важная из них — что каждый пример из справки разбирается и вызывает существующие
/// функции с существующими аргументами. Примеры — это то, что модель копирует дословно;
/// неверный пример учит неверному, и цена ошибки здесь выше, чем у любой другой строки
/// документации.
/// </para>
/// </remarks>
public sealed class LanguageStyleTests
{
    private static readonly ScriptHost Host = Script.FullHost();

    private static IReadOnlyList<ScriptFunction> Functions
    {
        get
        {
            var all = new List<ScriptFunction>();

            foreach (IScriptModule module in Host.Registry.Modules) all.AddRange(module.Functions);

            return all;
        }
    }

    // --- имена ---

    /// <summary>
    /// Имена функций и параметров — латиница в snake_case.
    /// </summary>
    /// <remarks>
    /// Кириллица в именах языка разрешена автору скрипта ([§18.2] проекта), но не самой
    /// библиотеке: имя, которое нужно набирать в другой раскладке, модель угадывает хуже, а
    /// человек путает с похожей латинской буквой.
    /// </remarks>
    [Fact]
    public void Names_AreAsciiSnakeCase()
    {
        var wrong = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (!IsSnakeCase(function.Name)) wrong.Add(function.FullName);

            foreach (ScriptParameter parameter in function.Parameters)
            {
                if (!IsSnakeCase(parameter.Name)) wrong.Add($"{function.FullName}({parameter.Name})");
            }
        }

        Assert.True(wrong.Count == 0, "имена не в змеином регистре латиницей:\n  " + string.Join("\n  ", wrong));
    }

    [Fact]
    public void Namespaces_AreShortAndAscii()
    {
        foreach (IScriptModule module in Host.Registry.Modules)
        {
            Assert.True(IsSnakeCase(module.Name), $"пространство '{module.Name}' не в змеином регистре");
            Assert.True(module.Name.Length <= 7, $"пространство '{module.Name}' длиннее семи символов");
        }
    }

    // --- описания ---

    [Fact]
    public void EveryFunction_HasDescription()
    {
        var missing = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (string.IsNullOrWhiteSpace(function.Description)) missing.Add(function.FullName);
        }

        Assert.True(missing.Count == 0, "без описания:\n  " + string.Join("\n  ", missing));
    }

    // Проверки на длину описания здесь нет намеренно. Она была написана и отвергнута:
    // «Квадратный корень» — исчерпывающее описание из двух слов, и любой порог по длине
    // требовал бы разбавлять его словами ради счётчика. Единственная защита от описания,
    // переписывающего имя другими буквами, — та, что ниже: одноимённые функции обязаны
    // различаться, иначе выбрать между ними по справке нельзя.

    [Fact]
    public void EveryParameter_HasDescription()
    {
        var missing = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            foreach (ScriptParameter parameter in function.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter.Description))
                    missing.Add($"{function.FullName}({parameter.Name})");
            }
        }

        Assert.True(missing.Count == 0, "параметры без описания:\n  " + string.Join("\n  ", missing));
    }

    // --- примеры ---

    [Fact]
    public void EveryFunction_HasExample()
    {
        var missing = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (string.IsNullOrWhiteSpace(function.Example)) missing.Add(function.FullName);
        }

        Assert.True(missing.Count == 0, "без примера:\n  " + string.Join("\n  ", missing));
    }

    /// <summary>Пример обязан разбираться: модель копирует его дословно.</summary>
    [Fact]
    public void EveryExample_Parses()
    {
        var broken = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (string.IsNullOrWhiteSpace(function.Example)) continue;

            var text = new SourceText(function.Example, function.FullName);
            var diagnostics = new DiagnosticBag(text);

            _ = new Parser(text, diagnostics).ParseUnit();

            if (!diagnostics.HasErrors) continue;

            broken.Add($"{function.FullName}: {First(diagnostics)}");
        }

        Assert.True(broken.Count == 0, "примеры не разбираются:\n  " + string.Join("\n  ", broken));
    }

    /// <summary>
    /// Пример вызывает существующие функции с существующими аргументами.
    /// </summary>
    /// <remarks>
    /// Ровно та проверка, которой не хватало, когда менялась сигнатура <c>plot.line</c>:
    /// подпись поменялась, примеры остались прежними, и модель по ним писала вызов, который
    /// не проходил проверку. Здесь такое расхождение падает тестом в ту же минуту.
    /// </remarks>
    [Fact]
    public void EveryExample_CallsRealFunctionsWithRealArguments()
    {
        var wrong = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (string.IsNullOrWhiteSpace(function.Example)) continue;

            var text = new SourceText(function.Example, function.FullName);
            var diagnostics = new DiagnosticBag(text);
            ScriptUnit unit = new Parser(text, diagnostics).ParseUnit();

            if (diagnostics.HasErrors) continue;

            foreach (CallExpr call in Calls(unit))
            {
                string? problem = Check(call);

                if (problem != null) wrong.Add($"{function.FullName}: {problem}");
            }
        }

        Assert.True(wrong.Count == 0, "примеры зовут несуществующее:\n  " + string.Join("\n  ", wrong));
    }

    /// <summary>
    /// Имена переменных в примерах — латиницей, как и всё остальное в библиотеке.
    /// </summary>
    /// <remarks>
    /// Примеры — то, чему модель подражает. Когда четыре сотни из них пишут <c>let m = ...</c>,
    /// а дюжина <c>let сеть = ...</c>, подражание становится случайным. Автор скрипта
    /// по-прежнему волен называть свои переменные по-русски — но библиотека показывает один
    /// стиль, а не два.
    /// </remarks>
    [Fact]
    public void Examples_UseLatinIdentifiers()
    {
        var mixed = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (string.IsNullOrWhiteSpace(function.Example)) continue;

            var text = new SourceText(function.Example, function.FullName);
            var diagnostics = new DiagnosticBag(text);
            ScriptUnit unit = new Parser(text, diagnostics).ParseUnit();

            if (diagnostics.HasErrors) continue;

            foreach (Stmt statement in unit.Statements)
            {
                string? name = statement switch
                {
                    LetStmt let => let.Name,
                    EmitStmt emit => emit.Name,
                    _ => null,
                };

                if (name == null || IsSnakeCase(name)) continue;

                mixed.Add($"{function.FullName}: '{name}'");
            }
        }

        Assert.True(mixed.Count == 0, "кириллица в именах примеров:\n  " + string.Join("\n  ", mixed));
    }

    // --- единообразие ---

    /// <summary>
    /// Выбор варианта называется одинаково во всех пространствах.
    /// </summary>
    /// <remarks>
    /// Там, где строкой выбирают разновидность операции, параметр зовётся <c>kind</c>.
    /// Синонимы вроде <c>type</c>, <c>mode</c> и <c>method</c> заставляют модель угадывать
    /// имя, которое она не может вывести из смысла, — а ошибка в имени именованного
    /// аргумента стоит целой попытки.
    /// </remarks>
    [Fact]
    public void VariantParameter_IsAlwaysCalledKind()
    {
        string[] synonyms = ["type", "mode", "variant", "algorithm"];
        var wrong = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            foreach (ScriptParameter parameter in function.Parameters)
            {
                if (Array.IndexOf(synonyms, parameter.Name) < 0) continue;

                wrong.Add($"{function.FullName}({parameter.Name}) — должно быть 'kind'");
            }
        }

        Assert.True(wrong.Count == 0, "разнобой в выборе варианта:\n  " + string.Join("\n  ", wrong));
    }

    /// <summary>
    /// Одинаковые по смыслу параметры называются одинаково.
    /// </summary>
    /// <remarks>
    /// Пары собраны из того, что уже встречалось в языке: частота дискретизации везде
    /// <c>fs</c>, зерно — <c>seed</c>, порог — <c>threshold</c>. Список закрытый: он
    /// перечисляет принятые имена, а не запрещает все остальные.
    /// </remarks>
    [Theory]
    [InlineData("sample_rate", "fs")]
    [InlineData("samplerate", "fs")]
    [InlineData("rng", "seed")]
    [InlineData("random_seed", "seed")]
    [InlineData("thresh", "threshold")]
    [InlineData("cnt", "count")]
    [InlineData("num", "count")]
    [InlineData("iterations", "epochs")]
    public void SynonymousParameters_UseTheAcceptedName(string forbidden, string accepted)
    {
        var wrong = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            foreach (ScriptParameter parameter in function.Parameters)
            {
                if (!string.Equals(parameter.Name, forbidden, StringComparison.Ordinal)) continue;

                wrong.Add(function.FullName);
            }
        }

        Assert.True(wrong.Count == 0, $"'{forbidden}' вместо '{accepted}':\n  " + string.Join("\n  ", wrong));
    }

    /// <summary>
    /// Одноимённые функции разных пространств различаются описанием.
    /// </summary>
    /// <remarks>
    /// Совпадение имён само по себе не беда — <c>vec.zeros</c> и <c>mat.zeros</c> делают одно
    /// и то же с разными предметами, и это как раз понятно. Беда — когда два <c>min</c>
    /// описаны одинаково: тогда выбрать между ними по справке невозможно, и модель берёт
    /// первое попавшееся.
    /// </remarks>
    [Fact]
    public void SameName_DifferentNamespaces_AreDistinguishableByDescription()
    {
        var byName = new Dictionary<string, List<ScriptFunction>>(StringComparer.Ordinal);

        foreach (ScriptFunction function in Functions)
        {
            if (!byName.TryGetValue(function.Name, out List<ScriptFunction>? group))
                byName[function.Name] = group = [];

            group.Add(function);
        }

        var clashes = new List<string>();

        foreach (var pair in byName)
        {
            if (pair.Value.Count < 2) continue;

            var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (ScriptFunction function in pair.Value)
            {
                if (seen.TryGetValue(function.Description, out string? previous))
                {
                    clashes.Add($"{previous} и {function.FullName}: «{function.Description}»");
                    continue;
                }

                seen[function.Description] = function.FullName;
            }
        }

        Assert.True(
            clashes.Count == 0,
            "одинаковое имя и одинаковое описание — выбрать нельзя:\n  " + string.Join("\n  ", clashes));
    }

    /// <summary>
    /// Тип результата в сигнатуре не врёт: дескриптор объявляется дескриптором, запись — записью.
    /// </summary>
    /// <remarks>
    /// Атрибут <c>Returns</c> задаёт тип-тег дескриптора, а не имя типа языка. Написанное в нём
    /// <c>"record"</c> печаталось в справке как <c>handle&lt;record&gt;</c> — модель читала, что
    /// функция вернёт дескриптор, и обращалась к нему методом вместо поля. Тег дескриптора
    /// всегда двусоставной: <c>ml.kmeans</c>, <c>search.index</c>.
    /// </remarks>
    [Fact]
    public void HandleReturnTags_LookLikeHandleTags()
    {
        var wrong = new List<string>();

        foreach (ScriptFunction function in Functions)
        {
            if (function.ReturnHandleType is not string tag) continue;
            if (tag.Contains('.', StringComparison.Ordinal)) continue;

            wrong.Add($"{function.FullName} -> handle<{tag}>");
        }

        Assert.True(
            wrong.Count == 0,
            "имя типа языка вместо тега дескриптора:\n  " + string.Join("\n  ", wrong));
    }

    // --- поля записей ---

    /// <summary>
    /// Имена полей возвращаемых записей — латиница в snake_case, как и всё остальное в языке.
    /// </summary>
    /// <remarks>
    /// Проверяется вызовом, а не разбором исходников: поля записи рождаются в момент возврата,
    /// и статически их не видно. Список вызовов ведётся руками — цена за то, что проверка
    /// говорит о настоящем поведении, а не о том, что написано в коде рядом.
    /// <para>
    /// Правило важнее, чем кажется. Библиотека, где одно пространство отвечает полями
    /// <c>x_train</c>, а соседнее — <c>окупаемость_периодов</c>, заставляет и человека, и
    /// модель помнить не смысл, а язык каждого пространства. Имена в самом скрипте автор
    /// по-прежнему волен писать по-русски: это его текст, а не словарь библиотеки.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData("econ.unit(marketing: 100, customers: 10, revenue: 50, churn: 0.2)")]
    [InlineData("econ.appraise(<-100, 60, 60>, rate: 0.1)")]
    [InlineData("econ.break_even(price: 10, variable_cost: 5, fixed_costs: 100, volume: 50)")]
    [InlineData("econ.loan(principal: 1000, rate: 0.1, periods: 12)")]
    [InlineData("econ.runway(cash: 1000, revenue: 0, costs: 100, horizon: 12, simulations: 50)")]
    [InlineData("econ.drawdown(signal.noise(50, sigma: 0.1))")]
    [InlineData("econ.performance(signal.noise(50, sigma: 0.1))")]
    [InlineData("econ.forecast(vec.linspace(1, 50, n: 50), horizon: 3)")]
    [InlineData("econ.theta(vec.linspace(1, 50, n: 50), horizon: 3)")]
    [InlineData("econ.elasticity(<100, 120, 140, 160, 180>, <50, 42, 36, 31, 28>)")]
    [InlineData("mw.waveguide(2.45e9)")]
    [InlineData("mw.material(\"вода\")")]
    [InlineData("mw.antenna(\"horn\", frequency: 2.45e9)")]
    [InlineData("siglab.iq(<1, 0, 1, 1>, kind: \"qpsk\")")]
    [InlineData("siglab.constellation(\"bpsk\")")]
    [InlineData("siglab.quadrature(signal.sine(signal.time(0.02, fs: 48000), freq: 2000), " +
        "carrier: 2000, fs: 48000)")]
    [InlineData("cv.sobel(mat.eye(8))")]
    [InlineData("chem.formula(\"H2O\")")]
    [InlineData("ml.split(mat.eye(8), <0, 1, 0, 1, 0, 1, 0, 1>)")]
    [InlineData("dsp.fft(signal.sine(signal.time(0.05, fs: 1000), freq: 50), fs: 1000)")]
    [InlineData("ctrl.identify(signal.noise(60, sigma: 1), signal.noise(60, sigma: 1), order: 2).describe()")]
    public void RecordFields_AreAsciiSnakeCase(string call)
    {
        RunResult result = Script.RunWith(Host, $"emit r = {call}", new RunOptions { Seed = 3 });

        Assert.True(result.Success, Script.Report(result));

        var record = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result.Emitted["r"]);

        Assert.NotEmpty(record);

        foreach (string field in record.Keys)
            Assert.True(IsSnakeCase(field), $"{call}: поле '{field}' не в змеином регистре латиницей");
    }

    /// <summary>Записи, собранные скриптом, остаются его делом: там кириллица разрешена.</summary>
    [Fact]
    public void RecordFields_InUserScripts_MayBeCyrillic()
    {
        RunResult result = Script.RunWith(Host, "emit r = { выручка: 100, клиентов: 5 }.выручка");

        Assert.True(result.Success, Script.Report(result));
        Assert.Equal(100.0, result.Emitted["r"]);
    }

    // --- манифест ---

    /// <summary>
    /// Индекс перечисляет все пространства и помещается в промпт.
    /// </summary>
    /// <remarks>
    /// Первое, что читает модель. Пространство, которого нет в индексе, для неё не
    /// существует — сколько бы функций в нём ни было.
    /// </remarks>
    [Fact]
    public void Index_ListsEveryNamespace()
    {
        string index = Host.DescribeCapabilities(ManifestOptions.Index);

        foreach (IScriptModule module in Host.Registry.Modules)
            Assert.Contains($"**{module.Name}**", index, StringComparison.Ordinal);
    }

    /// <summary>
    /// Карточка языка держится в объёме, ради которого она существует.
    /// </summary>
    /// <remarks>
    /// Карточка лежит в системном промпте целиком и всегда — в отличие от справки, которую
    /// запрашивают по надобности. Именно поэтому она разрастается незаметно: каждое новое
    /// пространство хочется описать «хотя бы одной строкой», и через пять этапов правила языка
    /// тонут в перечне библиотеки. Место перечня — индекс и <c>help</c>, место карточки —
    /// правила, которые нельзя узнать, спросив.
    /// <para>
    /// Порог в 8 тыс. знаков — это около 2.5 тыс. токенов на кириллице: заявленный в самой
    /// карточке ориентир с запасом примерно в десятую часть.
    /// </para>
    /// </remarks>
    [Fact]
    public void PromptCard_StaysWithinItsBudget()
    {
        string card = ScriptPrompt.Card;

        Assert.False(string.IsNullOrWhiteSpace(card), "карточка языка не найдена в ресурсах сборки");
        Assert.True(card.Length < 8000, $"карточка разрослась до {card.Length} знаков");
    }

    /// <summary>
    /// Карточка учит правилам языка, а не перечисляет библиотеку.
    /// </summary>
    /// <remarks>
    /// Признак сползания — списки функций в карточке. Одно-два имени как иллюстрация правила
    /// уместны; перечень из десяти означает, что каталог переехал туда, где ему не место.
    /// </remarks>
    [Fact]
    public void PromptCard_TeachesRulesNotCatalogue()
    {
        var crowded = new List<string>();

        foreach (string line in ScriptPrompt.Card.Split('\n'))
        {
            int names = 0;
            int from = 0;

            while ((from = line.IndexOf('`', from)) >= 0)
            {
                int end = line.IndexOf('`', from + 1);

                if (end < 0) break;

                names++;
                from = end + 1;
            }

            if (names > 12) crowded.Add(line.Trim());
        }

        Assert.True(crowded.Count == 0, "строки-перечни в карточке:\n  " + string.Join("\n  ", crowded));
    }

    /// <summary>
    /// Справка по функции содержит всё, что нужно для вызова.
    /// </summary>
    /// <remarks>
    /// Второй уровень манифеста — то, что модель запрашивает, столкнувшись с незнакомым
    /// именем. Если в ответе нет описания аргументов, следующий её шаг — угадывание.
    /// </remarks>
    [Fact]
    public void Help_ShowsSignatureArgumentsAndExample()
    {
        string help = Host.Describe("econ.appraise");

        Assert.Contains("econ.appraise(", help, StringComparison.Ordinal);
        Assert.Contains("обязательный", help, StringComparison.Ordinal);
        Assert.Contains("Пример", help, StringComparison.Ordinal);
    }

    // --- внутреннее ---

    private static string? Check(CallExpr call)
    {
        if (call.Callee is not MemberExpr { Target: NameExpr root } member) return null;

        // Метод дескриптора: имя слева — переменная, а не пространство имён.
        if (!Host.Registry.HasNamespace(root.Name)) return null;

        string full = $"{root.Name}.{member.Name}";
        ScriptFunction? function = Host.Registry.Find(full);

        if (function == null) return $"нет функции '{full}'";

        foreach (ArgumentNode argument in call.Arguments)
        {
            if (argument.Name == null) continue;

            bool known = false;

            foreach (ScriptParameter parameter in function.Parameters)
            {
                if (!string.Equals(parameter.Name, argument.Name, StringComparison.Ordinal)) continue;

                known = true;
                break;
            }

            if (!known) return $"у '{full}' нет аргумента '{argument.Name}'";
        }

        return null;
    }

    /// <summary>Все вызовы в разобранном примере, включая вложенные и звенья конвейера.</summary>
    private static List<CallExpr> Calls(ScriptUnit unit)
    {
        var found = new List<CallExpr>();

        foreach (Stmt statement in unit.Statements) Walk(statement, found);

        return found;
    }

    private static void Walk(object? node, List<CallExpr> found)
    {
        switch (node)
        {
            case null:
                return;

            case CallExpr call:
                found.Add(call);
                Walk(call.Callee, found);

                foreach (ArgumentNode argument in call.Arguments) Walk(argument.Value, found);

                return;

            case PipeExpr pipe:
                Walk(pipe.Left, found);
                Walk(pipe.Right, found);
                return;

            case BinaryExpr binary:
                Walk(binary.Left, found);
                Walk(binary.Right, found);
                return;

            case UnaryExpr unary:
                Walk(unary.Operand, found);
                return;

            case MemberExpr member:
                Walk(member.Target, found);
                return;

            case IndexExpr index:
                Walk(index.Target, found);

                foreach (IndexArgument argument in index.Arguments) Walk(argument.Value, found);

                return;

            case LambdaExpr lambda:
                Walk(lambda.Body, found);
                return;

            case ListExpr list:
                foreach (Expr item in list.Items) Walk(item, found);

                return;

            case VectorExpr vector:
                foreach (Expr item in vector.Items) Walk(item, found);

                return;

            case RecordExpr record:
                foreach (RecordFieldNode field in record.Fields) Walk(field.Value, found);

                return;

            case BlockExpr block:
                foreach (Stmt statement in block.Statements) Walk(statement, found);

                return;

            case IfExpr conditional:
                Walk(conditional.Condition, found);
                Walk(conditional.Then, found);
                Walk(conditional.Else, found);
                return;

            case LetStmt let:
                Walk(let.Value, found);
                return;

            case SetStmt set:
                Walk(set.Value, found);
                return;

            case EmitStmt emit:
                Walk(emit.Value, found);
                return;

            case ShowStmt show:
                Walk(show.Value, found);
                return;

            case AssertStmt assert:
                Walk(assert.Condition, found);
                return;

            case ExpressionStmt expression:
                Walk(expression.Expression, found);
                return;

            case ForStmt loop:
                Walk(loop.Iterable, found);
                Walk(loop.Body, found);
                return;

            default:
                return;
        }
    }

    private static string First(DiagnosticBag diagnostics)
    {
        foreach (Diagnostic diagnostic in diagnostics.ToList())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Error) return diagnostic.Message;
        }

        return "неизвестная ошибка";
    }

    private static bool IsSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        var builder = new StringBuilder();

        foreach (char c in name)
        {
            if (char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '_') continue;

            _ = builder.Append(c);
        }

        return builder.Length == 0 && !char.IsAsciiDigit(name[0]);
    }
}
