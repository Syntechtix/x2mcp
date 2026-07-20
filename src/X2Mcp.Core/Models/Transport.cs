using System.Text.Json.Serialization;

namespace X2Mcp.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Transport
{
    Stdio,
    StreamableHttp,
}
