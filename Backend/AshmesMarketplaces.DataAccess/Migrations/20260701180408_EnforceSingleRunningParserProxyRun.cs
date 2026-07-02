using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleRunningParserProxyRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH ranked AS (
                    SELECT
                        id,
                        row_number() OVER (
                            PARTITION BY parser_instance_id, proxy_key
                            ORDER BY started_at_utc DESC, id DESC
                        ) AS rn
                    FROM "ParserProxyRuns"
                    WHERE status = 'running'
                )
                UPDATE "ParserProxyRuns" AS runs
                SET
                    status = 'failed',
                    finished_at_utc = now(),
                    error = COALESCE(NULLIF(error, ''), 'Stopped by single-running-proxy migration.'),
                    updated_at_utc = now()
                FROM ranked
                WHERE runs.id = ranked.id AND ranked.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_proxy_key",
                table: "ParserProxyRuns",
                columns: new[] { "parser_instance_id", "proxy_key" },
                unique: true,
                filter: "status = 'running'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ParserProxyRuns_parser_instance_id_proxy_key",
                table: "ParserProxyRuns");
        }
    }
}
