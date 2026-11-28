﻿using System.Text.Json.Serialization;

namespace AI.LLM.Core.Models.Common.Requests;

/// <summary>
/// Какие поля рассуждения принимает модель.
/// </summary>
/// <remarks>
/// Задаётся в <see cref="GenerateSettings.ReasoningSupport"/>. Без него (<see langword="null"/>) возможности модели
/// неизвестны и поле выбирается по форме, в которой рассуждение задано.
/// </remarks>
[Flags]
public enum ReasoningSupport
{
    /// <summary>Ни одного: поля рассуждения не отправляются.</summary>
    None = 0,

    /// <summary><c>reasoning.effort</c>: уровень в объекте рассуждения (OpenRouter).</summary>
    Effort = 1,

    /// <summary><c>reasoning.max_tokens</c>: бюджет токенов в объекте рассуждения (OpenRouter, Anthropic, Gemini).</summary>
    MaxTokens = 2,

    /// <summary><c>reasoning_effort</c>: уровень полем верхнего уровня (OpenAI).</summary>
    TopLevelEffort = 4,
}

/// <summary>
/// Настройки рассуждений для генерации ответов с расширенным мышлением (CoT, ToT и подобные)
/// </summary>
/// <remarks>
/// <see cref="Effort"/> и <see cref="MaxTokens"/> взаимоисключающие: и конструктор, и инициализатор отказывают в паре
/// одинаково, исключением. Что уходит провайдеру, решает <see cref="ForRequest"/> — единственное место, где поля
/// рассуждения собираются в запрос.
/// </remarks>
[Serializable]
public class ReasoningSettings
{
    private string _effort;
    private int? _maxTokens;

    #region Основные настройки

    /// <summary>
    /// Уровень усилий для рассуждений: "high", "medium" или "low".
    /// Взаимоисключающее с <see cref="MaxTokens"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Уровень задаётся, когда уже задан <see cref="MaxTokens"/>.</exception>
    [JsonPropertyName("effort")]
    public string Effort
    {
        get => _effort;
        set
        {
            if (!string.IsNullOrEmpty(value) && _maxTokens.HasValue)
                throw PairRefused(nameof(Effort));
            _effort = value;
        }
    }

