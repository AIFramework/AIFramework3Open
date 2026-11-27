using AI.Agents.Contracts.Models;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Requests;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Точки входа адаптеров модели: <c>ModelPort.Fixed(client, capabilities)</c> (или <c>client.AsModelPort(…)</c>),
/// сторож потока <c>port.Watched(watch)</c>
/// и <c>port.AsLlmClient(role)</c>.
/// </summary>
/// <remarks>
/// Как переводится каждое поле — <see cref="ModelTranslationTable"/>; сами значения переводит
/// <see cref="ModelTranslation"/>. Журнал — Serilog; <c>log</c> = <see langword="null"/> означает
/// <see cref="Log.Logger"/> на момент записи.
/// </remarks>
public static class ModelPort
{
    /// <summary>Порт модели над старым клиентом: одна модель на все роли.</summary>
    /// <param name="client">Клиент модели.</param>
    /// <param name="capabilities">
    /// Что умеет модель клиента. Клиент этого не сообщает, поэтому признаки задает вызывающий. При
    /// <see cref="ModelCapabilities.Streaming"/> = <see langword="true"/> поток идет через перегрузку
    /// <c>SendFullAsync</c> с <c>onChunk</c>; иначе поток из одного итогового кадра.
    /// </param>
    /// <param name="settings">
    /// Настройки, с которых начинается каждый запрос (температура, рассуждение, <c>IncludeUsage</c> для стоимости у
    /// OpenRouter); копируются при создании. <see langword="null"/>: <c>new GenerateSettings()</c>. Инструменты, режим
    /// инструментов и схему ответа задает запрос.
    /// </param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Порт. <see cref="IModelPort.Describe(ModelRole)"/> на любую роль отдает <paramref name="capabilities"/>.</returns>
    public static IModelPort Fixed(
        ILLMClient client, ModelCapabilities capabilities, GenerateSettings? settings = null, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(capabilities);
        return new ClientModelPort(client, capabilities, settings, log);
    }

    /// <summary>То же, что <see cref="Fixed"/>, в виде расширения клиента.</summary>
    /// <param name="client">Клиент модели.</param>
    /// <param name="capabilities">Что умеет модель клиента.</param>
    /// <param name="settings">Начальные настройки запроса; <see langword="null"/>: <c>new GenerateSettings()</c>.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>Порт.</returns>
    public static IModelPort AsModelPort(
        this ILLMClient client, ModelCapabilities capabilities, GenerateSettings? settings = null, ILogger? log = null) =>
        Fixed(client, capabilities, settings, log);

    /// <summary>
    /// Порт по ролям с запасными моделями: роль → основная модель и запасные по порядку. Переход по цепочке —
    /// одно правило <see cref="ModelFallback"/>; итог говорит, какая модель ответила (<see cref="LlmAnswer.Model"/>), а
    /// расход считает по всем попыткам.
    /// </summary>
    /// <param name="chains">Цепочки по ролям; первая модель — основная. Пустая роль или цепочка не допускаются.</param>
    /// <param name="log">Журнал переходов; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>
    /// Порт. Роль вне <paramref name="chains"/> — <see cref="ArgumentException"/> при вызове: незнакомая роль — отказ,
    /// а не модель по умолчанию. <see cref="IModelPort.Describe(ModelRole)"/> — возможности основной модели роли.
    /// </returns>
    public static IModelPort Routed(IReadOnlyDictionary<ModelRole, IReadOnlyList<ModelSlot>> chains, ILogger? log = null) =>
        new RoutedModelPort(chains, log);

    /// <summary>Сторож потока над портом: молчание модели дольше <paramref name="watch"/> — итог сбоя, а не ожидание.</summary>
    /// <param name="port">Порт.</param>
    /// <param name="watch">Сколько ждать первого фрагмента и сколько терпеть простой посреди ответа.</param>
    /// <param name="time">Часы; <see langword="null"/>: <see cref="TimeProvider.System"/>.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>
    /// Порт. Срабатывание сторожа — кадр <see cref="LlmChunk.Final"/> с <see cref="LlmStatus.Failed"/> и
    /// <see cref="LlmFailure.Stalled"/> (текст причины в <see cref="LlmAnswer.FailureText"/>), внутренний поток при
    /// этом отменяется. Порт без настоящего потока (<see cref="ModelCapabilities.Streaming"/> =
    /// <see langword="false"/>) идет без сторожа: молчание у него не отличить от работы.
    /// </returns>
    public static IModelPort Watched(this IModelPort port, StreamWatch watch, TimeProvider? time = null, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(port);
        ArgumentNullException.ThrowIfNull(watch);
        return new WatchedModelPort(port, watch, time ?? TimeProvider.System, log);
    }

    /// <summary>Старый клиент над портом модели.</summary>
    /// <param name="port">Порт.</param>
    /// <param name="role">Роль, с которой клиент спрашивает порт.</param>
    /// <param name="log">Журнал; <see langword="null"/>: <see cref="Log.Logger"/>.</param>
    /// <returns>
    /// Клиент. Сбой и пустой ответ порта приходят исключением <c>LlmRequestException</c>, как у <c>ChatLLMApi</c>;
    /// <c>TokenizeAsync</c> не поддерживается (<see cref="NotSupportedException"/>).
    /// </returns>
    /// <exception cref="ArgumentException">Роль не задана (<c>default(ModelRole)</c>).</exception>
    public static ILLMClient AsLlmClient(this IModelPort port, ModelRole role, ILogger? log = null)
    {
        ArgumentNullException.ThrowIfNull(port);
        if (role.Name == null)
            throw new ArgumentException("Роль модели не задана (default(ModelRole)).", nameof(role));

        return new PortLlmClient(port, role, log);
    }
}
