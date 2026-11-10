using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stefan.Server.Application.Tools.Timer;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;

namespace Stefan.Server.Application.Commands;

public class ArchiveToolDocumentRequest
{
    public Guid Id { get; set; }
}

public class ArchiveToolDocument(
    StefanDbContext dbContext,
    IToolDocumentStore documentStore,
    CancelTimerJob cancelTimerJob,
    ILogger<ArchiveToolDocument> logger)
{
    public async Task<Result<bool>> Handle(
        ArchiveToolDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var documentType = await dbContext.ToolDocuments
            .AsNoTracking()
            .Where(d => d.Id == request.Id)
            .Select(d => d.Type)
            .FirstOrDefaultAsync(cancellationToken);

        if (documentType is null)
        {
            return Result<bool>.Failure(Error.NotFound("Tool document not found"));
        }

        // DeleteAsync archives the document and removes it from the live table.
        await documentStore.DeleteAsync(request.Id, commandId: null, cancellationToken);

        // A live timer also has a scheduled Quartz job. Archive first, then
        // cancel the job, so a cancellation failure cannot leave a live timer
        // without its alert.
        if (documentType == TimerEntry.DocumentType)
        {
            try
            {
                // The document is already archived. Finish the cleanup even if
                // the client disconnects.
                await cancelTimerJob.Handle(request.Id, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // FireTimerJob checks that the timer document is still live
                // before it sends an alert, so the archive stays safe.
                logger.LogError(
                    ex,
                    "Failed to cancel the scheduled job for archived timer {TimerId}.",
                    request.Id);
            }
        }

        return Result<bool>.Success(true);
    }
}
