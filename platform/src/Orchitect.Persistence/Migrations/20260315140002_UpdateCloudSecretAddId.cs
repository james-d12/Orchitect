using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCloudSecretAddId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.AddColumn<string>(
                name: "Id",
                schema: "inventory",
                table: "CloudSecrets",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_CloudSecrets_OrganisationId_Platform",
                schema: "inventory",
                table: "CloudSecrets",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_CloudSecrets_Url",
                schema: "inventory",
                table: "CloudSecrets",
                column: "Url",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropIndex(
                name: "IX_CloudSecrets_OrganisationId_Platform",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropIndex(
                name: "IX_CloudSecrets_Url",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropColumn(
                name: "Id",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.AddPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets",
                columns: new[] { "OrganisationId", "Name", "Location", "Platform" });
        }
    }
}
