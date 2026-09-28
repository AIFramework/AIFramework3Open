using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>Метод для исполнения: сборка и идентификатор в формате XML doc ID.</summary>
public sealed record MethodRef(string Assembly, string Id);

/// <summary>Запрос исполнителю: пара методов и входы.</summary>
public sealed record BehaviorRequest(MethodRef A, MethodRef B, int Seed, int Cases);

/// <summary>
/// Рабочий процесс (<c>atlas worker &lt;корень&gt;</c>): читает запросы построчно из stdin,
/// исполняет пару через <see cref="BehaviorProbe"/> и пишет итог строкой JSON в stdout.
/// </summary>
/// <remarks>
/// Консоль исполняемого кода заглушена: вывод библиотечного метода не должен попасть в
/// протокол, а чтение с консоли — повиснуть. Зависимости сборок ищутся рядом с ними и в
/// <c>src/*/bin</c>. Снимает процесс хозяин (<see cref="BehaviorHost"/>): тайм-аут внутри
/// процесса зависший метод не остановит.
/// </remarks>
public static class BehaviorWorker
{
    /// <summary>Цикл запросов до конца stdin.</summary>
    public static int Run(string root)
    {
        var protocol = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true };
        var input = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        Console.SetIn(TextReader.Null);

        var folders = new List<string>();
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            string? path = folders.Select(folder => Path.Combine(folder, name.Name + ".dll")).FirstOrDefault(File.Exists) ?? BuiltAssemblies.Find(root, name.Name!);
            return path is null ? null : context.LoadFromAssemblyPath(Found(folders, path));
        };

        while (input.ReadLine() is string line)
        {
            BehaviorResult result;
            try
            {
                BehaviorRequest request = JsonSerializer.Deserialize<BehaviorRequest>(line)!;

                // Запрет проверяется до загрузки: сборку графики или GPU незачем и небезопасно поднимать.
                result = Early(request.A) is string whyA ? BehaviorResult.Unchecked("A: " + whyA)
                    : Early(request.B) is string whyB ? BehaviorResult.Unchecked("B: " + whyB)
                    : Resolve(request.A, folders) is not { } a ? BehaviorResult.Unchecked($"A не найден в сборке: {request.A.Id}")
                    : Resolve(request.B, folders) is not { } b ? BehaviorResult.Unchecked($"B не найден в сборке: {request.B.Id}")
                    : BehaviorProbe.Compare(a, b, request.Seed, request.Cases);
            }
            catch (Exception error)
            {
                result = BehaviorResult.Unchecked($"{error.GetType().Name}: {error.Message}");
            }

            protocol.WriteLine(JsonSerializer.Serialize(result));
        }

        return 0;
    }

    /// <summary>Метод по XML doc ID; <c>null</c> — нет такого или это не метод.</summary>
    public static MethodInfo? Find(Assembly assembly, string id)
    {
        if (!id.StartsWith("M:", StringComparison.Ordinal) || id.Contains("#ctor", StringComparison.Ordinal) || id.Contains("``", StringComparison.Ordinal)) return null;

        int paren = id.IndexOf('(');
        string path = paren < 0 ? id[2..] : id[2..paren];
        int dot = path.LastIndexOf('.');
        if (dot < 0) return null;

        string typeName = path[..dot], name = path[(dot + 1)..];
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        return Types(assembly)
            .Where(type => type.FullName?.Replace('+', '.') == typeName)
            .SelectMany(type => type.GetMethods(all))
            .FirstOrDefault(method => method.Name == name && DocId(method) == id);
    }

    /// <summary>XML doc ID метода, как его пишет компилятор.</summary>
    public static string DocId(MethodInfo method)
    {
        ParameterInfo[] parameters = method.GetParameters();
        string list = parameters.Length == 0 ? "" : "(" + string.Join(",", parameters.Select(p => DocName(p.ParameterType))) + ")";
        return $"M:{method.DeclaringType!.FullName!.Replace('+', '.')}.{method.Name}{list}";
    }

    private static string DocName(Type type)
    {
        if (type.IsByRef) return DocName(type.GetElementType()!) + "@";
        if (type.IsArray)
        {
            int rank = type.GetArrayRank();
            return DocName(type.GetElementType()!) + (rank == 1 ? "[]" : "[" + string.Join(",", Enumerable.Repeat("0:", rank)) + "]");
        }
        if (type.IsGenericParameter) return (type.DeclaringMethod != null ? "``" : "`") + type.GenericParameterPosition;
        if (type.IsGenericType)
        {
            string definition = type.GetGenericTypeDefinition().FullName!;
            return definition[..definition.IndexOf('`')].Replace('+', '.') + "{" + string.Join(",", type.GetGenericArguments().Select(DocName)) + "}";
        }

        return type.FullName!.Replace('+', '.');
    }

    private static string? Early(MethodRef method) =>
        BehaviorProbe.RefusedAssembly(Path.GetFileNameWithoutExtension(method.Assembly))
        ?? (method.Id.Contains("``", StringComparison.Ordinal) ? "обобщённый метод" : null);

    private static MethodInfo? Resolve(MethodRef method, List<string> folders)
    {
        string name = Path.GetFileNameWithoutExtension(method.Assembly);
        Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(loaded => loaded.GetName().Name == name)
            ?? AssemblyLoadContext.Default.LoadFromAssemblyPath(Found(folders, Path.GetFullPath(method.Assembly)));

        return Find(assembly, method.Id);
    }

    private static string Found(List<string> folders, string path)
    {
        string folder = Path.GetDirectoryName(path)!;
        if (!folders.Contains(folder)) folders.Add(folder);
        return path;
    }

    private static IEnumerable<Type> Types(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException error)
        {
            return error.Types.OfType<Type>();
        }
    }
}
