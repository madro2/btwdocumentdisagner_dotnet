using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwDocumentDesigner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PdfDesignImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    ImageData = table.Column<byte[]>(type: "bytea", nullable: false),
                    UploadDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfDesignImages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PdfDesignTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentType = table.Column<string>(type: "text", nullable: false),
                    DesignName = table.Column<string>(type: "text", nullable: false),
                    DesignVersion = table.Column<int>(type: "integer", nullable: false),
                    JsonConfiguration = table.Column<string>(type: "text", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModificationUser = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PdfDesignTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PdfDesignTemplates_DesignName_DesignVersion",
                table: "PdfDesignTemplates",
                columns: new[] { "DesignName", "DesignVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PdfDesignImages");

            migrationBuilder.DropTable(
                name: "PdfDesignTemplates");
        }
    }
}
