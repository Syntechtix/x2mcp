using System.Diagnostics;
using System.Text;
using X2Mcp.Core.Abstractions;
using X2Mcp.Core.Models;

namespace X2Mcp.Core.Process;

public class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executable,
        string arguments,
        string workingDirectory,
        CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = new System.Diagnostics.Process { StartInfo = psi };

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
