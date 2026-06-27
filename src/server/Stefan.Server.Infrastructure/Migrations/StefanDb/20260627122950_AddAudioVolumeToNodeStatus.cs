using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations.StefanDb
{
    /// <inheritdoc />
    public partial class AddAudioVolumeToNodeStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AudioVolume",
                table: "NodeStatusReports",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AudioVolume",
                table: "NodeStatusReports");
        }
    }
}
