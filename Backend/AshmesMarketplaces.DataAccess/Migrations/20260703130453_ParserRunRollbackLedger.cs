using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserRunRollbackLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_proxy_run_id",
                table: "ParserBatchSubmissions",
                type: "character varying(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parser_cycle_id",
                table: "ParserBatchSubmissions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parser_proxy_run_id",
                table: "ParserBatchSubmissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ParserRunCurrentEntityEffects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_proxy_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_batch_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_key = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    effect_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    old_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    new_value_json = table.Column<string>(type: "jsonb", nullable: true),
                    rollback_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rollback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rolled_back_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRunCurrentEntityEffects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserRunProductEffects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_proxy_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_batch_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    wb_product_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    service_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effect_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rollback_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rollback_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    rolled_back_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRunProductEffects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserRunRollbacks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_proxy_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_products_count = table.Column<int>(type: "integer", nullable: false),
                    updated_products_count = table.Column<int>(type: "integer", nullable: false),
                    deleted_products_count = table.Column<int>(type: "integer", nullable: false),
                    restored_products_count = table.Column<int>(type: "integer", nullable: false),
                    conflict_products_count = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserRunRollbacks", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_parser_instance_id_external_proxy_ru~",
                table: "ParserBatchSubmissions",
                columns: new[] { "parser_instance_id", "external_proxy_run_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_parser_proxy_run_id",
                table: "ParserBatchSubmissions",
                column: "parser_proxy_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunCurrentEntityEffects_parser_batch_submission_id",
                table: "ParserRunCurrentEntityEffects",
                column: "parser_batch_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunCurrentEntityEffects_parser_proxy_run_id_wb_produc~",
                table: "ParserRunCurrentEntityEffects",
                columns: new[] { "parser_proxy_run_id", "wb_product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunCurrentEntityEffects_rollback_status",
                table: "ParserRunCurrentEntityEffects",
                column: "rollback_status");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunCurrentEntityEffects_wb_product_id_entity_kind_ent~",
                table: "ParserRunCurrentEntityEffects",
                columns: new[] { "wb_product_id", "entity_kind", "entity_key", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunProductEffects_parser_batch_submission_id",
                table: "ParserRunProductEffects",
                column: "parser_batch_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunProductEffects_parser_proxy_run_id_rollback_status",
                table: "ParserRunProductEffects",
                columns: new[] { "parser_proxy_run_id", "rollback_status" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunProductEffects_parser_proxy_run_id_wb_product_id",
                table: "ParserRunProductEffects",
                columns: new[] { "parser_proxy_run_id", "wb_product_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunProductEffects_wb_product_id_created_at_utc",
                table: "ParserRunProductEffects",
                columns: new[] { "wb_product_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunRollbacks_parser_proxy_run_id",
                table: "ParserRunRollbacks",
                column: "parser_proxy_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserRunRollbacks_status",
                table: "ParserRunRollbacks",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParserRunCurrentEntityEffects");

            migrationBuilder.DropTable(
                name: "ParserRunProductEffects");

            migrationBuilder.DropTable(
                name: "ParserRunRollbacks");

            migrationBuilder.DropIndex(
                name: "IX_ParserBatchSubmissions_parser_instance_id_external_proxy_ru~",
                table: "ParserBatchSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_ParserBatchSubmissions_parser_proxy_run_id",
                table: "ParserBatchSubmissions");

            migrationBuilder.DropColumn(
                name: "external_proxy_run_id",
                table: "ParserBatchSubmissions");

            migrationBuilder.DropColumn(
                name: "parser_cycle_id",
                table: "ParserBatchSubmissions");

            migrationBuilder.DropColumn(
                name: "parser_proxy_run_id",
                table: "ParserBatchSubmissions");
        }
    }
}
