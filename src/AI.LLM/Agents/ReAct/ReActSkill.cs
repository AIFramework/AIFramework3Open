using AI.LLM.Agents.Planning;

namespace AI.LLM.Agents.ReAct;

/// <summary>Навык цикла, заданный текстом и необязательным условием применимости.</summary>
public sealed class ReActSkill : IReActSkill
{
    private readonly Func<ReActRunContext, bool> _isApplicable;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Instruction { get; }

    /// <summary>Создаёт навык.</summary>
    /// <param name="name">Короткое имя; обязательно.</param>
    /// <param name="instruction">Инструкция для модели; обязательна.</param>
    /// <param name="isApplicable">Условие применимости; при <c>null</c> навык применим всегда.</param>
    public ReActSkill(string name, string instruction, Func<ReActRunContext, bool> isApplicable = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Имя навыка не может быть пустым.", nameof(name));
        if (string.IsNullOrWhiteSpace(instruction))
            throw new ArgumentException("Инструкция навыка не может быть пустой.", nameof(instruction));

        Name = name.Trim();
        Instruction = instruction;
        _isApplicable = isApplicable;
    }

    /// <inheritdoc />
    public bool IsApplicable(ReActRunContext context) => _isApplicable == null || _isApplicable(context);

    /// <summary>Переносит навык планировщика в цикл без изменений.</summary>
    /// <param name="skill">Навык планировщика.</param>
    public static ReActSkill FromPlanningSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return new ReActSkill(skill.Name, skill.Description);
    }
}
