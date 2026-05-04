using System;
using System.IO;

namespace AI.DataStructs;

/// <summary>
/// Устаревший сериализатор на основе BinaryFormatter — удалён из соображений безопасности.
/// BinaryFormatter допускает выполнение произвольного кода (RCE) при десериализации
/// недоверенных данных и признан Microsoft deprecated начиная с .NET 5.
/// </summary>
/// <remarks>
/// Используйте вместо него <see cref="SafeSerializer"/>:
/// <list type="bullet">
///   <item>Для Vector / Matrix / Tensor / ComplexVector / NDTensor —
///         <c>SafeSerializer.SaveBytes(path, obj.GetBytes())</c> /
///         <c>Type.FromBytes(SafeSerializer.LoadBytes(path))</c></item>
///   <item>Для ML-моделей (NN, KNNCl и др.) — используйте методы Save / Load
///         в самих классах, которые внутри вызывают SafeSerializer.</item>
/// </list>
/// </remarks>
[Obsolete("BinarySerializer использует BinaryFormatter (RCE-риск) и удалён. " +
          "Используйте SafeSerializer или методы Save/Load конкретных типов.", error: true)]
public static class BinarySerializer
{
    public static T Load<T>(string filePath) =>
        throw new NotSupportedException(
            "BinarySerializer удалён из-за уязвимости BinaryFormatter (RCE). " +
            "Используйте SafeSerializer или методы Save/Load конкретного типа. " +
            "Файлы, сохранённые старым форматом, необходимо пересохранить.");

    public static T Load<T>(Stream stream) =>
        throw new NotSupportedException(
            "BinarySerializer удалён из-за уязвимости BinaryFormatter (RCE). " +
            "Используйте SafeSerializer или методы Save/Load конкретного типа.");

    public static void Save<T>(string filePath, T data) =>
        throw new NotSupportedException(
            "BinarySerializer удалён из-за уязвимости BinaryFormatter (RCE). " +
            "Используйте SafeSerializer или методы Save/Load конкретного типа.");

    public static void Save<T>(Stream stream, T data) =>
        throw new NotSupportedException(
            "BinarySerializer удалён из-за уязвимости BinaryFormatter (RCE). " +
            "Используйте SafeSerializer или методы Save/Load конкретного типа.");
}
