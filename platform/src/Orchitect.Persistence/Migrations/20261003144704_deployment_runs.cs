using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class deployment_runs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeploymentRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeploymentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExitCode = table.Column<long>(type: "bigint", nullable: true),
                    ErrorSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RunnerId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LogLocation = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TokenHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeploymentRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeploymentRuns_Deployments_DeploymentId",
                        column: x => x.DeploymentId,
                        principalTable: "Deployments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeploymentRuns_DeploymentId_QueuedAt",
                table: "DeploymentRuns",
                columns: new[] { "DeploymentId", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeploymentRuns");
        }
    }
}
