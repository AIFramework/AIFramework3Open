using AI.Agents.Contracts.Models;
using AI.LLM.Core.Abstractions;
using AI.LLM.Core.Models.Common.Requests;
using Serilog;

namespace AI.Agents.Interop.Models;

/// <summary>
/// Точки входа адаптеров модели: <c>ModelPort.Fixed(client, capabilities)</c> (или <c>client.AsModelPort(…)</c>)
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
