using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Config;

namespace X2Mcp.Core.Toolchain;

public class ToolchainValidator : IToolchainValidator
{
    private readonly IProcessRunner _processRunner;

    public ToolchainValidator(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<IReadOnlyList<string>> FindMissingExecutablesAsync(
        ToolchainConfig toolchain,
        CancellationToken ct = default)
    {
        var missing = new List<string>();

        foreach (var tool in toolchain.RequiredExecutables)
        {
            var (executable, arguments) = ProbeCommand(tool);
            var result = await _processRunner.RunAsync(executable, arguments, Path.GetTempPath(), ct);

            // ProcessRunner reports a failed-to-start process (executable missing/not on PATH) with
            // exit code -1 — see ProcessRunner's Win32Exception handling. A real process exiting with
            // -1 on its own is not a case any of our toolchains produce for a `--version`-style probe.
            if (result.ExitCode == -1)
                missing.Add(tool);
        }

        return missing;
    }

    // pyinstaller is invoked as `python -m PyInstaller` rather than as a standalone executable on
    // PATH (see X2Mcp.Language.Python's toolchain.json), so its availability has to be probed
    // through the same python interpreter that will actually run it.
    private static (string Executable, string Arguments) ProbeCommand(string tool) =>
        tool == "pyinstaller" ? ("python", "-m PyInstaller --version") : (tool, "--version");
}
