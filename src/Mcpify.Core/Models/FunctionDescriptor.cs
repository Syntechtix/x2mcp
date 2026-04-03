namespace Mcpify.Core.Models;

public record FunctionDescriptor(
    string Name,
    IReadOnlyList<ParameterDescriptor> Parameters,
    string ReturnType,
    bool IsAsync);
