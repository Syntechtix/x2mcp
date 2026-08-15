using X2Mcp.Core.Models;

namespace X2Mcp.Core.Orchestration;

public static class CommandTokenResolver
{
    public static string Resolve(string template, BuildContext context) =>
        Resolve(template, context, OperatingSystem.IsWindows());

    // Internal overload with an explicit isWindows flag so both the Windows and non-Windows
    // forms of {ExeSuffix} can be exercised by unit tests regardless of the host OS running them.
    public static string Resolve(string template, BuildContext context, bool isWindows) =>
        template
            .Replace("{SourcePath}", context.SourcePath)
            .Replace("{OutputPath}", context.OutputPath)
            .Replace("{GeneratedProjectPath}", context.GeneratedProjectPath)
            .Replace("{ServerName}", context.ServerName)
            .Replace("{Transport}", context.Transport.ToString())
            .Replace("{ExeSuffix}", isWindows ? ".exe" : "");
}
