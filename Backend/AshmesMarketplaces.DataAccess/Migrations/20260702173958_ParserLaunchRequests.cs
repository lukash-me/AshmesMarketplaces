using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserLaunchRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserLaunchRequests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    launch_mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    proxy_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    batch_limit = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserLaunchRequests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLaunchRequests_parser_instance_configuration_id_status",
                table: "ParserLaunchRequests",
                columns: new[] { "parser_instance_configuration_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserLaunchRequests_status_requested_at_utc",
                table: "ParserLaunchRequests",
                columns: new[] { "status", "requested_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserLaunchRequests");
        }
    }
}
