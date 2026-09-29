using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class deployment_active_run_index : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deployments_ApplicationId_EnvironmentId_CommitId_Status",
                table: "Deployments");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_ActiveRun",
                table: "Deployments",
                columns: new[] { "ApplicationId", "EnvironmentId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Deploying', 'Destroying')");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_ApplicationId_EnvironmentId_CreatedAt",
                table: "Deployments",
                columns: new[] { "ApplicationId", "EnvironmentId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Deployments_ActiveRun",
                table: "Deployments");

            migrationBuilder.DropIndex(
                name: "IX_Deployments_ApplicationId_EnvironmentId_CreatedAt",
                table: "Deployments");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_ApplicationId_EnvironmentId_CommitId_Status",
                table: "Deployments",
                columns: new[] { "ApplicationId", "EnvironmentId", "CommitId", "Status" },
                unique: true);
        }
    }
}
