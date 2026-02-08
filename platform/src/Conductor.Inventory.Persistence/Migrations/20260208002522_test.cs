using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class test : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.CreateTable(
                name: "CloudResources",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CloudSecrets",
                schema: "inventory",
                columns: table => new
                {
                    Name = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CloudSecrets", x => new { x.Name, x.Location, x.Platform });
                });

            migrationBuilder.CreateTable(
                name: "Owners",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Owners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PullRequests",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Labels = table.Column<string>(type: "text", nullable: false),
                    Reviewers = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    LastCommit_Id = table.Column<string>(type: "text", nullable: true),
                    LastCommit_Url = table.Column<string>(type: "text", nullable: true),
                    LastCommit_Committer = table.Column<string>(type: "text", nullable: true),
                    LastCommit_Comment = table.Column<string>(type: "text", nullable: true),
                    LastCommit_ChangeCount = table.Column<int>(type: "integer", nullable: true),
                    RepositoryUrl = table.Column<string>(type: "text", nullable: false),
                    RepositoryName = table.Column<string>(type: "text", nullable: false),
                    CreatedOnDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TicketingUsers",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketingUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pipelines",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pipelines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pipelines_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "inventory",
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    DefaultBranch = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Owners_OwnerId",
                        column: x => x.OwnerId,
                        principalSchema: "inventory",
                        principalTable: "Owners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_OwnerId",
                schema: "inventory",
                table: "Pipelines",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_OwnerId",
                schema: "inventory",
                table: "Repositories",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketingUsers_Email",
                schema: "inventory",
                table: "TicketingUsers",
                column: "Email");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CloudResources",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "CloudSecrets",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Pipelines",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "PullRequests",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Repositories",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "TicketingUsers",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "WorkItems",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "Owners",
                schema: "inventory");
        }
    }
}
