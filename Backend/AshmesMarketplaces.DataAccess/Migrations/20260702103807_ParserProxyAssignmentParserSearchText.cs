using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ParserProxyAssignmentParserSearchText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "parser_search_text",
                table: "ParserProxyNicheAssignments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "ParserProxyNicheAssignments"
                SET "parser_search_text" = "source_subcategory"
                WHERE "parser_search_text" IS NULL OR btrim("parser_search_text") = ''
                """);

            migrationBuilder.AlterColumn<string>(
                name: "parser_search_text",
                table: "ParserProxyNicheAssignments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "parser_search_text",
                table: "ParserProxyNicheAssignments");
        }
    }
}
