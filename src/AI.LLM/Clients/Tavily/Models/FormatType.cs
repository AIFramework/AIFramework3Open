using System.ComponentModel;

namespace AI.LLM.Clients.Tavily.Models;

public enum FormatType
{
    [Description("markdown")]
    Markdown = 1,

    [Description("text")]
    Text = 2,
}
