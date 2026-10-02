using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Orchitect.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class runner_read_only_role : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'orchitect_runner') THEN
                        CREATE ROLE orchitect_runner NOLOGIN;
                    END IF;
                    EXECUTE format('GRANT CONNECT ON DATABASE %I TO orchitect_runner', current_database());
                END
                $$;

                GRANT USAGE ON SCHEMA public TO orchitect_runner;

                GRANT SELECT ON TABLE
                    "Applications",
                    "Deployments",
                    "ResourceTemplates",
                    "ResourceTemplateVersion"
                TO orchitect_runner;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                REVOKE ALL ON TABLE
                    "Applications",
                    "Deployments",
                    "ResourceTemplates",
                    "ResourceTemplateVersion"
                FROM orchitect_runner;

                REVOKE USAGE ON SCHEMA public FROM orchitect_runner;

                DO $$
                BEGIN
                    EXECUTE format('REVOKE CONNECT ON DATABASE %I FROM orchitect_runner', current_database());
                END
                $$;
                """);
        }
    }
}
