using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class organisation_users_drop_duplicate_fk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganisationUsers_Organisations_OrganisationId1",
                table: "OrganisationUsers");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationUsers_OrganisationId1",
                table: "OrganisationUsers");

            migrationBuilder.DropColumn(
                name: "OrganisationId1",
                table: "OrganisationUsers");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId1",
                table: "OrganisationUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUsers_OrganisationId1",
                table: "OrganisationUsers",
                column: "OrganisationId1");

            migrationBuilder.AddForeignKey(
                name: "FK_OrganisationUsers_Organisations_OrganisationId1",
                table: "OrganisationUsers",
                column: "OrganisationId1",
                principalTable: "Organisations",
                principalColumn: "Id");
        }
    }
}