    /// <summary>
    /// Максимальное количество токенов для процесса рассуждений.
    /// Взаимоисключающее с <see cref="Effort"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Бюджет задаётся, когда уже задан <see cref="Effort"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Бюджет не положителен.</exception>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens
    {
        get => _maxTokens;
        set
        {
            if (value <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTokens), $"MaxTokens должен быть положительным. Получено: {value}");
            if (value.HasValue && !string.IsNullOrEmpty(_effort))
                throw PairRefused(nameof(MaxTokens));
            _maxTokens = value;
        }
    }

    /// <summary>
    /// Исключить ли токены рассуждений из ответа (по умолчанию false).
    /// При true возвращается только финальный результат без промежуточных шагов.
    /// </summary>
    [JsonPropertyName("exclude")]
    public bool Exclude { get; set; }

    /// <summary>
    /// Включены ли рассуждения.
    /// В запросе включаются и сами, если задан <see cref="MaxTokens"/> или <see cref="Effort"/> не "none".
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    #endregion

    #region Конструкторы

    /// <summary>
    /// Инициализирует новый экземпляр для сериализации.
    /// </summary>
    public ReasoningSettings() { }

    /// <summary>
    /// Инициализирует настройки рассуждений с указанными параметрами.
    /// </summary>
    /// <param name="effort">Уровень усилий: "high", "medium" или "low". Используйте константы <see cref="EffortLevels"/>.</param>
    /// <param name="maxTokens">Максимальное количество токенов для рассуждений.</param>
    /// <param name="exclude">Исключить промежуточные шаги рассуждений из ответа.</param>
    /// <param name="enabled">Явно включить/выключить рассуждения.</param>
    /// <exception cref="ArgumentException">Выбрасывается, если указаны одновременно <paramref name="effort"/> и <paramref name="maxTokens"/>.</exception>
    public ReasoningSettings(
        string effort = null,
        int? maxTokens = null,
        bool exclude = false,
        bool enabled = false)
    {
        Effort = effort;
        MaxTokens = maxTokens;
        Exclude = exclude;
        Enabled = enabled || !string.IsNullOrEmpty(effort) || maxTokens.HasValue;
    }

    #endregion

    #region Вспомогательные методы

    private static ArgumentException PairRefused(string paramName) =>
        new("Параметры Effort и MaxTokens взаимоисключающие. Укажите только один из них.", paramName);

    /// <summary>
    /// Создает настройки с высоким уровнем усилий.
    /// </summary>
    public static ReasoningSettings CreateHighEffort(bool excludeSteps = false) =>
            new ReasoningSettings(effort: EffortLevels.High, exclude: excludeSteps);


    /// <summary>
    /// Создает настройки с ограничением по токенам.
    /// </summary>
    public static ReasoningSettings CreateWithTokenLimit(int maxTokens, bool excludeSteps = false) =>
            new ReasoningSettings(maxTokens: maxTokens, exclude: excludeSteps);


    #endregion

    #region Сборка запроса

    /// <summary>
    /// Поля рассуждения в запрос провайдеру — единственное место, где они собираются.
    /// </summary>
    /// <remarks>
    /// Правило. Сколько рассуждать говорит ровно одно из трёх полей: <c>reasoning.effort</c>, <c>reasoning.max_tokens</c>
    /// или <c>reasoning_effort</c>; вместе они не уходят никогда. Кандидаты по убыванию явности: уровень объекта
    /// рассуждения, бюджет объекта, поле верхнего уровня (объект — настройка рассуждения целиком, поле верхнего
    /// уровня — сокращение). Берётся первый, который модель примет. Если возможности известны
    /// (<see cref="GenerateSettings.ReasoningSupport"/>), уровень переезжает в ту форму, которую модель принимает
    /// (объект или поле верхнего уровня), бюджет без поддержки отбрасывается; если неизвестны — поле уходит в той форме,
    /// в какой задано. <c>exclude</c> и <c>enabled</c> едут в объекте, если модель принимает объект; <c>enabled</c>
    /// включается и сам, если выбран бюджет или уровень не "none". <c>max_reasoning_tokens</c> (vLLM) повторяет
    /// выбранный бюджет и без него не уходит.
    /// </remarks>
    /// <param name="settings">Настройки запроса; не меняются.</param>
    /// <returns>Объект <c>reasoning</c>, поле <c>reasoning_effort</c> и <c>max_reasoning_tokens</c>; <see langword="null"/> — поле не уходит.</returns>
    internal static (ReasoningSettings Reasoning, string ReasoningEffort, int? MaxReasoningTokens) ForRequest(GenerateSettings settings)
    {
        ReasoningSettings source = settings?.ReasoningSettings;
        ReasoningSupport? supported = settings?.ReasoningSupport;
        bool objectAccepted = supported is not { } known || (known & (ReasoningSupport.Effort | ReasoningSupport.MaxTokens)) != 0;

        string effort = null, topLevel = null;
        int? budget = null;

        if (source?.Effort is { Length: > 0 } objectEffort)
            PlaceLevel(objectEffort, fromObject: true);
        else if (source?.MaxTokens is { } maxTokens && Accepts(ReasoningSupport.MaxTokens))
            budget = maxTokens;

        if (effort == null && budget == null && topLevel == null && settings?.ReasoningEffort is { Length: > 0 } flat)
            PlaceLevel(flat, fromObject: false);

        ReasoningSettings reasoning = null;
        if ((source != null && objectAccepted) || effort != null || budget != null)
        {
            reasoning = new ReasoningSettings
            {
                Effort = effort,
                MaxTokens = budget,
                Exclude = source?.Exclude ?? false,
                Enabled = (source?.Enabled ?? false) || budget != null
                    || (effort != null && !string.Equals(effort, EffortLevels.None, StringComparison.OrdinalIgnoreCase)),
            };
        }

        return (reasoning, topLevel, budget);

        bool Accepts(ReasoningSupport field) => supported is not { } s || (s & field) != 0;

        // Неизвестные возможности: форма та, в которой уровень задан; известные: форма, которую модель принимает,
        // с предпочтением заданной.
        void PlaceLevel(string level, bool fromObject)
        {
            if (supported == null)
            {
                if (fromObject) effort = level;
                else topLevel = level;
            }
            else if (fromObject ? Accepts(ReasoningSupport.Effort) : !Accepts(ReasoningSupport.TopLevelEffort) && Accepts(ReasoningSupport.Effort))
            {
                effort = level;
            }
            else if (Accepts(ReasoningSupport.TopLevelEffort))
            {
                topLevel = level;
            }
        }
    }

    /// <summary>
    /// Уровень запроса поверх начальных настроек: в объект рассуждения, если начальные настройки говорят им, иначе в
    /// <c>reasoning_effort</c>. Уровень запроса явнее начальных настроек, поэтому заменяет и их уровень, и их бюджет;
    /// <c>exclude</c> и <c>enabled</c> объекта сохраняются. Модель, которая уровня не принимает ни в какой форме
    /// (<see cref="GenerateSettings.ReasoningSupport"/>), остаётся при начальных настройках.
    /// </summary>
    /// <param name="settings">Копия настроек запроса; меняется она, объект рассуждения заменяется целиком.</param>
    /// <param name="effort">Уровень строчными: none, minimal, low, medium, high.</param>
    internal static void ApplyLevel(GenerateSettings settings, string effort)
    {
        if (settings.ReasoningSupport is { } known && (known & (ReasoningSupport.Effort | ReasoningSupport.TopLevelEffort)) == 0)
            return;

        if (settings.ReasoningSettings is { } current)
            Assign(settings, new ReasoningSettings { Effort = effort, Exclude = current.Exclude, Enabled = current.Enabled }, null);
        else
            Assign(settings, null, effort);
    }

    /// <summary>
    /// Объект рассуждения поверх начальных настроек (настройки клиента): если он задаёт уровень или бюджет, поле
    /// <c>reasoning_effort</c> начальных настроек снимается — объект явнее.
    /// </summary>
    internal static void ApplyObject(GenerateSettings settings, ReasoningSettings reasoning)
    {
        bool sizes = !string.IsNullOrEmpty(reasoning?.Effort) || reasoning?.MaxTokens != null;
        Assign(settings, reasoning, sizes ? null : settings.ReasoningEffort);
    }

    /// <summary>Записывает оба входных поля рассуждения настроек разом.</summary>
    internal static void Assign(GenerateSettings settings, ReasoningSettings reasoning, string reasoningEffort)
    {
        settings.ReasoningSettings = reasoning;
        settings.ReasoningEffort = reasoningEffort;
    }

    #endregion


    /// <summary>
    /// Уровни усилий для рассуждения
    /// </summary>
    [Serializable]
    public static class EffortLevels
    {
        /// <summary>Высокий уровень усилий для сложных задач.</summary>
        public const string High = "high";

        /// <summary>Средний уровень усилий (по умолчанию).</summary>
        public const string Medium = "medium";

        /// <summary>Низкий уровень усилий для простых задач.</summary>
        public const string Low = "low";

        /// <summary>Отсутсвие усилия и рассуждения в целом.</summary>
        public const string None = "none";
    }
}
