using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceMarketProductDemoCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace_wb_product_id_source_s~",
                table: "WorkspaceMarketProducts");

            migrationBuilder.AlterColumn<string>(
                name: "wb_product_id",
                table: "WorkspaceMarketProducts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<Guid>(
                name: "parser_product_row_id",
                table: "WorkspaceMarketProducts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "characteristics_json",
                table: "WorkspaceMarketProducts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "cost_price",
                table: "WorkspaceMarketProducts",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "demo_payload_json",
                table: "WorkspaceMarketProducts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "WorkspaceMarketProducts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_type",
                table: "WorkspaceMarketProducts",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "parser");

            migrationBuilder.AddColumn<string>(
                name: "supplier_name",
                table: "WorkspaceMarketProducts",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "supplier_url",
                table: "WorkspaceMarketProducts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkspaceMarketProductMedia",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    id_workspace_market_product = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    file_name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMarketProductMedia", x => x.id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMarketProductMedia_WorkspaceMarketProducts_id_work~",
                        column: x => x.id_workspace_market_product,
                        principalTable: "WorkspaceMarketProducts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace_wb_product_id_source_s~",
                table: "WorkspaceMarketProducts",
                columns: new[] { "id_workspace", "wb_product_id", "source_subcategory_key", "source_region_dest_key" },
                unique: true,
                filter: "wb_product_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_source_type",
                table: "WorkspaceMarketProducts",
                column: "source_type");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductMedia_id_workspace_market_product",
                table: "WorkspaceMarketProductMedia",
                column: "id_workspace_market_product");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProductMedia_id_workspace_market_product_sor~",
                table: "WorkspaceMarketProductMedia",
                columns: new[] { "id_workspace_market_product", "sort_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspaceMarketProductMedia");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace_wb_product_id_source_s~",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropIndex(
                name: "IX_WorkspaceMarketProducts_source_type",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "characteristics_json",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "cost_price",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "demo_payload_json",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "description",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "source_type",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "supplier_name",
                table: "WorkspaceMarketProducts");

            migrationBuilder.DropColumn(
                name: "supplier_url",
                table: "WorkspaceMarketProducts");

            migrationBuilder.AlterColumn<string>(
                name: "wb_product_id",
                table: "WorkspaceMarketProducts",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "parser_product_row_id",
                table: "WorkspaceMarketProducts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMarketProducts_id_workspace_wb_product_id_source_s~",
                table: "WorkspaceMarketProducts",
                columns: new[] { "id_workspace", "wb_product_id", "source_subcategory_key", "source_region_dest_key" },
                unique: true);
        }
    }
}
