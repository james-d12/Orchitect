using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class deployment_run_token_hash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuns_TokenHash",
                table: "DeploymentRuns",
                column: "TokenHash",
                unique: true,
                filter: "\"TokenHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DeploymentRuns_TokenHash",
                table: "DeploymentRuns");
        }
    }
}
