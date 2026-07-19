using BtwDocumentDesigner.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwDocumentDesigner.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260719103000_AddImageResourceKey")]
public partial class AddImageResourceKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ResourceKey",
            table: "PdfDesignImages",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_PdfDesignImages_ResourceKey",
            table: "PdfDesignImages",
            column: "ResourceKey",
            unique: true,
            filter: "\"ResourceKey\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_PdfDesignImages_ResourceKey",
            table: "PdfDesignImages");

        migrationBuilder.DropColumn(
            name: "ResourceKey",
            table: "PdfDesignImages");
    }
}
