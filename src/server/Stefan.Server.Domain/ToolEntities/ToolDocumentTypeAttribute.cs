namespace Stefan.Server.Domain.ToolEntities;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ToolDocumentTypeAttribute(string type) : Attribute
{
    public string Type { get; } = type;
}
