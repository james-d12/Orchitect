using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Conductor.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class orgupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OrganisationTeams_Organisations_OrganisationId1",
                table: "OrganisationTeams");

            migrationBuilder.DropIndex(
                name: "IX_OrganisationTeams_OrganisationId1",
                table: "OrganisationTeams");

            migrationBuilder.DropColumn(
                name: "OrganisationId1",
                table: "OrganisationTeams");

            migrationBuilder.CreateTable(
                name: "OrganisationTeamUsers",
                columns: table => new
                {
                    OrganisationTeamId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OrganisationUserId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationTeamUsers", x => new { x.OrganisationTeamId, x.OrganisationUserId });
                    table.ForeignKey(
                        name: "FK_OrganisationTeamUsers_OrganisationTeams_OrganisationTeamId",
                        column: x => x.OrganisationTeamId,
                        principalTable: "OrganisationTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganisationTeamUsers_OrganisationUsers_OrganisationUserId",
                        column: x => x.OrganisationUserId,
                        principalTable: "OrganisationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationTeamUsers_OrganisationUserId",
                table: "OrganisationTeamUsers",
                column: "OrganisationUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrganisationTeamUsers");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId1",
                table: "OrganisationTeams",
                type: "TEXT",
                nullable: true);

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
        }
    }
}
