using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class update_inventory_domain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Owners_OwnerId",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Owners_OwnerId",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropTable(
                name: "TicketingUsers");

            migrationBuilder.DropTable(
                name: "WorkItems",
                schema: "inventory");

            migrationBuilder.RenameTable(
                name: "DiscoveryConfigurations",
                newName: "DiscoveryConfigurations",
                newSchema: "inventory");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                schema: "inventory",
                table: "Repositories",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Repositories_OwnerId",
                schema: "inventory",
                table: "Repositories",
                newName: "IX_Repositories_UserId");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                schema: "inventory",
                table: "Pipelines",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Pipelines_OwnerId",
                schema: "inventory",
                table: "Pipelines",
                newName: "IX_Pipelines_UserId");

            migrationBuilder.CreateTable(
                name: "Issues",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Issues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Issues_Organisations",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OrganisationId",
                schema: "inventory",
                table: "Issues",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Issues_OrganisationId_Platform",
                schema: "inventory",
                table: "Issues",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.AddForeignKey(
                name: "FK_DiscoveryConfigurations_Credentials",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                column: "CredentialId",
                principalTable: "Credentials",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscoveryConfigurations_Organisations",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Owners_UserId",
                schema: "inventory",
                table: "Pipelines",
                column: "UserId",
                principalSchema: "inventory",
                principalTable: "Owners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Owners_UserId",
                schema: "inventory",
                table: "Repositories",
                column: "UserId",
                principalSchema: "inventory",
                principalTable: "Owners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscoveryConfigurations_Credentials",
                schema: "inventory",
                table: "DiscoveryConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_DiscoveryConfigurations_Organisations",
                schema: "inventory",
                table: "DiscoveryConfigurations");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Owners_UserId",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Owners_UserId",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropTable(
                name: "Issues",
                schema: "inventory");

            migrationBuilder.RenameTable(
                name: "DiscoveryConfigurations",
                schema: "inventory",
                newName: "DiscoveryConfigurations");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "inventory",
                table: "Repositories",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Repositories_UserId",
                schema: "inventory",
                table: "Repositories",
                newName: "IX_Repositories_OwnerId");

            migrationBuilder.RenameColumn(
                name: "UserId",
                schema: "inventory",
                table: "Pipelines",
                newName: "OwnerId");

            migrationBuilder.RenameIndex(
                name: "IX_Pipelines_UserId",
                schema: "inventory",
                table: "Pipelines",
                newName: "IX_Pipelines_OwnerId");

            migrationBuilder.CreateTable(
                name: "TicketingUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
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
                    Description = table.Column<string>(type: "text", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItems_Organisations",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketingUsers_Email",
                table: "TicketingUsers",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_OrganisationId",
                schema: "inventory",
                table: "WorkItems",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_OrganisationId_Platform",
                schema: "inventory",
                table: "WorkItems",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Owners_OwnerId",
                schema: "inventory",
                table: "Pipelines",
                column: "OwnerId",
                principalSchema: "inventory",
                principalTable: "Owners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Owners_OwnerId",
                schema: "inventory",
                table: "Repositories",
                column: "OwnerId",
                principalSchema: "inventory",
                principalTable: "Owners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
