namespace Mcpify.Core.Models;

public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
