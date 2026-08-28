using AI.Script.Runtime;
using System.Text;

namespace AI.Script.Binding;

/// <summary>Параметр функции языка.</summary>
public sealed class ScriptParameter
{
    /// <summary>Имя параметра в скрипте.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Тип параметра.</summary>
    public ScriptType Type { get; init; } = ScriptType.Any;

    /// <summary>Описание для манифеста и диагностики.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Есть ли значение по умолчанию.</summary>
    public bool IsOptional { get; init; }

    /// <summary>Значение по умолчанию.</summary>
    public ScriptValue Default { get; init; } = ScriptValue.None;

    /// <summary>Собирает ли параметр остаток позиционных аргументов.</summary>
    public bool IsVariadic { get; init; }

    /// <inheritdoc/>
    public override string ToString()
    {
        string head = IsVariadic ? $"...{Name}" : Name;
        string typed = $"{head}: {Type.ToName()}";

        return IsOptional ? $"{typed} = {ScriptFormatter.Format(Default, quoteStrings: true)}" : typed;
    }
}

/// <summary>
/// Функция языка: описание для манифеста и делегат исполнения.
/// </summary>
/// <remarks>
/// Описание и реализация лежат вместе намеренно (принцип П6 из DESIGN.md): документация
/// выводится из того же объекта, что и вызов, и разойтись с ним не может. В системе, где по
/// документации генерируют код, разошедшаяся документация — не неудобство, а поломка.
/// </remarks>
public sealed class ScriptFunction
{
    /// <summary>Пространство имён.</summary>
    public string Namespace { get; init; } = string.Empty;

    /// <summary>Имя функции.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Полное имя вида <c>ml.kmeans</c>.</summary>
    public string FullName => $"{Namespace}.{Name}";

    /// <summary>Описание.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Пример вызова.</summary>
    public string? Example { get; init; }

    /// <summary>Параметры в порядке объявления.</summary>
    public IReadOnlyList<ScriptParameter> Parameters { get; init; } = [];

    /// <summary>Тип результата.</summary>
    public ScriptType ReturnType { get; init; } = ScriptType.Any;

    /// <summary>Тип-тег результата, если возвращается дескриптор.</summary>
    public string? ReturnHandleType { get; init; }

    /// <summary>Тип-тег дескриптора, методом которого функция является; <c>null</c> — обычная функция.</summary>
    public string? MethodOf { get; init; }

    /// <summary>Делегат исполнения; аргументы уже разложены по порядку параметров.</summary>
    public Func<ScriptValue[], IScriptContext, ValueTask<ScriptValue>> Invoke { get; init; } = null!;

    /// <summary>Число обязательных параметров: столько аргументов можно передать позиционно.</summary>
    public int RequiredCount
    {
        get
        {
            int count = 0;
            foreach (ScriptParameter parameter in Parameters)
            {
                if (parameter.IsOptional || parameter.IsVariadic) break;
                count++;
            }

            return count;
        }
    }

    /// <summary>Есть ли вариадический параметр.</summary>
    public bool IsVariadic
    {
        get
        {
            foreach (ScriptParameter parameter in Parameters)
            {
                if (parameter.IsVariadic) return true;
            }

            return false;
        }
    }

    /// <summary>Текстовая сигнатура для диагностики и манифеста.</summary>
    public string Signature
    {
        get
        {
            var builder = new StringBuilder(FullName).Append('(');

            for (int i = 0; i < Parameters.Count; i++)
            {
                if (i > 0) _ = builder.Append(", ");
                _ = builder.Append(Parameters[i]);
            }

            _ = builder.Append(") -> ").Append(ReturnHandleType != null
                ? $"handle<{ReturnHandleType}>"
                : ReturnType.ToName());

            return builder.ToString();
        }
    }

    /// <summary>Ищет параметр по имени.</summary>
    public ScriptParameter? FindParameter(string name)
    {
        foreach (ScriptParameter parameter in Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.Ordinal)) return parameter;
        }

        return null;
    }

    /// <inheritdoc/>
    public override string ToString() => Signature;
}
