using System;
using System.Collections.Generic;
using System.Text;

namespace AI.ClassicMath.Calculator;

[Serializable]
public class FunctionDefinition
{
    public int ArgumentCount { get; set; }
    public Func<object[], object> Delegate { get; set; }

    public string Name { get; set; }

    public DescriptionFunction Description { get; set; }

    public FunctionDefinition() { }

    public FunctionDefinition(int argumentCount, Func<object[], object> @delegate)
    {
        ArgumentCount = argumentCount;
        Delegate = @delegate;
    }

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

