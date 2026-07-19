using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwDocumentDesigner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSystemDefaultValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemDefaultValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemDefaultValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemDefaultValues_Key",
                table: "SystemDefaultValues",
                column: "Key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemDefaultValues");
        }
    }
}
