using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Режим «только DLL»: единицы открытого API сборки из пакета, описания — из XML рядом с ней.
/// </summary>
/// <remarks>
/// Для проекта, который подключает библиотеку пакетом NuGet, исходников нет. Сборка читается
/// тем же Roslyn, что и исходники, поэтому ключи и сигнатуры совпадают с индексом по
/// исходникам. Тел нет, и дубли по такой сборке ищутся только по описаниям.
/// </remarks>
public static class PackageReader
{
    /// <summary>Единицы открытого API сборки.</summary>
    /// <param name="assemblyPath">DLL; соседние DLL из той же папки подключаются как зависимости.</param>
    public static IReadOnlyList<CodeUnit> Read(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);

        if (!File.Exists(assemblyPath)) throw new FileNotFoundException("Сборка не найдена.", assemblyPath);

        string xml = Path.ChangeExtension(assemblyPath, ".xml");
        MetadataReference main = MetadataReference.CreateFromFile(
            assemblyPath, documentation: File.Exists(xml) ? XmlDocumentationProvider.CreateFromFile(xml) : null);

        CSharpCompilation compilation = CSharpCompilation.Create("atlas-package", references: [main, .. Dependencies(assemblyPath)]);

        if (compilation.GetAssemblyOrModuleSymbol(main) is not IAssemblySymbol assembly)
            throw new InvalidOperationException($"{Path.GetFileName(assemblyPath)} не читается как сборка .NET.");

        var units = new List<CodeUnit>();
        string file = Path.GetFileName(assemblyPath);

        foreach (INamedTypeSymbol type in Types(assembly.GlobalNamespace))
        {
            // Делегат не единица, и его Invoke/BeginInvoke тоже: в исходнике их нет.
            if (!SymbolUnits.IsUnit(type)) continue;

            // Вложенные типы обходит Types, здесь только сам тип и его методы.
            foreach (ISymbol symbol in type.GetMembers().Where(member => member is not INamedTypeSymbol).Prepend(type))
            {
                if (SymbolUnits.IsUnit(symbol) && SymbolUnits.Access(symbol) == "public")
                    units.Add(SymbolUnits.From(symbol, file, line: 0, body: ""));
            }
        }

        return units;
    }

    private static IEnumerable<INamedTypeSymbol> Types(INamespaceSymbol space)
    {
        foreach (INamespaceOrTypeSymbol member in space.GetMembers())
        {
            IEnumerable<INamedTypeSymbol> found = member switch
            {
                INamespaceSymbol inner => Types(inner),
                INamedTypeSymbol type => Nested(type),
                _ => [],
            };

            foreach (INamedTypeSymbol type in found) yield return type;
        }
    }

    private static IEnumerable<INamedTypeSymbol> Nested(INamedTypeSymbol type) =>
        type.GetTypeMembers().SelectMany(Nested).Prepend(type);

    /// <summary>
    /// Сборки платформы и соседи по папке: без них типы в сигнатурах остаются нераспознанными.
    /// </summary>
    private static IEnumerable<MetadataReference> Dependencies(string assemblyPath)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFileName(assemblyPath) };
        string platform = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "";
        IEnumerable<string> neighbours = Directory.GetFiles(Path.GetDirectoryName(assemblyPath)!, "*.dll");

        foreach (string path in neighbours.Concat(platform.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)))
        {
            if (names.Add(Path.GetFileName(path))) yield return MetadataReference.CreateFromFile(path);
        }
    }
}
