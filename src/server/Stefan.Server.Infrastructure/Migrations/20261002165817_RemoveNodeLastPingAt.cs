using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveNodeLastPingAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPingAt",
                table: "Nodes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastPingAt",
                table: "Nodes",
                type: "timestamp with time zone",
                nullable: true);
        }
    }
}
