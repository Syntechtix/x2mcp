namespace Mcpify.Core.Models;

public record BuildResult(bool Success, string OutputPath, string? Error);
