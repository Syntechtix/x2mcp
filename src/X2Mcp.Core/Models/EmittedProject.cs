namespace X2Mcp.Core.Models;

public record EmittedProject(string ProjectPath, IReadOnlyList<EmittedFile> Files);
