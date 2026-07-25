namespace AI.LLM.Agents.ReAct;

/// <summary>
/// Фрагмент потокового ответа модели. Два независимых канала: видимый текст и поток рассуждений —
/// их нельзя смешивать, иначе рассуждения протекают в ответ пользователю.
/// </summary>
/// <param name="Content">Видимый текст ответа; может быть пустым.</param>
/// <param name="Reasoning">Фрагмент рассуждений модели; может быть пустым.</param>
public readonly record struct ReActTextChunk(string Content, string Reasoning);
