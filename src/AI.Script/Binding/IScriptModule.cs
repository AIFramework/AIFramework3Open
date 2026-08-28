namespace AI.Script.Binding;

/// <summary>
/// Модуль языка: пространство имён и его функции.
/// </summary>
/// <remarks>
/// Единственная точка расширения языка. Ядро о содержимом фреймворка не знает ничего:
/// подключение новой библиотеки — это регистрация модуля, а не правка интерпретатора.
/// </remarks>
public interface IScriptModule
{
    /// <summary>Имя пространства имён.</summary>
    string Name { get; }

    /// <summary>Описание модуля.</summary>
    string Description { get; }

    /// <summary>Версия модуля.</summary>
    string Version { get; }

    /// <summary>Функции модуля.</summary>
    IReadOnlyList<ScriptFunction> Functions { get; }
}
