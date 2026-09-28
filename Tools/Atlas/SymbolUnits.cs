using AI.Script.Binding;
using Microsoft.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Символ Roslyn → единица индекса.
/// </summary>
/// <remarks>
/// Одно преобразование на исходники и на сборки из пакета: идентификатор, сигнатура и
/// комментарий строятся по символу, а не по тексту. Поэтому метод из исходника и тот же метод
/// из DLL получают одинаковый ключ, и индекс проекта на пакете сравним с индексом библиотеки.
/// </remarks>
internal static class SymbolUnits
{
    private static readonly SymbolDisplayFormat TypeFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private static readonly SymbolDisplayFormat SignatureFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeExplicitInterface
            | SymbolDisplayMemberOptions.IncludeRef,
        kindOptions: SymbolDisplayKindOptions.IncludeTypeKeyword,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeExtensionThis,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>
    /// Попадает ли символ в индекс: типы и методы, как их видит пользователь сборки.
    /// </summary>
    /// <remarks>
    /// Правило одно для исходников и для DLL, иначе составы разойдутся на пустом месте. Члены,
    /// которые компилятор достраивает у записей, помечены <c>CompilerGenerated</c> и в индекс не
    /// идут. Конструктор по умолчанию у класса, наоборот, идёт: в DLL он неотличим от
    /// написанного, и на него ссылаются вызовы <c>new T()</c>.
    /// </remarks>
    public static bool IsUnit(ISymbol symbol)
    {
        if (symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "CompilerGeneratedAttribute")) return false;

