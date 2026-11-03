using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWakeWordAudioToCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "WakeWordAudio",
                table: "CommandRecords",
                type: "bytea",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WakeWordAudioDurationMs",
                table: "CommandRecords",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WakeWordAudioFormat",
                table: "CommandRecords",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WakeWordAudio",
                table: "CommandRecords");

            migrationBuilder.DropColumn(
                name: "WakeWordAudioDurationMs",
                table: "CommandRecords");

            migrationBuilder.DropColumn(
                name: "WakeWordAudioFormat",
                table: "CommandRecords");
        }
    }
}
