namespace Mcpify.Core.Abstractions;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    Task WriteAllTextAsync(string path, string contents, CancellationToken ct = default);
    void CreateDirectory(string path);
    string[] GetFiles(string path, string searchPattern, SearchOption searchOption);
}
