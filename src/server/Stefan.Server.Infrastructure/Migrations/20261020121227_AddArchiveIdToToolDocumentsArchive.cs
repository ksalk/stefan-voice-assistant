using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveIdToToolDocumentsArchive : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ToolDocumentArchive",
                schema: "tools",
                table: "ToolDocumentArchive");

            migrationBuilder.AddColumn<Guid>(
                name: "ArchiveId",
                schema: "tools",
                table: "ToolDocumentArchive",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToolDocumentArchive",
                schema: "tools",
                table: "ToolDocumentArchive",
                column: "ArchiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDocumentArchive_Id",
                schema: "tools",
                table: "ToolDocumentArchive",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ToolDocumentArchive",
                schema: "tools",
                table: "ToolDocumentArchive");

            migrationBuilder.DropIndex(
                name: "IX_ToolDocumentArchive_Id",
                schema: "tools",
                table: "ToolDocumentArchive");

            migrationBuilder.DropColumn(
                name: "ArchiveId",
                schema: "tools",
                table: "ToolDocumentArchive");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ToolDocumentArchive",
                schema: "tools",
                table: "ToolDocumentArchive",
                column: "Id");
        }
    }
}
