namespace Stefan.Server.Domain.ToolEntities;

public interface IToolDocument
{
    static abstract string DocumentType { get; }

    Guid Id { get; set; }
}
