using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class resource_domain_refactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_ApplicationId_EnvironmentId_ResourceTemplateId",
                table: "Resources");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApplicationId",
                table: "Resources",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Resources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Resources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                table: "Resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "Resources",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_EnvironmentId_ResourceTemplateId",
                table: "Resources",
                columns: new[] { "EnvironmentId", "ResourceTemplateId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Resources_EnvironmentId_ResourceTemplateId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                table: "Resources");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "Resources");

            migrationBuilder.AlterColumn<Guid>(
                name: "ApplicationId",
                table: "Resources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ApplicationId_EnvironmentId_ResourceTemplateId",
                table: "Resources",
                columns: new[] { "ApplicationId", "EnvironmentId", "ResourceTemplateId" });
        }
    }
}
