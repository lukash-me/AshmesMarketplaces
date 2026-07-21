using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyRunSessionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "egress_ip",
                table: "ParserProxyRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "session_status",
                table: "ParserProxyRuns",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_ref",
                table: "ParserProxyRuns",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "egress_ip",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "session_status",
                table: "ParserProxyRuns");

            migrationBuilder.DropColumn(
                name: "token_ref",
                table: "ParserProxyRuns");
        }
    }
}
