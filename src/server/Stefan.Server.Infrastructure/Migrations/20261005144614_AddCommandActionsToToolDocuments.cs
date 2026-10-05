using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stefan.Server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandActionsToToolDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommandActions",
                schema: "tools",
                table: "ToolDocuments",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CommandActions",
                schema: "tools",
                table: "ToolDocumentArchive",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_ToolDocuments_CommandActions",
                schema: "tools",
                table: "ToolDocuments",
                column: "CommandActions")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_ToolDocumentArchive_CommandActions",
                schema: "tools",
                table: "ToolDocumentArchive",
                column: "CommandActions")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "jsonb_path_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ToolDocuments_CommandActions",
                schema: "tools",
                table: "ToolDocuments");

            migrationBuilder.DropIndex(
                name: "IX_ToolDocumentArchive_CommandActions",
                schema: "tools",
                table: "ToolDocumentArchive");

            migrationBuilder.DropColumn(
                name: "CommandActions",
                schema: "tools",
                table: "ToolDocuments");

            migrationBuilder.DropColumn(
                name: "CommandActions",
                schema: "tools",
                table: "ToolDocumentArchive");
        }
    }
}
