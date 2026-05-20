using OpenAI.Chat;

namespace Stefan.Server.Application.Tools;

public class ToolRegistry
{
    private readonly Dictionary<string, ITool> _toolsByName;
    private readonly List<ChatTool> _toolDefinitions;

    public ToolRegistry(IEnumerable<ITool> tools)
    {
        _toolsByName = tools.ToDictionary(t => t.GetType().Name, t => t);
        _toolDefinitions = _toolsByName.Values.Select(t => t.Definition).ToList();
    }

    public ITool GetTool(string toolName)
    {
        if (_toolsByName.TryGetValue(toolName, out var tool))
        {
            return tool;
        }

        throw new KeyNotFoundException($"Tool with name {toolName} not found.");
    }

    public ChatTool GetToolDefinition(string toolName)
    {
        return GetTool(toolName).Definition;
    }

    public List<ChatTool> GetAllToolDefinitions()
    {
        return _toolDefinitions;
    }
}