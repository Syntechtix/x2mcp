namespace Mcpify.Core.Models;

public record BuildContext(
    string SourcePath,
    string OutputPath,
    string GeneratedProjectPath,
    string ServerName,
    Transport Transport);
