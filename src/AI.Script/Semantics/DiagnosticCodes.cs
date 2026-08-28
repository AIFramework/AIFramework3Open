namespace AI.Script.Semantics;

/// <summary>
/// Коды диагностик. Диапазоны закреплены за стадиями обработки — см. приложение C в DESIGN.md.
/// </summary>
public static class DiagnosticCodes
{
    // --- AIS00xx: лексер ---

    /// <summary>Незакрытая строка.</summary>
    public const string UnterminatedString = "AIS0001";

    /// <summary>Недопустимый символ.</summary>
    public const string InvalidCharacter = "AIS0003";

    /// <summary>Неизвестная escape-последовательность.</summary>
    public const string UnknownEscape = "AIS0004";

    /// <summary>Некорректный числовой литерал.</summary>
    public const string InvalidNumber = "AIS0005";

    /// <summary>Некорректный литерал даты.</summary>
    public const string InvalidDate = "AIS0006";

    /// <summary>Незакрытая подстановка <c>${...}</c>.</summary>
    public const string UnterminatedInterpolation = "AIS0007";

    // --- AIS02xx: парсер ---

    /// <summary>Цепочка сравнений: <c>a &lt; b &lt; c</c>.</summary>
    public const string ChainedComparison = "AIS0201";

    /// <summary>Ожидался другой токен.</summary>
    public const string UnexpectedToken = "AIS0205";

    /// <summary>Более одного плейсхолдера <c>_</c> в звене конвейера.</summary>
    public const string DuplicatePlaceholder = "AIS0210";

    /// <summary>Правое звено конвейера не является вызовом.</summary>
    public const string PipeTargetNotCall = "AIS0212";

    /// <summary>Позиционный аргумент после именованного.</summary>
    public const string PositionalAfterNamed = "AIS0215";

    /// <summary>Блок <c>options</c> не в начале файла.</summary>
    public const string MisplacedOptions = "AIS0220";

    /// <summary>Инструкция допустима только на верхнем уровне.</summary>
    public const string NotTopLevel = "AIS0225";

    // --- AIS11xx: имена и сигнатуры ---

    /// <summary>Неизвестная функция.</summary>
    public const string UnknownFunction = "AIS1101";

    /// <summary>Неизвестный именованный аргумент.</summary>
    public const string UnknownArgument = "AIS1102";

    /// <summary>Не передан обязательный аргумент.</summary>
    public const string MissingArgument = "AIS1103";

    /// <summary>Лишний позиционный аргумент.</summary>
    public const string ExtraPositional = "AIS1105";

    /// <summary>Аргумент передан дважды.</summary>
    public const string DuplicateArgument = "AIS1106";

    /// <summary>Неизвестное пространство имён.</summary>
    public const string UnknownNamespace = "AIS1110";

    // --- AIS12xx: области видимости ---

    /// <summary>Повторный <c>let</c> для уже связанного имени.</summary>
    public const string DuplicateLet = "AIS1201";

    /// <summary><c>set</c> для несвязанного имени.</summary>
    public const string UnboundSet = "AIS1202";

    /// <summary>Использование несвязанного имени.</summary>
    public const string UnboundName = "AIS1203";

    /// <summary><c>break</c>/<c>continue</c> вне цикла.</summary>
    public const string NotInLoop = "AIS1205";

    /// <summary><c>return</c> вне функции.</summary>
    public const string ReturnOutsideFunction = "AIS1206";

    /// <summary>Повторное объявление функции.</summary>
    public const string DuplicateFunction = "AIS1207";

    // --- AIS2xxx: типы и предупреждения ---

    /// <summary>Несовместимый тип аргумента.</summary>
    public const string TypeMismatch = "AIS2101";

    /// <summary>Оператор не определён для типов операндов.</summary>
    public const string BadOperandTypes = "AIS2102";

    /// <summary>Условие не является логическим значением.</summary>
    public const string ConditionNotBool = "AIS2103";

    /// <summary>Вызывается значение, не являющееся функцией.</summary>
    public const string NotCallable = "AIS2104";

    /// <summary>Объявленный тип не совпадает с типом значения.</summary>
    public const string DeclaredTypeMismatch = "AIS2105";

    /// <summary>По значению такого типа нельзя пройти циклом.</summary>
    public const string NotIterable = "AIS2106";

    /// <summary>Точное сравнение вещественных чисел.</summary>
    public const string ExactFloatComparison = "AIS2301";

    /// <summary>Сравнение значений разных типов: результат постоянен.</summary>
    public const string ComparingDifferentTypes = "AIS2302";

    /// <summary>Затенение имени во вложенной области.</summary>
    public const string Shadowing = "AIS2401";

    /// <summary>Возможность языка ещё не реализована в текущей версии.</summary>
    public const string NotImplementedYet = "AIS2901";

    // --- AIS31xx: исполнение ---

    /// <summary>Индекс вне границ.</summary>
    public const string IndexOutOfRange = "AIS3101";

    /// <summary>Несовместимые размеры.</summary>
    public const string SizeMismatch = "AIS3102";

    /// <summary>Операция не определена для данных типов.</summary>
    public const string BadOperand = "AIS3103";

    /// <summary>Ошибка внутри вызванной функции фреймворка.</summary>
    public const string FunctionFailed = "AIS3105";

    /// <summary>Нарушен инвариант <c>assert</c>.</summary>
    public const string AssertionFailed = "AIS3120";

    // --- AIS32xx: лимиты ---

    /// <summary>Превышен потолок шагов интерпретатора.</summary>
    public const string StepLimit = "AIS3201";

    /// <summary>Превышен таймаут прогона.</summary>
    public const string Timeout = "AIS3202";

    /// <summary>Превышен потолок памяти под значения.</summary>
    public const string MemoryLimit = "AIS3203";

    /// <summary>Превышена глубина вложенности вызовов.</summary>
    public const string CallDepthLimit = "AIS3204";

    /// <summary>Прогон отменён.</summary>
    public const string Cancelled = "AIS3210";

    /// <summary>Превышен потолок расходов на внешние вызовы.</summary>
    public const string CostLimit = "AIS3205";

    // --- AIS34xx: ресурсы и политика ---

    /// <summary>Путь вне песочницы либо файловый доступ запрещён.</summary>
    public const string SandboxDenied = "AIS3410";

    /// <summary>Обращение к сети запрещено настройками прогона.</summary>
    public const string NetworkDenied = "AIS3411";

    /// <summary>Файл не найден.</summary>
    public const string FileNotFound = "AIS3420";

    /// <summary>Не удалось разобрать содержимое файла.</summary>
    public const string BadFileFormat = "AIS3421";
}
