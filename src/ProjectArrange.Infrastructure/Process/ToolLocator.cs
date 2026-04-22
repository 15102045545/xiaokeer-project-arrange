using Microsoft.Extensions.Configuration;

namespace ProjectArrange.Infrastructure.Process;

public sealed class ToolLocator(IConfiguration configuration) : IToolLocator
{
    public string Resolve(string toolName)
    {
        var name = toolName.Trim();
        if (string.IsNullOrWhiteSpace(name)) return toolName;

        var overridePath = configuration[$"Tools:{name}"];
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath.Trim();

        return toolName;
    }
}

