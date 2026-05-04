using System.ComponentModel;

namespace AI.LLM.Clients.Tavily.Models;

public enum TopicType
{
    [Description("general")]
    General = 1,

    [Description("news")]
    News = 2,

    [Description("finance")]
    Finance = 3,
}
