using Microsoft.AspNetCore.Mvc;
using Stefan.Server.Application.Commands;

namespace Stefan.Server.API.Endpoints.Dashboard;

public static class GetToolDocumentsEndpoint
{
    public static void MapGetToolDocumentsEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("api/tool-documents", async (
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? scope,
            [FromQuery] string? type,
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortDirection,
            [FromServices] GetToolDocuments getToolDocuments,
            CancellationToken cancellationToken) =>
        {
            var result = await getToolDocuments.Handle(new GetToolDocumentsRequest
            {
                Page = page ?? 1,
                PageSize = pageSize ?? 20,
                Scope = scope ?? "live",
                Type = type,
                Search = search,
                SortBy = sortBy,
                SortDirection = sortDirection
            }, cancellationToken);

            return Results.Ok(result);
        })
        .WithName("GetToolDocuments");
    }
}
