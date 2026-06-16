using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AshmesMarketplaces.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260615093000_WorkspaceReadStateLogisticsFactors")]
    public partial class WorkspaceReadStateLogisticsFactors : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "baseline_logistics_factors",
                table: "WorkspaceMarketProductUserReadStates",
                type: "jsonb",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "baseline_logistics_factors",
                table: "WorkspaceMarketProductUserReadStates");
        }
    }
}
