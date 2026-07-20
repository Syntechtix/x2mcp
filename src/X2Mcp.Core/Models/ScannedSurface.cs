namespace X2Mcp.Core.Models;

public record ScannedSurface(
    string SourcePath,
    string Language,
    IReadOnlyList<TypeDescriptor> Types);
