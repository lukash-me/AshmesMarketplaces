using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProductPresenceDetection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "parser_cycle_id",
                table: "ParserLaunchRequests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "legacy");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_missing_detected_at_utc",
                table: "ParserCurrentProductRows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_presence_checked_parser_cycle_id",
                table: "ParserCurrentProductRows",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_seen_at_utc",
                table: "ParserCurrentProductRows",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_seen_parser_cycle_id",
                table: "ParserCurrentProductRows",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_seen_parser_proxy_run_id",
                table: "ParserCurrentProductRows",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "marketplace_presence_status",
                table: "ParserCurrentProductRows",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.CreateTable(
                name: "ParserProductPresenceEvents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    parser_cycle_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    old_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    new_status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    reason = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProductPresenceEvents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLaunchRequests_parser_cycle_id",
                table: "ParserLaunchRequests",
                column: "parser_cycle_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_last_seen_at_utc",
                table: "ParserCurrentProductRows",
                column: "last_seen_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserCurrentProductRows_source_category_source_subcategory~",
                table: "ParserCurrentProductRows",
                columns: new[] { "source_category", "source_subcategory", "marketplace_presence_status", "last_seen_parser_cycle_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductPresenceEvents_parser_cycle_id_source_category~",
                table: "ParserProductPresenceEvents",
                columns: new[] { "parser_cycle_id", "source_category", "source_subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProductPresenceEvents_wb_product_id_created_at_utc",
                table: "ParserProductPresenceEvents",
                columns: new[] { "wb_product_id", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserProductPresenceEvents");

            migrationBuilder.DropIndex(
                name: "IX_ParserLaunchRequests_parser_cycle_id",
                table: "ParserLaunchRequests");

            migrationBuilder.DropIndex(
                name: "IX_ParserCurrentProductRows_last_seen_at_utc",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropIndex(
                name: "IX_ParserCurrentProductRows_source_category_source_subcategory~",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "parser_cycle_id",
                table: "ParserLaunchRequests");

            migrationBuilder.DropColumn(
                name: "last_missing_detected_at_utc",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "last_presence_checked_parser_cycle_id",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "last_seen_at_utc",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "last_seen_parser_cycle_id",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "last_seen_parser_proxy_run_id",
                table: "ParserCurrentProductRows");

            migrationBuilder.DropColumn(
                name: "marketplace_presence_status",
                table: "ParserCurrentProductRows");
        }
    }
}
