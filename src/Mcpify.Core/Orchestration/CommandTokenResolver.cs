using Mcpify.Core.Models;

namespace Mcpify.Core.Orchestration;

public static class CommandTokenResolver
{
    public static string Resolve(string template, BuildContext context) =>
        template
            .Replace("{SourcePath}", context.SourcePath)
            .Replace("{OutputPath}", context.OutputPath)
            .Replace("{GeneratedProjectPath}", context.GeneratedProjectPath)
            .Replace("{ServerName}", context.ServerName)
            .Replace("{Transport}", context.Transport.ToString());
}
