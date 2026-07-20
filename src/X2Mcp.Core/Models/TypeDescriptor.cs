namespace X2Mcp.Core.Models;

public record TypeDescriptor(
    string Namespace,
    string Name,
    IReadOnlyList<FunctionDescriptor> Functions);
