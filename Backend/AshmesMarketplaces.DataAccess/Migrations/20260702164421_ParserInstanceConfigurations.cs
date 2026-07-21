using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserInstanceConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserInstanceConfigurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    host_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserInstanceConfigurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserInstanceProxyAssignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proxy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserInstanceProxyAssignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserInstanceProxyAssignments_ParserInstanceConfigurations~",
                        column: x => x.parser_instance_configuration_id,
                        principalTable: "ParserInstanceConfigurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParserInstanceProxyAssignments_ParserProxies_proxy_id",
                        column: x => x.proxy_id,
                        principalTable: "ParserProxies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstanceConfigurations_parser_instance_id",
                table: "ParserInstanceConfigurations",
                column: "parser_instance_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstanceConfigurations_status",
                table: "ParserInstanceConfigurations",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstanceProxyAssignments_enabled",
                table: "ParserInstanceProxyAssignments",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstanceProxyAssignments_parser_instance_configuratio~",
                table: "ParserInstanceProxyAssignments",
                column: "parser_instance_configuration_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstanceProxyAssignments_proxy_id",
                table: "ParserInstanceProxyAssignments",
                column: "proxy_id",
                unique: true,
                filter: "enabled = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserInstanceProxyAssignments");

            migrationBuilder.DropTable(
                name: "ParserInstanceConfigurations");
        }
    }
}
