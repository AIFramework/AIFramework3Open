using System.Collections.Generic;
using System.IO;
using System.Linq;
using YamlDotNet.Serialization;

namespace AI.DataPrepaire.DataLoader.Formats;

/// <summary>
/// Загрузчик YAML файлов
/// </summary>
public static class YAMLLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// Разбор YAML-строки в объект
    /// </summary>
    /// <typeparam name="T">Тип результата; ключи YAML сопоставляются через [YamlMember(Alias = ...)] или по именам свойств</typeparam>
    /// <param name="yaml">Текст YAML</param>
    public static T Deserialize<T>(string yaml) => Deserializer.Deserialize<T>(yaml);

    /// <summary>
    /// Загрузка YAML файла
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="path">Путь к файлу</param>
    public static T Read<T>(string path) => Deserialize<T>(File.ReadAllText(path));

    /// <summary>
    /// Загрузка всех YAML файлов папки
    /// </summary>
    /// <typeparam name="T">Тип содержимого одного файла</typeparam>
    /// <param name="directory">Папка</param>
    /// <param name="searchPattern">Маска файлов</param>
    public static IEnumerable<T> ReadDirectory<T>(string directory, string searchPattern = "*.yaml") =>
        Directory.EnumerateFiles(directory, searchPattern).Select(Read<T>);
}
