namespace AI.Script.Binding;

/// <summary>
/// Помечает класс как модуль языка: его функции попадают в пространство имён <see cref="Name"/>.
/// </summary>
/// <remarks>
/// Схема повторяет <c>AgentToolAttribute</c> из <c>AI.LLM</c> сознательно: одна привычная
/// модель расширения на весь фреймворк дешевле двух, каждая из которых «лучше» в своём углу.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ScriptModuleAttribute : Attribute
{
    /// <summary>Имя пространства имён: <c>ml</c>, <c>dsp</c>, <c>core</c>.</summary>
    public string Name { get; }

    /// <summary>Описание модуля для манифеста и <c>help</c>.</summary>
    public string Description { get; }

    /// <summary>Версия модуля; входит в манифест и в ключ кэша стадий.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Помечает класс как модуль языка.</summary>
    /// <param name="name">Имя пространства имён.</param>
    /// <param name="description">Описание модуля.</param>
    public ScriptModuleAttribute(string name, string description = "")
    {
        Name = name;
        Description = description ?? string.Empty;
    }
}

/// <summary>Помечает метод как функцию языка.</summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScriptFnAttribute : Attribute
{
    /// <summary>Имя функции в скрипте; <c>null</c> — имя метода в snake_case.</summary>
    public string? Name { get; }

    /// <summary>Описание для манифеста и диагностики.</summary>
    public string Description { get; }

    /// <summary>Пример вызова; попадает в манифест.</summary>
    public string? Example { get; set; }

    /// <summary>Тип-тег результата, если функция возвращает дескриптор.</summary>
    public string? Returns { get; set; }

    /// <summary>Помечает метод как функцию языка.</summary>
    /// <param name="name">Имя функции; <c>null</c> — из имени метода.</param>
    /// <param name="description">Описание.</param>
    public ScriptFnAttribute(string? name = null, string description = "")
    {
        Name = name;
        Description = description ?? string.Empty;
    }
}

/// <summary>
/// Объявляет функцию методом дескриптора: <c>model.predict(x)</c> вместо <c>ml.predict(model, x)</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ScriptMethodAttribute : Attribute
{
    /// <summary>Тип-тег дескриптора, для которого функция является методом.</summary>
    public string HandleType { get; }

    /// <summary>Объявляет функцию методом дескриптора.</summary>
    /// <param name="handleType">Тип-тег дескриптора, например <c>ml.kmeans</c>.</param>
    public ScriptMethodAttribute(string handleType) => HandleType = handleType;
}

/// <summary>Описывает параметр функции языка.</summary>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class ScriptParamAttribute : Attribute
{
    /// <summary>Описание параметра.</summary>
    public string Description { get; }

    /// <summary>Имя параметра в скрипте; <c>null</c> — имя параметра метода в snake_case.</summary>
    public string? Name { get; set; }

    /// <summary>Собирает ли параметр остаток позиционных аргументов.</summary>
    public bool Variadic { get; set; }

    /// <summary>Описывает параметр функции языка.</summary>
    /// <param name="description">Описание параметра.</param>
    public ScriptParamAttribute(string description) => Description = description;
}
