namespace AI.LLM.Agents.ReAct.Policies;

/// <summary>
/// Обращение к модели: системная инструкция и запрос на входе, текст на выходе.
/// <para>
/// Делегат, а не интерфейс, намеренно. Вызывающей стороне не нужно писать класс-адаптер:
/// она передаёт лямбду поверх собственного клиента и сохраняет за собой то, что библиотеке
/// знать не следует, — выбор модели под задачу, учёт расходов, маршрутизацию, повторы.
/// </para>
/// </summary>
/// <param name="system">Системная инструкция.</param>
/// <param name="user">Запрос вместе с накопленными наблюдениями.</param>
/// <param name="cancellationToken">Токен отмены.</param>
/// <returns>Текст ответа модели.</returns>
public delegate Task<string> ReActCompletionDelegate(
    string system, string user, CancellationToken cancellationToken);
