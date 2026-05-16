using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class add_resource_instance_and_dependency_graph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ResourceDependencyGraphs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nodes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceDependencyGraphs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ResourceInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    OutputLocation = table.Column<string>(type: "text", nullable: true),
                    OutputWorkspace = table.Column<string>(type: "text", nullable: true),
                    InputParameters = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "timezone('utc', now())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceInstances", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceDependencyGraphs_OrganisationId_EnvironmentId",
                table: "ResourceDependencyGraphs",
                columns: new[] { "OrganisationId", "EnvironmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceInstances_ResourceId_EnvironmentId",
                table: "ResourceInstances",
                columns: new[] { "ResourceId", "EnvironmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResourceDependencyGraphs");

            migrationBuilder.DropTable(
                name: "ResourceInstances");
        }
    }
}
