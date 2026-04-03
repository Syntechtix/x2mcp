namespace Mcpify.Core.Orchestration;

public sealed class NoLanguageModuleException : Exception
{
    public string SourcePath { get; }
    public IReadOnlyCollection<string> DetectedExtensions { get; }

    public NoLanguageModuleException(string sourcePath, IReadOnlyCollection<string> detectedExtensions)
        : base(BuildMessage(sourcePath, detectedExtensions))
    {
        SourcePath = sourcePath;
        DetectedExtensions = detectedExtensions;
    }

    private static string BuildMessage(string sourcePath, IReadOnlyCollection<string> extensions) =>
        extensions.Count > 0
            ? $"No language module registered for extensions: {string.Join(", ", extensions)} (source: '{sourcePath}')"
            : $"No source files found at '{sourcePath}'";
}