        return symbol switch
        {
            IMethodSymbol { IsImplicitlyDeclared: true } method => IsDefaultConstructor(method),
            _ when symbol.IsImplicitlyDeclared => false,
            INamedTypeSymbol type => type.TypeKind is TypeKind.Class or TypeKind.Struct or TypeKind.Interface or TypeKind.Enum,
            IMethodSymbol { MethodKind: MethodKind.Ordinary } method => method.CanBeReferencedByName,
            IMethodSymbol method => method.MethodKind is MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion,
            _ => false,
        };
    }

    /// <summary>Строит единицу.</summary>
    /// <param name="symbol">Тип или метод, для которого <see cref="IsUnit"/> истинно.</param>
    /// <param name="file">Файл от корня репозитория либо имя DLL.</param>
    /// <param name="line">Строка объявления; ноль, если исходника нет.</param>
    /// <param name="body">Тело метода; пусто, если его нет.</param>
    public static CodeUnit From(ISymbol symbol, string file, int line, string body)
    {
        string signature = symbol.ToDisplayString(SignatureFormat);
        string doc = Doc(symbol);

        return new CodeUnit(
            symbol.ContainingAssembly.Name,
            symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString(),
            Kind(symbol),
            Access(symbol),
            file,
            line,
            signature,
            doc,
            body,
            ScriptName(symbol),
            Hash(signature + "\n" + doc + "\n" + body),
            symbol is IMethodSymbol returning ? Returns(returning) : "",
            symbol is IMethodSymbol method ? string.Join('|', method.Parameters.Where(p => p.RefKind != RefKind.Out).Select(p => TypeKey(p.Type))) : "",
            symbol is INamedTypeSymbol type ? string.Join('|', Supertypes(type).Select(TypeKey)) : "");
    }

    /// <summary>
    /// Ключ типа для графа типов: полное имя без <c>global::</c>, со встроенными псевдонимами
    /// (<c>double[]</c>) и без пометки допустимости null, чтобы <c>Vector</c> и <c>Vector?</c>
    /// были одной вершиной.
    /// </summary>
    public static string TypeKey(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(TypeFormat);

    /// <summary>Что метод отдаёт: тип результата, у конструктора — созданный тип.</summary>
    private static string Returns(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Constructor ? TypeKey(method.ContainingType) : TypeKey(method.ReturnType);

    /// <summary>Базовые типы и интерфейсы, кроме <c>object</c>: по ним выход одного метода подходит ко входу другого.</summary>
    private static IEnumerable<ITypeSymbol> Supertypes(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type.BaseType; current is { SpecialType: not SpecialType.System_Object }; current = current.BaseType)
            yield return current;

        foreach (INamedTypeSymbol contract in type.AllInterfaces) yield return contract;
    }

    /// <summary>
    /// Видимость снаружи сборки с учётом вложенности: открытый метод внутреннего типа — внутренний.
    /// </summary>
    public static string Access(ISymbol symbol)
    {
        string access = "public";

        for (ISymbol? current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingSymbol)
        {
            switch (current.DeclaredAccessibility)
            {
                case Accessibility.Private:
                    return "private";
                case Accessibility.Internal or Accessibility.ProtectedAndInternal:
                    access = "internal";
                    break;
            }
        }

        return access;
    }

    /// <summary>Отпечаток текста: шестнадцать шестнадцатеричных знаков SHA-256.</summary>
    public static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0, 8).ToLowerInvariant();

    private static bool IsDefaultConstructor(IMethodSymbol method) =>
        method is { MethodKind: MethodKind.Constructor, IsStatic: false, Parameters.Length: 0 }
        && method.ContainingType is { TypeKind: TypeKind.Class, IsStatic: false };

    private static string Kind(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol => "type",
        IMethodSymbol { MethodKind: MethodKind.Constructor } => "ctor",
        IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion } => "operator",
        _ => "method",
    };

    /// <summary>
    /// XML-комментарий без обёртки <c>member</c>; битый XML сохраняется как есть. У функции
    /// AIScript без комментария описанием служат описание и пример из <c>[ScriptFn]</c>: справку
    /// языка пишут там, и без этого поиск не нашёл бы её по смыслу.
    /// </summary>
    private static string Doc(ISymbol symbol)
    {
        string xml = symbol.GetDocumentationCommentXml() ?? "";

        if (xml.Length == 0) return ScriptDoc(symbol);

        try
        {
            XElement member = XElement.Parse(xml);
            return string.Concat(member.Nodes()).Trim();
        }
        catch (System.Xml.XmlException)
        {
            return xml.Trim();
        }
    }

    /// <summary>
    /// Имя функции AIScript: <c>пространство.функция</c> по атрибутам модуля и метода.
    /// </summary>
    /// <remarks>
    /// Имя по умолчанию выводится тем же <see cref="ScriptModule.ToSnakeCase"/>, что и в самом
    /// языке, иначе обёртка без явного имени получила бы в индексе не то имя, под которым её зовут.
    /// </remarks>
    private static string? ScriptName(ISymbol symbol)
    {
        if (symbol is not IMethodSymbol method) return null;

        AttributeData? function = Attribute(method, nameof(ScriptFnAttribute));
        AttributeData? module = method.ContainingType is null ? null : Attribute(method.ContainingType, nameof(ScriptModuleAttribute));

        if (function is null || module is null || module.ConstructorArguments.Length == 0) return null;

        string? name = function.ConstructorArguments.Length > 0 ? function.ConstructorArguments[0].Value as string : null;

        return $"{module.ConstructorArguments[0].Value}.{name ?? ScriptModule.ToSnakeCase(method.Name)}";
    }

    private static string ScriptDoc(ISymbol symbol)
    {
        if (Attribute(symbol, nameof(ScriptFnAttribute)) is not { } function) return "";

        string description = function.ConstructorArguments.Length > 1 ? function.ConstructorArguments[1].Value as string ?? "" : "";
        string? example = function.NamedArguments.FirstOrDefault(argument => argument.Key == nameof(ScriptFnAttribute.Example)).Value.Value as string;

        var doc = new XElement("doc", new XElement("summary", description));
        if (!string.IsNullOrWhiteSpace(example)) doc.Add(new XElement("example", example));

        return description.Length == 0 && example is null ? "" : string.Concat(doc.Nodes());
    }

    private static AttributeData? Attribute(ISymbol symbol, string name) =>
        symbol.GetAttributes().FirstOrDefault(attribute => attribute.AttributeClass?.Name == name);
}
