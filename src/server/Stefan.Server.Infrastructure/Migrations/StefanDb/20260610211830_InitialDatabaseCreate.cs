using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations.StefanDb
{
    /// <inheritdoc />
    public partial class InitialDatabaseCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    TotalDurationMs = table.Column<double>(type: "double precision", nullable: false)
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
                    Version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    GitCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NodeStatusReports", x => x.Id);
                });
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
        }
    }
}
