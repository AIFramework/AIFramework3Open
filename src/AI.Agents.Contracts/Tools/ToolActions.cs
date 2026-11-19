namespace AI.Agents.Contracts.Tools;

/// <summary>
/// Трактовка вида действия: одно место, где решается, как понимать незаданный вид.
/// </summary>
public static class ToolActions
{
    /// <summary>
    /// Вид, по которому решают согласие и повтор: <see cref="ToolAction.Unknown"/> и любое
    /// необъявленное значение становятся <see cref="ToolAction.Irreversible"/>, остальные остаются как есть.
    /// </summary>
    /// <remarks>
    /// Гейт вызывает этот метод на результате <see cref="ITool.Classify(ToolCall)"/> до исполнения. Инструмент
    /// чужого сервера без достоверного объявления получает <see cref="ToolAction.Unknown"/> и потому
    /// считается необратимым.
    /// </remarks>
    /// <param name="action">Объявленный вид действия.</param>
    /// <returns>Вид для решения: никогда не <see cref="ToolAction.Unknown"/>.</returns>
    public static ToolAction Effective(this ToolAction action) => action switch
    {
        ToolAction.Read => ToolAction.Read,
        ToolAction.Compute => ToolAction.Compute,
        ToolAction.Reversible => ToolAction.Reversible,
        _ => ToolAction.Irreversible,
    };
}
