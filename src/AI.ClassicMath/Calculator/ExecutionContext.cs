using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AI.ClassicMath.Calculator;

/// <summary>
/// Хранит состояние сессии калькулятора, в первую очередь - память о переменных.
/// </summary>
public class ExecutionContext
{
    /// <summary>
    /// Словарь для хранения переменных.
    /// Теперь регистрозависимый - M и m это разные переменные!
    /// </summary>
    public Dictionary<string, object> Memory { get; } = new(StringComparer.Ordinal);

    public ExecutionContext()
    {
        Memory["pi"] = Math.PI;
        Memory["e"] = Math.E;
        Memory["phi"] = 1.61803398874989;
    }

    public void AddDoubleConstant(string constantName, double constant)
    {
        if (!Memory.ContainsKey(constantName))
            Memory.Add(constantName, constant);
        else Memory[constantName] = constant;
    }

    /// <summary>
    /// Загрузка списка скалярных констант
    /// </summary>
    /// <param name="constants">Строка вида "name1=value1\nname2=value2"</param>
    public void AddDoubleConstants(string constants)
    {
        constants = constants.Replace(" ", "");
        List<string> constsArray = constants.Split('\n').ToList();

        foreach (string constant in constsArray)
        {
            if (string.IsNullOrWhiteSpace(constant)) continue;
            var nameValue = constant.Split('=');
            if (nameValue.Length < 2) continue;

            var isValue = double.TryParse(nameValue[1], NumberStyles.Any, CultureInfo.InvariantCulture, out var numValue);
            if (isValue) AddDoubleConstant(nameValue[0], numValue);
        }
    }
}

