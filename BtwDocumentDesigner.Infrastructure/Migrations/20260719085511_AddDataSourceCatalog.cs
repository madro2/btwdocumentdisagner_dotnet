using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BtwDocumentDesigner.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDataSourceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataSourceCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataSourceFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DataSourceCollectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Cardinality = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Group = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataSourceFields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataSourceFields_DataSourceCollections_DataSourceCollection~",
                        column: x => x.DataSourceCollectionId,
                        principalTable: "DataSourceCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceCollections_Name",
                table: "DataSourceCollections",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataSourceFields_DataSourceCollectionId_Path",
                table: "DataSourceFields",
                columns: new[] { "DataSourceCollectionId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataSourceFields");

            migrationBuilder.DropTable(
                name: "DataSourceCollections");
        }
    }
}
