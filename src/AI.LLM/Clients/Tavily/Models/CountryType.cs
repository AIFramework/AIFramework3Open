using System.ComponentModel;

namespace AI.LLM.Clients.Tavily.Models;

public enum CountryType
{
    [Description(null)]
    All = 1,

    [Description("russia")]
    Russia = 2,
}
