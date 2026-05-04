namespace AI.LLM.Agents.Multimodal;

/// <summary>
/// Поставщик наблюдений среды для мультимодального агента.
/// <para>Реализации: захват скриншота (Computer Use), камера робота,
/// датчики глубины, LIDAR и любые другие источники визуальных данных.</para>
/// <example>
/// <code>
/// // Пример: скриншот рабочего стола
/// public class ScreenshotProvider : IObservationProvider
/// {
///     public async Task&lt;AgentObservation&gt; ObserveAsync(CancellationToken ct)
///     {
///         var bitmap = CaptureScreen();
///         var png = bitmap.Encode(SKEncodedImageFormat.Png, 80);
///         return new AgentObservation(
///             new AgentImage(png.ToArray(), "image/png", "desktop_screenshot"),
///             $"Скриншот {DateTime.Now:HH:mm:ss}, разрешение {bitmap.Width}x{bitmap.Height}");
///     }
/// }
///
/// // Пример: камера робота
/// public class RobotCameraProvider : IObservationProvider
/// {
///     public async Task&lt;AgentObservation&gt; ObserveAsync(CancellationToken ct)
///     {
///         var frame = await _camera.CaptureFrameAsync(ct);
///         return new AgentObservation(
///             new AgentImage(frame.JpegBytes, "image/jpeg", "camera_front"),
///             $"Камера: позиция ({_robot.X:F1}, {_robot.Y:F1}), угол {_robot.Heading:F0}°");
///     }
/// }
/// </code>
/// </example>
/// </summary>
public interface IObservationProvider
{
    /// <summary>
    /// Получает текущее наблюдение среды.
    /// Вызывается агентом после выполнения инструментов для обновления визуального контекста.
    /// </summary>
    Task<AgentObservation> ObserveAsync(CancellationToken cancellationToken = default);
}
