using Mcpify.Core.Abstractions;

namespace Mcpify.Core.IO;

public class FileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default) =>
        File.WriteAllTextAsync(path, contents, ct);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public string[] GetFiles(string path, string searchPattern, SearchOption searchOption) =>
        Directory.GetFiles(path, searchPattern, searchOption);
}
