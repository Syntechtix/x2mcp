using System.Text.Json.Serialization;

namespace Mcpify.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Transport
{
    Stdio,
    StreamableHttp,
}
