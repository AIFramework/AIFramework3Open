namespace AI.LLM.Services.Prompts
{
    [Serializable]
    public static class TaskPrompts
    {

        public static string InputPrompt(string text, string task, string lang = "en")
        {
            string key = $"{task}_{lang}";
            if (!PromptManager.TasksForChatModelPrompts.TryGetValue(key, out string summarizationPromptBase))
                throw new ArgumentException($"Prompt not found for task='{task}', lang='{lang}' (key='{key}').");
            return summarizationPromptBase.Replace("{text}", text);
        }

    }
}
