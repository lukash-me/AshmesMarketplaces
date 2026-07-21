using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260711184500_WbCategoryScopeSubjectMappings")]
    public partial class WbCategoryScopeSubjectMappings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WbCategoryScopeSubjectMappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_menu_id = table.Column<long>(type: "bigint", nullable: false),
                    menu_token = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    subject_id = table.Column<long>(type: "bigint", nullable: false),
                    subject_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mapping_source = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WbCategoryScopeSubjectMappings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WbCategoryScopeSubjectMappings_source_path",
                table: "WbCategoryScopeSubjectMappings",
                column: "source_path");

            migrationBuilder.CreateIndex(
                name: "IX_WbCategoryScopeSubjectMappings_wb_menu_id_status",
                table: "WbCategoryScopeSubjectMappings",
                columns: new[] { "wb_menu_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_WbCategoryScopeSubjectMappings_wb_menu_id_subject_id",
                table: "WbCategoryScopeSubjectMappings",
                columns: new[] { "wb_menu_id", "subject_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WbCategoryScopeSubjectMappings");
        }
    }
}
