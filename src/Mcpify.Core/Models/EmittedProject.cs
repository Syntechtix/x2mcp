namespace Mcpify.Core.Models;

public record EmittedProject(string ProjectPath, IReadOnlyList<EmittedFile> Files);
