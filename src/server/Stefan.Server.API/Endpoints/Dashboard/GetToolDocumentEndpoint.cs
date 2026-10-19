using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetToolDocumentEndpoint
{
    public static void MapGetToolDocumentEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/tool-documents/{id:guid}", async (
            Guid id,
            [FromServices] GetToolDocument getToolDocument,
            CancellationToken cancellationToken) =>
        {
            var result = await getToolDocument.Handle(new GetToolDocumentRequest { Id = id }, cancellationToken);

            return result.ToHttpResult(Results.Ok);
        })
        .WithName("GetToolDocument");
    }
}
