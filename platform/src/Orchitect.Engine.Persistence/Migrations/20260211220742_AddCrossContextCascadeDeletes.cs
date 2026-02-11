using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCrossContextCascadeDeletes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE engine."Applications"
                    ADD CONSTRAINT "FK_Applications_Organisations_OrganisationId"
                    FOREIGN KEY ("OrganisationId") REFERENCES core."Organisations" ("Id")
                    ON DELETE CASCADE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE engine."Environments"
                    ADD CONSTRAINT "FK_Environments_Organisations_OrganisationId"
                    FOREIGN KEY ("OrganisationId") REFERENCES core."Organisations" ("Id")
                    ON DELETE CASCADE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE engine."Services"
                    ADD CONSTRAINT "FK_Services_Organisations_OrganisationId"
                    FOREIGN KEY ("OrganisationId") REFERENCES core."Organisations" ("Id")
                    ON DELETE CASCADE;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE engine."ResourceTemplates"
                    ADD CONSTRAINT "FK_ResourceTemplates_Organisations_OrganisationId"
                    FOREIGN KEY ("OrganisationId") REFERENCES core."Organisations" ("Id")
                    ON DELETE CASCADE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""ALTER TABLE engine."Applications" DROP CONSTRAINT "FK_Applications_Organisations_OrganisationId";""");
            migrationBuilder.Sql("""ALTER TABLE engine."Environments" DROP CONSTRAINT "FK_Environments_Organisations_OrganisationId";""");
            migrationBuilder.Sql("""ALTER TABLE engine."Services" DROP CONSTRAINT "FK_Services_Organisations_OrganisationId";""");
            migrationBuilder.Sql("""ALTER TABLE engine."ResourceTemplates" DROP CONSTRAINT "FK_ResourceTemplates_Organisations_OrganisationId";""");
        }
    }
}
