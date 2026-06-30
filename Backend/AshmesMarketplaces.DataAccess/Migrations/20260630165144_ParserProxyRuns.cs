using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserProxyRuns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    external_proxy_run_id = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    proxy_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    planned_products_count = table.Column<int>(type: "integer", nullable: false),
                    downloaded_products_count = table.Column<int>(type: "integer", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_heartbeat_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    finished_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProxyRuns", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_finished_at_utc",
                table: "ParserProxyRuns",
                column: "finished_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_external_proxy_run_id",
                table: "ParserProxyRuns",
                columns: new[] { "parser_instance_id", "external_proxy_run_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_started_at_utc",
                table: "ParserProxyRuns",
                columns: new[] { "parser_instance_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_proxy_key_status",
                table: "ParserProxyRuns",
                columns: new[] { "proxy_key", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_started_at_utc",
                table: "ParserProxyRuns",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_status",
                table: "ParserProxyRuns",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserProxyRuns");
        }
    }
}
