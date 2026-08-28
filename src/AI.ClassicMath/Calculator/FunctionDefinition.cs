using System;
using System.Collections.Generic;
using System.Text;

namespace AI.ClassicMath.Calculator;

[Serializable]
public class FunctionDefinition
{
    public int ArgumentCount { get; set; }
    public Func<object[], object> Delegate { get; set; }

    /// <summary>
    /// Тело функции, которому нужен КОНТЕКСТ прогона: переменные, счётчик шагов, итоги.
    /// Приоритетнее <see cref="Delegate"/>.
    /// </summary>
    /// <remarks>
    /// Понадобилось двоим сразу: функциям, объявленным скриптом (их тело — инструкции, а
    /// инструкции исполняются в контексте), и <c>emit</c> (он копит именованные результаты
    /// прогона). Обычным функциям набора контекст по-прежнему не нужен и не даётся.
    /// </remarks>
    public Func<object[], ExecutionContext, object> ContextDelegate { get; set; }

    public string Name { get; set; }

    public DescriptionFunction Description { get; set; }

    public FunctionDefinition() { }

    public FunctionDefinition(int argumentCount, Func<object[], object> @delegate)
    {
        ArgumentCount = argumentCount;
        Delegate = @delegate;
    }

    /// <summary>Вызывает функцию: с контекстом, если её телу он нужен.</summary>
    public object Invoke(object[] args, ExecutionContext context) =>
        ContextDelegate != null ? ContextDelegate(args, context) : Delegate(args);
}

[Serializable]
public class DescriptionFunction
{
    public string Signature { get; set; }
    public string Description { get; set; }
    public List<string> AreaList { get; set; }
    public string Example { get; set; }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine("\n**Описание функции:**\n");

        sb.AppendLine($"Описание функции: {Description}");
        sb.AppendLine($"Описание сигнатуры (входов и выходов): {Signature}");
        sb.AppendLine($"Доменные области: [{string.Join(", ", AreaList).Trim(", ".ToCharArray())}]");
        sb.AppendLine($"Пример использования: {Example}");

        return sb.ToString();
    }
}
