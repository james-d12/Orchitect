using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class organisationupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId1",
                table: "OrganisationUsers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId1",
                table: "OrganisationTeams",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUsers_OrganisationId1",
                table: "OrganisationUsers",
                column: "OrganisationId1");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationTeams_OrganisationId1",
                table: "OrganisationTeams",
                column: "OrganisationId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganisationTeams_Organisations_OrganisationId1",
                table: "OrganisationTeams",
                column: "OrganisationId1",
                principalTable: "Organisations",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganisationUsers_Organisations_OrganisationId1",
                table: "OrganisationUsers",
                column: "OrganisationId1",
                principalTable: "Organisations",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganisationTeams_Organisations_OrganisationId1",
                table: "OrganisationTeams");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganisationUsers_Organisations_OrganisationId1",
                table: "OrganisationUsers");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationUsers_OrganisationId1",
                table: "OrganisationUsers");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationTeams_OrganisationId1",
                table: "OrganisationTeams");

            migrationBuilder.DropColumn(
                name: "OrganisationId1",
                table: "OrganisationUsers");

            migrationBuilder.DropColumn(
                name: "OrganisationId1",
                table: "OrganisationTeams");
        }
    }
}
