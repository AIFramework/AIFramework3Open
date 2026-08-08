namespace AI.LLM.Core.Abstractions;

/// <summary>
/// Транспорт потоковой выдачи на стороне ПРИЛОЖЕНИЯ: доставка кадров генерации до его
/// потребителя (веб-сокет, SignalR, очередь).
/// </summary>
/// <remarks>
/// Сама библиотека этот контракт НЕ использует. Раньше <see cref="StartAsync"/> вызывался из
/// клиента LLM, но тот путь давно вытеснен: разбор SSE живёт внутри клиента, а наружу кадры
/// отдаёт <see cref="Clients.Base.ChatLLMApi.SendWithContextStreamAsync"/> — обычной
/// асинхронной последовательностью, без передачи вызывающему сырого HTTP-ответа.
/// Конструкторы клиентов больше не принимают обработчик: параметр был мёртвым и молча
/// игнорировался, создавая впечатление настроенного стриминга.
/// <para>
/// Интерфейс оставлен, потому что приложения используют его как СВОЮ абстракцию доставки
/// (<see cref="SendAsync(string, string, string)"/>). Прочитать генерацию по кадрам —
/// <c>SendWithContextStreamAsync</c>, разослать их своим клиентам — эта реализация.
/// </para>
/// </remarks>
public interface IStreamHandler
{
    /// <summary>
    /// Разбор сырого HTTP-ответа на стороне вызывающего.
    /// </summary>
    /// <remarks>
    /// Библиотекой не вызывается. Для покадрового чтения генерации используйте
    /// <see cref="Clients.Base.ChatLLMApi.SendWithContextStreamAsync"/>.
    /// </remarks>
    Task<string> StartAsync(string streamId, HttpResponseMessage response, string method);

    /// <summary>Отправляет текстовый кадр потребителю потока.</summary>
    Task<bool> SendAsync(string streamId, string message, string method);

    /// <summary>Отправляет объектный кадр потребителю потока.</summary>
    Task<bool> SendAsync<T>(string streamId, T message, string method) where T : class;
}
