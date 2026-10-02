using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class deployment_lifecycle_fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Deployments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorSummary",
                table: "Deployments",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedBy",
                table: "Deployments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "Deployments",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Deployments");

            migrationBuilder.DropColumn(
                name: "ErrorSummary",
                table: "Deployments");

            migrationBuilder.DropColumn(
                name: "RequestedBy",
                table: "Deployments");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "Deployments");
        }
    }
}
