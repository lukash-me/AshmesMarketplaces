using System;
using AshmesMarketplaces.DataAccess;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260629150000_ParserBatchQueue")]
    public partial class ParserBatchQueue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParserInstances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    display_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserInstances", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserNicheAssignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    proxy_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserNicheAssignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserBatchSubmissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parser_instance_id = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    external_batch_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_category = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    source_subcategory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    proxy_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    batch_kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    content_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    attempts_count = table.Column<int>(type: "integer", nullable: false),
                    accepted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processing_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    acknowledged_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserBatchSubmissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserBatchArtifacts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserBatchArtifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ParserBatchSubmissionEvents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParserBatchSubmissionEvents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchArtifacts_batch_submission_id",
                table: "ParserBatchArtifacts",
                column: "batch_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchArtifacts_batch_submission_id_artifact_kind",
                table: "ParserBatchArtifacts",
                columns: new[] { "batch_submission_id", "artifact_kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissionEvents_batch_submission_id",
                table: "ParserBatchSubmissionEvents",
                column: "batch_submission_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissionEvents_created_at_utc",
                table: "ParserBatchSubmissionEvents",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_accepted_at_utc",
                table: "ParserBatchSubmissions",
                column: "accepted_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_external_batch_id",
                table: "ParserBatchSubmissions",
                column: "external_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_parser_instance_id_external_batch_id",
                table: "ParserBatchSubmissions",
                columns: new[] { "parser_instance_id", "external_batch_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_source_category_source_subcategory",
                table: "ParserBatchSubmissions",
                columns: new[] { "source_category", "source_subcategory" });

            migrationBuilder.CreateIndex(
                name: "IX_ParserBatchSubmissions_status",
                table: "ParserBatchSubmissions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstances_last_seen_at_utc",
                table: "ParserInstances",
                column: "last_seen_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstances_parser_instance_id",
                table: "ParserInstances",
                column: "parser_instance_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserInstances_status",
                table: "ParserInstances",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_ParserNicheAssignments_is_enabled",
                table: "ParserNicheAssignments",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "IX_ParserNicheAssignments_parser_instance_id_source_category_source_subcategory",
                table: "ParserNicheAssignments",
                columns: new[] { "parser_instance_id", "source_category", "source_subcategory" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ParserNicheAssignments_proxy_key",
                table: "ParserNicheAssignments",
                column: "proxy_key");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ParserBatchArtifacts");
            migrationBuilder.DropTable(name: "ParserBatchSubmissionEvents");
            migrationBuilder.DropTable(name: "ParserBatchSubmissions");
            migrationBuilder.DropTable(name: "ParserNicheAssignments");
            migrationBuilder.DropTable(name: "ParserInstances");
        }
    }
}
