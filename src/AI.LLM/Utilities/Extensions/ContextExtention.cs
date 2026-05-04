using AI.LLM.Core.Models.Common.Messages;

namespace AI.LLM.Utilities.Extensions;

public static class ContextExtention
{
    /// <summary>
    /// Исправление контекста для соответствия требованиям OpenAI API.
    /// API требует, чтобы:
    /// 1. Сообщения чередовались между ролями (user/assistant)
    /// 2. Первое сообщение не было от assistant
    /// 3. После system не шло сразу assistant
    /// 4. Сообщения с role=tool пропускаются без вставки фейковых промежуточных
    /// Метод автоматически вставляет пустые сообщения для соблюдения этих требований.
    /// </summary>
    public static List<LLMMessage> FixContext(this IEnumerable<LLMMessage> context)
    {
        List<LLMMessage> fixedMessages = [];

        foreach (var message in context)
        {
            // tool-сообщения добавляются как есть — они идут после assistant с tool_calls
            if (message.Role == LLMMessage.ToolRole)
            {
                fixedMessages.Add(message);
                continue;
            }

            if (fixedMessages.Count == 0)
            {
                if (message.Role == "assistant")
                    fixedMessages.Add(LLMMessage.CreateMessage(Roles.User, " "));

                fixedMessages.Add(message);
            }
            else
            {
                var prevRole = fixedMessages[fixedMessages.Count - 1].Role;

                // Пропускаем проверку чередования если предыдущее сообщение было tool
                if (prevRole != LLMMessage.ToolRole)
                {
                    if (message.Role == prevRole)
                        fixedMessages.Add(LLMMessage.CreateMessage(
                            message.Role == "assistant" ? Roles.User : Roles.Assistant, ""));

                    if (message.Role == "assistant" && prevRole == "system")
                        fixedMessages.Add(LLMMessage.CreateMessage(Roles.User, ""));
                }

                fixedMessages.Add(message);
            }
        }

        return fixedMessages;
    }
}
