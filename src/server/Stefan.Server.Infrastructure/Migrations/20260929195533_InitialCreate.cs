using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "tools");

            migrationBuilder.CreateTable(
                name: "CommandRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<string>(type: "text", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    InputAudio = table.Column<byte[]>(type: "bytea", nullable: false),
                    InputAudioFormat = table.Column<string>(type: "text", nullable: false),
                    InputAudioDurationMs = table.Column<double>(type: "double precision", nullable: false),
                    Transcript = table.Column<string>(type: "text", nullable: true),
                    LlmConversationJson = table.Column<string>(type: "text", nullable: true),
                    ResponseText = table.Column<string>(type: "text", nullable: true),
                    OutputAudio = table.Column<byte[]>(type: "bytea", nullable: true),
                    OutputAudioFormat = table.Column<string>(type: "text", nullable: true),
                    SttDurationMs = table.Column<double>(type: "double precision", nullable: true),
                    LlmDurationMs = table.Column<double>(type: "double precision", nullable: true),
                    TtsDurationMs = table.Column<double>(type: "double precision", nullable: true),
                    TotalDurationMs = table.Column<double>(type: "double precision", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommandRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CurrentSessionId = table.Column<string>(type: "text", nullable: false),
                    LastKnownIpAddress = table.Column<string>(type: "text", nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastPingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestartCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NodeStatusReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CpuUsage = table.Column<double>(type: "double precision", nullable: true),
                    MemoryUsage = table.Column<double>(type: "double precision", nullable: true),
                    DiskUsage = table.Column<double>(type: "double precision", nullable: true),
                    AudioVolume = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GitCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeStatusReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolDocumentArchive",
                schema: "tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ArchivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolDocumentArchive", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ToolDocuments",
                schema: "tools",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ToolDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ToolDocumentArchive_Type",
                schema: "tools",
                table: "ToolDocumentArchive",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDocuments_Type",
                schema: "tools",
                table: "ToolDocuments",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommandRecords");

            migrationBuilder.DropTable(
                name: "Nodes");

            migrationBuilder.DropTable(
                name: "NodeStatusReports");

            migrationBuilder.DropTable(
                name: "ToolDocumentArchive",
                schema: "tools");

            migrationBuilder.DropTable(
                name: "ToolDocuments",
                schema: "tools");
        }
    }
}
