using System.ComponentModel;

namespace AI.LLM.Clients.Tavily.Models;

public enum ExtractDepth
{
    [Description("basic")]
    Basic = 1,

    [Description("advanced")]
    Advanced = 2,
}
