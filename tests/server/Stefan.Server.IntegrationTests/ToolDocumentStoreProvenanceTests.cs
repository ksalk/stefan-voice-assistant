using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Stefan.Server.Domain;
using Stefan.Server.Domain.ToolEntities;
using Stefan.Server.Infrastructure;
using Testcontainers.PostgreSql;

namespace Stefan.Server.IntegrationTests;

public class ToolDocumentStoreProvenanceTests
{
    private const string DbImageName = "stefan-db:test";
    private const string DbUser = "stefan";
    private const string DbPassword = "changeme";
    private const string DbName = "stefan_db";

    [Fact]
    public async Task Store_RecordsCommandActions_AcrossLiveAndArchive()
    {
        var db = new PostgreSqlBuilder(DbImageName)
            .WithDatabase(DbName)
            .WithUsername(DbUser)
            .WithPassword(DbPassword)
            .Build();
        await db.StartAsync();
        try
        {
            var store = CreateStore(db.GetConnectionString());

            var commandId = Guid.NewGuid();
            var timer = new TimerEntry { Id = Guid.NewGuid(), DurationInSeconds = 30, Label = "pasta", CreatedAt = DateTime.UtcNow };
            var item = new ShoppingListItem { Id = Guid.NewGuid(), Name = "milk" };

            await store.AddAsync(timer, commandId);
            await store.UpdateAsync(timer, commandId);
            await store.AddAsync(item, commandId);
            await store.DeleteAsync<TimerEntry>(timer.Id, commandId);

            var refs = await store.ListByCommandAsync(commandId);
            Assert.Equal(2, refs.Count);
            Assert.Contains(refs, r => r.Type == "timer");
            Assert.Contains(refs, r => r.Type == "shopping-item");

            var timers = await store.ListByCommandAsync<TimerEntry>(commandId);
            var timerDoc = Assert.Single(timers);
            Assert.Equal(timer.Id, timerDoc.Id);
            Assert.Equal(timer.DurationInSeconds, timerDoc.DurationInSeconds);
            Assert.Equal(timer.Label, timerDoc.Label);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Store_PreservesFullActionTrail_InArchive()
    {
        var db = new PostgreSqlBuilder(DbImageName)
            .WithDatabase(DbName)
            .WithUsername(DbUser)
            .WithPassword(DbPassword)
            .Build();
        await db.StartAsync();
        try
        {
            var store = CreateStore(db.GetConnectionString());
            var commandId = Guid.NewGuid();
            var timer = new TimerEntry { Id = Guid.NewGuid(), DurationInSeconds = 10, CreatedAt = DateTime.UtcNow };

            await store.AddAsync(timer, commandId);
            await store.UpdateAsync(timer, commandId);
            await store.DeleteAsync<TimerEntry>(timer.Id, commandId);

            var trail = await GetCommandActionsAsync(db.GetConnectionString(), timer.Id, archived: true);
            Assert.Equal(
                new[]
                {
                    (ToolDocumentCommandActionType.Created, commandId),
                    (ToolDocumentCommandActionType.Updated, commandId),
                    (ToolDocumentCommandActionType.Deleted, commandId),
                },
                trail.Select(a => (a.Action, a.CommandId)).ToArray());
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task Store_RecordsNothing_WhenCommandIdIsNull()
    {
        var db = new PostgreSqlBuilder(DbImageName)
            .WithDatabase(DbName)
            .WithUsername(DbUser)
            .WithPassword(DbPassword)
            .Build();
        await db.StartAsync();
        try
        {
            var store = CreateStore(db.GetConnectionString());
            var timer = new TimerEntry { Id = Guid.NewGuid(), DurationInSeconds = 10, CreatedAt = DateTime.UtcNow };

            await store.AddAsync(timer, commandId: null);
            await store.DeleteAsync<TimerEntry>(timer.Id, commandId: null);

            var trail = await GetCommandActionsAsync(db.GetConnectionString(), timer.Id, archived: true);
            Assert.Empty(trail);
        }
        finally
        {
            await db.DisposeAsync();
        }
    }

    private static ToolDocumentStore CreateStore(string connectionString)
    {
        var options = new DbContextOptionsBuilder<StefanDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new ToolDocumentStore(new StefanDbContext(options));
    }

    private static async Task<List<ToolDocumentCommandAction>> GetCommandActionsAsync(
        string connectionString, Guid documentId, bool archived)
    {
        var table = archived ? "tools.\"ToolDocumentArchive\"" : "tools.\"ToolDocuments\"";
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            $"""SELECT "CommandActions" FROM {table} WHERE "Id" = @id""",
            connection);
        command.Parameters.AddWithValue("id", documentId);

        var json = (string)(await command.ExecuteScalarAsync())!;
        return JsonSerializer.Deserialize<List<ToolDocumentCommandAction>>(json)
               ?? throw new InvalidOperationException("CommandActions was null.");
    }
}
