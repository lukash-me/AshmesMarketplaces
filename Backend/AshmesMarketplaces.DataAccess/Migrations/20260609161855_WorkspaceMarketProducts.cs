using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceMarketProducts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkspaceMarketProducts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace = table.Column<Guid>(type: "uuid", nullable: false),
                    id_created_by_user = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_product_row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    wb_root_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    source_region_dest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    source_subcategory_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_region_dest_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    source_query = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    tag_key = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    brand_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    seller_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    price_regular = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_discounted = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    price_wb_wallet = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    review_rating = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    feedback_count = table.Column<int>(type: "integer", nullable: true),
                    position_absolute = table.Column<int>(type: "integer", nullable: true),
                    total_quantity = table.Column<int>(type: "integer", nullable: true),
                    date_create = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    date_update = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMarketProducts", x => x.id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProducts_ParserProductRows_parser_product_ro~",
                        column: x => x.parser_product_row_id,
                        principalTable: "ParserProductRows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProducts_Users_id_created_by_user",
                        column: x => x.id_created_by_user,
                        principalTable: "Users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProducts_Workspaces_id_workspace",
                        column: x => x.id_workspace,
                        principalTable: "Workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_date_update",
                table: "WorkspaceMarketProducts",
                column: "date_update");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_id_created_by_user",
                table: "WorkspaceMarketProducts",
                column: "id_created_by_user");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace",
                table: "WorkspaceMarketProducts",
                column: "id_workspace");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace_wb_product_id_source_s~",
                table: "WorkspaceMarketProducts",
                columns: new[] { "id_workspace", "wb_product_id", "source_subcategory_key", "source_region_dest_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_parser_product_row_id",
                table: "WorkspaceMarketProducts",
                column: "parser_product_row_id");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_source_subcategory",
                table: "WorkspaceMarketProducts",
                column: "source_subcategory");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_tag_key",
                table: "WorkspaceMarketProducts",
                column: "tag_key");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_wb_product_id",
                table: "WorkspaceMarketProducts",
                column: "wb_product_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceMarketProducts");
        }
    }
}
