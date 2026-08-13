using X2Mcp.Core.Abstractions;
using X2Mcp.Core.IO;
using X2Mcp.Core.Models;
using System.Text;

namespace X2Mcp.Language.Ruby;

public class RubyWrapperEmitter : IWrapperEmitter
{
    private readonly IFileSystem _fileSystem;

    public RubyWrapperEmitter(IFileSystem? fileSystem = null)
    {
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public EmittedProject Emit(ScannedSurface surface, BuildContext context)
    {
        var files = new List<EmittedFile>();
        var sourceMappings = ResolveSourceMappings(context.SourcePath);

        foreach (var sourceMapping in sourceMappings)
            files.Add(new EmittedFile(sourceMapping.TargetRelativePath, _fileSystem.ReadAllText(sourceMapping.SourcePath)));

        files.Add(new EmittedFile("server.rb", GenerateServer(surface, context, sourceMappings)));
        files.Add(new EmittedFile("build.rb", GenerateBuildScript()));

        return new EmittedProject(context.GeneratedProjectPath, files);
    }

    private List<SourceMapping> ResolveSourceMappings(string sourcePath)
    {
        if (_fileSystem.FileExists(sourcePath))
        {
            if (Path.GetExtension(sourcePath).Equals(".rb", StringComparison.OrdinalIgnoreCase)
                && !IsExcludedFile(sourcePath))
            {
                return [new SourceMapping(sourcePath, Path.GetFileName(sourcePath))];
            }

            return [];
        }

        if (!_fileSystem.DirectoryExists(sourcePath))
            return [];

        return _fileSystem
            .GetFiles(sourcePath, "*.rb", SearchOption.AllDirectories)
            .Where(path => !IsExcludedFile(path))
            .Select(path => new SourceMapping(path, Path.GetRelativePath(sourcePath, path)))
            .ToList();
    }

    private static string GenerateServer(
        ScannedSurface surface,
        BuildContext context,
        IReadOnlyList<SourceMapping> sourceMappings)
    {
        var requireLines = sourceMappings
            .Select(mapping => Path.ChangeExtension(mapping.TargetRelativePath, null) ?? mapping.TargetRelativePath)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => path
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/'))
            .Select(path => $"require_relative './{path}'")
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("require 'json'");
        builder.AppendLine("require 'time'");
        foreach (var requireLine in requireLines)
            builder.AppendLine(requireLine);

        builder.AppendLine();
        builder.AppendLine("TOOLS = {");

        var registrations = BuildToolRegistrations(surface.Types);
        for (var i = 0; i < registrations.Count; i++)
        {
            var registration = registrations[i];
            var comma = i < registrations.Count - 1 ? "," : string.Empty;
            builder.AppendLine($"  '{registration.ToolName}' => {{ params: {registration.ParametersLiteral}, call: lambda {{ |args| {registration.CallExpression} }} }}{comma}");
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine($"SERVER_NAME = '{EscapeSingleQuoted(context.ServerName)}'");
        builder.AppendLine();
        builder.AppendLine("def success_response(id, result)");
        builder.AppendLine("  payload = { jsonrpc: '2.0', id: id, result: result }");
        builder.AppendLine("  STDOUT.puts(JSON.generate(payload))");
        builder.AppendLine("  STDOUT.flush");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("def error_response(id, code, message)");
        builder.AppendLine("  payload = { jsonrpc: '2.0', id: id, error: { code: code, message: message } }");
        builder.AppendLine("  STDOUT.puts(JSON.generate(payload))");
        builder.AppendLine("  STDOUT.flush");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("def mcp_tools");
        builder.AppendLine("  TOOLS.map do |name, config|");
        builder.AppendLine("    properties = {}");
        builder.AppendLine("    required = []");
        builder.AppendLine("    config[:params].each do |param|");
        builder.AppendLine("      properties[param[:name]] = { type: 'string' }");
        builder.AppendLine("      required << param[:name] unless param[:optional]");
        builder.AppendLine("    end");
        builder.AppendLine("    { name: name, inputSchema: { type: 'object', properties: properties, required: required } }");
        builder.AppendLine("  end");
        builder.AppendLine("end");
        builder.AppendLine();
        builder.AppendLine("STDIN.each_line do |line|");
        builder.AppendLine("  line = line.strip");
        builder.AppendLine("  next if line.empty?");
        builder.AppendLine("  begin");
        builder.AppendLine("    message = JSON.parse(line)");
        builder.AppendLine("  rescue JSON::ParserError");
        builder.AppendLine("    next");
        builder.AppendLine("  end");
        builder.AppendLine();
        builder.AppendLine("  id = message['id']");
        builder.AppendLine("  method = message['method']");
        builder.AppendLine();
        builder.AppendLine("  case method");
        builder.AppendLine("  when 'initialize'");
        builder.AppendLine("    success_response(id, {");
        builder.AppendLine("      protocolVersion: '2024-11-05',");
        builder.AppendLine("      serverInfo: { name: SERVER_NAME, version: '1.0.0' },");
        builder.AppendLine("      capabilities: { tools: {} }");
        builder.AppendLine("    })");
        builder.AppendLine("  when 'tools/list'");
        builder.AppendLine("    success_response(id, { tools: mcp_tools })");
        builder.AppendLine("  when 'tools/call'");
        builder.AppendLine("    params = message['params'] || {}");
        builder.AppendLine("    name = params['name']");
        builder.AppendLine("    args = params['arguments'] || {}");
        builder.AppendLine("    config = TOOLS[name]");
        builder.AppendLine("    if config.nil?");
        builder.AppendLine("      error_response(id, -32602, \"Unknown tool: #{name}\")");
        builder.AppendLine("      next");
        builder.AppendLine("    end");
        builder.AppendLine();
        builder.AppendLine("    begin");
        builder.AppendLine("      value = config[:call].call(args)");
        builder.AppendLine("      success_response(id, { content: [{ type: 'text', text: value.to_s }] })");
        builder.AppendLine("    rescue StandardError => e");
        builder.AppendLine("      error_response(id, -32000, e.message)");
        builder.AppendLine("    end");
        builder.AppendLine("  when 'notifications/initialized'");
        builder.AppendLine("  else");
        builder.AppendLine("    error_response(id, -32601, \"Method not found: #{method}\") unless id.nil?");
        builder.AppendLine("  end");
        builder.AppendLine("end");

        return builder.ToString();
    }

    private static List<ToolRegistration> BuildToolRegistrations(IReadOnlyList<TypeDescriptor> types)
    {
        var result = new List<ToolRegistration>();
        var classInstanceNames = new Dictionary<(string ModuleName, string ClassName), string>();

        foreach (var type in types)
        {
            if (type.Namespace == type.Name)
            {
                foreach (var function in type.Functions)
                {
                    var parametersLiteral = BuildParametersLiteral(function.Parameters);
                    var argsLiteral = BuildArgumentArray(function.Parameters);
                    var callExpression = $"Object.send(:{function.Name}, {argsLiteral})";
                    result.Add(new ToolRegistration(function.Name, parametersLiteral, callExpression));
                }

                continue;
            }

            var key = (type.Namespace, type.Name);
            if (!classInstanceNames.TryGetValue(key, out var instanceName))
            {
                instanceName = "instance_" + SanitizeName($"{type.Namespace}_{type.Name}");
                classInstanceNames[key] = instanceName;
            }

            foreach (var function in type.Functions)
            {
                var parametersLiteral = BuildParametersLiteral(function.Parameters);
                var argsLiteral = BuildArgumentArray(function.Parameters);
                var toolName = $"{type.Name}_{function.Name}";
                var callExpression = $"(@{instanceName} ||= Object.const_get('{EscapeSingleQuoted(type.Name)}').new).send(:{function.Name}, {argsLiteral})";
                result.Add(new ToolRegistration(toolName, parametersLiteral, callExpression));
            }
        }

        return result;
    }

    private static string BuildParametersLiteral(IReadOnlyList<ParameterDescriptor> parameters)
    {
        if (parameters.Count == 0)
            return "[]";

        var values = parameters
            .Select(parameter => $"{{ name: '{EscapeSingleQuoted(parameter.Name)}', optional: {(parameter.IsOptional ? "true" : "false")} }}");
        return "[" + string.Join(", ", values) + "]";
    }

    private static string BuildArgumentArray(IReadOnlyList<ParameterDescriptor> parameters)
    {
        if (parameters.Count == 0)
            return "*[]";

        var values = parameters
            .Select(parameter => $"args['{EscapeSingleQuoted(parameter.Name)}']");
        return "*[" + string.Join(", ", values) + "]";
    }

    private static string GenerateBuildScript() =>
        "require 'fileutils'\n" +
        "\n" +
        "output_path = ARGV[0]\n" +
        "server_name = ARGV[1]\n" +
        "raise 'Missing output path' if output_path.nil? || output_path.empty?\n" +
        "raise 'Missing server name' if server_name.nil? || server_name.empty?\n" +
        "\n" +
        "bundle_dir = File.join(output_path, \"#{server_name}_bundle\")\n" +
        "FileUtils.rm_rf(bundle_dir)\n" +
        "FileUtils.mkdir_p(bundle_dir)\n" +
        "\n" +
        "Dir.glob('**/*.rb', File::FNM_DOTMATCH).each do |entry|\n" +
        "  next if entry.start_with?('.')\n" +
        "  source = File.expand_path(entry)\n" +
        "  next unless File.file?(source)\n" +
        "  target = File.join(bundle_dir, entry)\n" +
        "  FileUtils.mkdir_p(File.dirname(target))\n" +
        "  FileUtils.cp(source, target)\n" +
        "end\n" +
        "\n" +
        "if Gem.win_platform?\n" +
        "  launcher = File.join(output_path, \"#{server_name}.cmd\")\n" +
        "  command = \"@echo off\\r\\nruby \\\"%~dp0#{server_name}_bundle\\\\server.rb\\\" %*\\r\\n\"\n" +
        "  File.write(launcher, command)\n" +
        "else\n" +
        "  launcher = File.join(output_path, server_name)\n" +
        "  command = \"#!/usr/bin/env sh\\nexec ruby \\\"$(dirname \\\"$0\\\")/#{server_name}_bundle/server.rb\\\" \\\"$@\\\"\\n\"\n" +
        "  File.write(launcher, command)\n" +
        "  FileUtils.chmod(0o755, launcher)\n" +
        "end\n";

    private static bool IsExcludedFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.StartsWith("test_", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith("_test.rb", StringComparison.OrdinalIgnoreCase))
            return true;

        var normalized = filePath
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');

        return normalized.Contains("/test/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/spec/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeSingleQuoted(string value) =>
        value.Replace("'", "\\\\'", StringComparison.Ordinal);

    private static string SanitizeName(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        }

        return new string(chars);
    }

    private sealed record SourceMapping(string SourcePath, string TargetRelativePath);

    private sealed record ToolRegistration(string ToolName, string ParametersLiteral, string CallExpression);
}
