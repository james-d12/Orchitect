using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class discovery_runs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveryRuns",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscoveryConfigurationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TeamCount = table.Column<int>(type: "integer", nullable: false),
                    RepositoryCount = table.Column<int>(type: "integer", nullable: false),
                    PipelineCount = table.Column<int>(type: "integer", nullable: false),
                    PullRequestCount = table.Column<int>(type: "integer", nullable: false),
                    IssueCount = table.Column<int>(type: "integer", nullable: false),
                    CloudResourceCount = table.Column<int>(type: "integer", nullable: false),
                    CloudSecretCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveryRuns_DiscoveryConfigurations",
                        column: x => x.DiscoveryConfigurationId,
                        principalSchema: "inventory",
                        principalTable: "DiscoveryConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryRuns_DiscoveryConfigurationId_StartedAt",
                schema: "inventory",
                table: "DiscoveryRuns",
                columns: new[] { "DiscoveryConfigurationId", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveryRuns",
                schema: "inventory");
        }
    }
}
