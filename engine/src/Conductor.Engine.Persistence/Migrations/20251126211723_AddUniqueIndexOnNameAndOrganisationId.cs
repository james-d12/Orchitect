using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnNameAndOrganisationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ResourceTemplates_Name",
                table: "ResourceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Environments_Name",
                table: "Environments");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                table: "ResourceTemplates",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplates_Name_OrganisationId",
                table: "ResourceTemplates",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplates_OrganisationId",
                table: "ResourceTemplates",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Name_OrganisationId",
                table: "Environments",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_OrganisationId",
                table: "Applications",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ResourceTemplates_Organisations_OrganisationId",
                table: "ResourceTemplates",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ResourceTemplates_Organisations_OrganisationId",
                table: "ResourceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ResourceTemplates_Name_OrganisationId",
                table: "ResourceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_ResourceTemplates_OrganisationId",
                table: "ResourceTemplates");

            migrationBuilder.DropIndex(
                name: "IX_Environments_Name_OrganisationId",
                table: "Environments");

            migrationBuilder.DropIndex(
                name: "IX_Applications_Name_OrganisationId",
                table: "Applications");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "ResourceTemplates");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplates_Name",
                table: "ResourceTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Name",
                table: "Environments",
                column: "Name",
                unique: true);
        }
    }
}
