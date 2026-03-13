using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Inventory.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscoveryConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiscoveryConfigurations",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<Guid>(type: "uuid", nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Schedule = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PlatformConfig = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveryConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryConfigurations_CredentialId",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                column: "CredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryConfigurations_IsEnabled",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryConfigurations_OrganisationId",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveryConfigurations_OrganisationId_Platform",
                schema: "inventory",
                table: "DiscoveryConfigurations",
                columns: new[] { "OrganisationId", "Platform" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiscoveryConfigurations",
                schema: "inventory");
        }
    }
}
