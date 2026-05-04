using System.ComponentModel;
using System.Reflection;

namespace AI.LLM.Infrastructure.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        FieldInfo field = value.GetType().GetField(value.ToString());
        if (field == null)
        {
            return value.ToString();
        }

        // Пытаемся получить атрибут Description
        DescriptionAttribute attribute = field.GetCustomAttribute<DescriptionAttribute>();

        // Если атрибут есть и его значение не null, возвращаем его, иначе — имя члена enum
        return attribute?.Description ?? value.ToString();
    }
}