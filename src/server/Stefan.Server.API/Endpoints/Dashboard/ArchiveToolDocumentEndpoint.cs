using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class ArchiveToolDocumentEndpoint
{
    public static void MapArchiveToolDocumentEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("api/tool-documents/{id:guid}/archive", async (
            Guid id,
            [FromServices] ArchiveToolDocument archiveToolDocument,
            CancellationToken cancellationToken) =>
        {
            var result = await archiveToolDocument.Handle(
                new ArchiveToolDocumentRequest { Id = id },
                cancellationToken);

            return result.ToHttpResult(_ => Results.NoContent());
        })
        .WithName("ArchiveToolDocument");
    }
}
