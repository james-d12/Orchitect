using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganisationIdAndTeamToInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_CloudSecrets",
                table: "CloudSecrets");

            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.RenameTable(
                name: "WorkItems",
                newName: "WorkItems",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "Repositories",
                newName: "Repositories",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "PullRequests",
                newName: "PullRequests",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "Pipelines",
                newName: "Pipelines",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "Owners",
                newName: "Owners",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "CloudSecrets",
                newName: "CloudSecrets",
                newSchema: "inventory");

            migrationBuilder.RenameTable(
                name: "CloudResources",
                newName: "CloudResources",
                newSchema: "inventory");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "WorkItems",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "inventory",
                table: "WorkItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                schema: "inventory",
                table: "WorkItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "State",
                schema: "inventory",
                table: "WorkItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "WorkItems",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "WorkItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "Repositories",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "Repositories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultBranch",
                schema: "inventory",
                table: "Repositories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "Repositories",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "PullRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryUrl",
                schema: "inventory",
                table: "PullRequests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryName",
                schema: "inventory",
                table: "PullRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "PullRequests",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "PullRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "PullRequests",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "PullRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "Pipelines",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "Pipelines",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Pipelines",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "Pipelines",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Pipelines",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "Owners",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "Owners",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                schema: "inventory",
                table: "Owners",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Owners",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "Owners",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Owners",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "CloudSecrets",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                schema: "inventory",
                table: "CloudSecrets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "CloudSecrets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "CloudSecrets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "CloudSecrets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "CloudSecrets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                schema: "inventory",
                table: "CloudResources",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "inventory",
                table: "CloudResources",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "inventory",
                table: "CloudResources",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<DateTime>(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "CloudResources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "OrganisationId",
                schema: "inventory",
                table: "CloudResources",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "inventory",
                table: "CloudResources",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets",
                columns: new[] { "OrganisationId", "Name", "Location", "Platform" });

            migrationBuilder.CreateTable(
                name: "Teams",
                schema: "inventory",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Platform = table.Column<string>(type: "text", nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Organisations",
                        column: x => x.OrganisationId,
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_OrganisationId",
                schema: "inventory",
                table: "Repositories",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_OrganisationId_Platform",
                schema: "inventory",
                table: "Repositories",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Url",
                schema: "inventory",
                table: "Repositories",
                column: "Url",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_OrganisationId",
                schema: "inventory",
                table: "PullRequests",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_RepositoryUrl",
                schema: "inventory",
                table: "PullRequests",
                column: "RepositoryUrl");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequests_Status",
                schema: "inventory",
                table: "PullRequests",
                column: "Status",
                filter: "\"Status\" IN ('Active', 'Draft')");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_OrganisationId",
                schema: "inventory",
                table: "Pipelines",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Pipelines_OrganisationId_Platform",
                schema: "inventory",
                table: "Pipelines",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_Owners_OrganisationId",
                schema: "inventory",
                table: "Owners",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Owners_OrganisationId_Name_Platform",
                schema: "inventory",
                table: "Owners",
                columns: new[] { "OrganisationId", "Name", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_CloudSecrets_OrganisationId",
                schema: "inventory",
                table: "CloudSecrets",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_CloudResources_OrganisationId",
                schema: "inventory",
                table: "CloudResources",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_CloudResources_OrganisationId_Platform",
                schema: "inventory",
                table: "CloudResources",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OrganisationId",
                schema: "inventory",
                table: "Teams",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_OrganisationId_Platform",
                schema: "inventory",
                table: "Teams",
                columns: new[] { "OrganisationId", "Platform" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Url",
                schema: "inventory",
                table: "Teams",
                column: "Url",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CloudResources_Organisations",
                schema: "inventory",
                table: "CloudResources",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CloudSecrets_Organisations",
                schema: "inventory",
                table: "CloudSecrets",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Owners_Organisations",
                schema: "inventory",
                table: "Owners",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pipelines_Organisations",
                schema: "inventory",
                table: "Pipelines",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PullRequests_Organisations",
                schema: "inventory",
                table: "PullRequests",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Repositories_Organisations",
                schema: "inventory",
                table: "Repositories",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkItems_Organisations",
                schema: "inventory",
                table: "WorkItems",
                column: "OrganisationId",
                principalTable: "Organisations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CloudResources_Organisations",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.DropForeignKey(
                name: "FK_CloudSecrets_Organisations",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropForeignKey(
                name: "FK_Owners_Organisations",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropForeignKey(
                name: "FK_Pipelines_Organisations",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropForeignKey(
                name: "FK_PullRequests_Organisations",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Repositories_Organisations",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkItems_Organisations",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropTable(
                name: "Teams",
                schema: "inventory");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_OrganisationId",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_WorkItems_OrganisationId_Platform",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_OrganisationId",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_OrganisationId_Platform",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Url",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_PullRequests_OrganisationId",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropIndex(
                name: "IX_PullRequests_RepositoryUrl",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropIndex(
                name: "IX_PullRequests_Status",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_OrganisationId",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Pipelines_OrganisationId_Platform",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropIndex(
                name: "IX_Owners_OrganisationId",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropIndex(
                name: "IX_Owners_OrganisationId_Name_Platform",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropPrimaryKey(
                name: "PK_CloudSecrets",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropIndex(
                name: "IX_CloudSecrets_OrganisationId",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropIndex(
                name: "IX_CloudResources_OrganisationId",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.DropIndex(
                name: "IX_CloudResources_OrganisationId_Platform",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "WorkItems");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "PullRequests");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Pipelines");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "Owners");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "CloudSecrets");

            migrationBuilder.DropColumn(
                name: "DiscoveredAt",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.DropColumn(
                name: "OrganisationId",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "inventory",
                table: "CloudResources");

            migrationBuilder.RenameTable(
                name: "WorkItems",
                schema: "inventory",
                newName: "WorkItems");

            migrationBuilder.RenameTable(
                name: "Repositories",
                schema: "inventory",
                newName: "Repositories");

            migrationBuilder.RenameTable(
                name: "PullRequests",
                schema: "inventory",
                newName: "PullRequests");

            migrationBuilder.RenameTable(
                name: "Pipelines",
                schema: "inventory",
                newName: "Pipelines");

            migrationBuilder.RenameTable(
                name: "Owners",
                schema: "inventory",
                newName: "Owners");

            migrationBuilder.RenameTable(
                name: "CloudSecrets",
                schema: "inventory",
                newName: "CloudSecrets");

            migrationBuilder.RenameTable(
                name: "CloudResources",
                schema: "inventory",
                newName: "CloudResources");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "WorkItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "WorkItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "WorkItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "State",
                table: "WorkItems",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultBranch",
                table: "Repositories",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "PullRequests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryUrl",
                table: "PullRequests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "RepositoryName",
                table: "PullRequests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "PullRequests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Pipelines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Pipelines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Owners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Owners",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Owners",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "CloudSecrets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "CloudSecrets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CloudSecrets",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "CloudResources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "CloudResources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CloudResources",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AddPrimaryKey(
                name: "PK_CloudSecrets",
                table: "CloudSecrets",
                columns: new[] { "Name", "Location", "Platform" });
        }
    }
}
