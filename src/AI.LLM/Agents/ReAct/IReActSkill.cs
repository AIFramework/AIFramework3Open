namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Навык — текстовая инструкция «как делать», подмешиваемая в системный промпт цикла.
/// <para>
/// В отличие от <see cref="Planning.Skill"/>, который виден только генератору плана, навык
/// цикла доходит до исполнения и умеет отключаться: <see cref="IsApplicable"/> позволяет не
/// грузить модель инструкциями, не относящимися к текущему запросу.
/// </para>
/// </summary>
/// <remarks>
/// Навык не поставляет инструменты — это работа <see cref="Tools.IReActToolSource"/>.
/// Разделение намеренное: два источника инструментов потребовали бы правил приоритета и
/// разрешения конфликтов имён, а выигрыша не дают.
/// </remarks>
public interface IReActSkill
{
    /// <summary>Короткое имя навыка.</summary>
    string Name { get; }

    /// <summary>Инструкция для модели.</summary>
    string Instruction { get; }

    /// <summary>Применим ли навык в этом контексте.</summary>
    /// <param name="context">Контекст прогона.</param>
    /// <returns><c>true</c>, если инструкцию нужно включить в промпт.</returns>
    bool IsApplicable(ReActRunContext context) => true;
}
