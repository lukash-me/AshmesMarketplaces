using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserProxies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ip = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    http_port = table.Column<int>(type: "integer", nullable: false),
                    socks_port = table.Column<int>(type: "integer", nullable: false),
                    login = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    encrypted_password = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProxies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserProxyNicheAssignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    proxy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_category_id = table.Column<long>(type: "bigint", nullable: false),
                    source_category = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    search_query = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserProxyNicheAssignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_ParserProxyNicheAssignments_ParserProxies_proxy_id",
                        column: x => x.proxy_id,
                        principalTable: "ParserProxies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxies_key",
                table: "ParserProxies",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxies_status",
                table: "ParserProxies",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyNicheAssignments_enabled",
                table: "ParserProxyNicheAssignments",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyNicheAssignments_proxy_id",
                table: "ParserProxyNicheAssignments",
                column: "proxy_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyNicheAssignments_wb_category_id",
                table: "ParserProxyNicheAssignments",
                column: "wb_category_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserProxyNicheAssignments");

            migrationBuilder.DropTable(
                name: "ParserProxies");
        }
    }
}
