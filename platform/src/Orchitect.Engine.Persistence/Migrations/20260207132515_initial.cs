using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Orchitect.Engine.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "engine");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "Organisations",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organisations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resources",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ResourceTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Applications",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Repository_Name = table.Column<string>(type: "text", nullable: false),
                    Repository_Url = table.Column<string>(type: "text", nullable: false),
                    Repository_Provider = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Applications_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Environments",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Environments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Environments_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationServices",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationServices_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationTeams",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationTeams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationTeams_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceTemplates",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceTemplates_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoleClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleClaims_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationUsers",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IdentityUserId = table.Column<string>(type: "text", nullable: false),
                    OrganisationId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganisationUsers_Organisations_OrganisationId",
                        column: x => x.OrganisationId,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganisationUsers_Organisations_OrganisationId1",
                        column: x => x.OrganisationId1,
                        principalSchema: "engine",
                        principalTable: "Organisations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OrganisationUsers_Users_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserClaims",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserClaims_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                schema: "identity",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "identity",
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTokens",
                schema: "identity",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_UserTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "identity",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Deployments",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvironmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitId = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deployments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deployments_Applications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalSchema: "engine",
                        principalTable: "Applications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deployments_Environments_EnvironmentId",
                        column: x => x.EnvironmentId,
                        principalSchema: "engine",
                        principalTable: "Environments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceTemplateVersion",
                schema: "engine",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<string>(type: "text", nullable: false),
                    Source_BaseUrl = table.Column<string>(type: "text", nullable: false),
                    Source_FolderPath = table.Column<string>(type: "text", nullable: false),
                    Source_Tag = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceTemplateVersion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceTemplateVersion_ResourceTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalSchema: "engine",
                        principalTable: "ResourceTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrganisationTeamUsers",
                schema: "engine",
                columns: table => new
                {
                    OrganisationTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganisationUserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganisationTeamUsers", x => new { x.OrganisationTeamId, x.OrganisationUserId });
                    table.ForeignKey(
                        name: "FK_OrganisationTeamUsers_OrganisationTeams_OrganisationTeamId",
                        column: x => x.OrganisationTeamId,
                        principalSchema: "engine",
                        principalTable: "OrganisationTeams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrganisationTeamUsers_OrganisationUsers_OrganisationUserId",
                        column: x => x.OrganisationUserId,
                        principalSchema: "engine",
                        principalTable: "OrganisationUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Applications_Name_OrganisationId",
                schema: "engine",
                table: "Applications",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Applications_OrganisationId",
                schema: "engine",
                table: "Applications",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_ApplicationId_EnvironmentId_CommitId_Status",
                schema: "engine",
                table: "Deployments",
                columns: new[] { "ApplicationId", "EnvironmentId", "CommitId", "Status" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_EnvironmentId",
                schema: "engine",
                table: "Deployments",
                column: "EnvironmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Environments_Name_OrganisationId",
                schema: "engine",
                table: "Environments",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Environments_OrganisationId",
                schema: "engine",
                table: "Environments",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_Organisations_Name",
                schema: "engine",
                table: "Organisations",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationServices_Name_OrganisationId",
                schema: "engine",
                table: "OrganisationServices",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationServices_OrganisationId",
                schema: "engine",
                table: "OrganisationServices",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationTeams_Name_OrganisationId",
                schema: "engine",
                table: "OrganisationTeams",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationTeams_OrganisationId",
                schema: "engine",
                table: "OrganisationTeams",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationTeamUsers_OrganisationUserId",
                schema: "engine",
                table: "OrganisationTeamUsers",
                column: "OrganisationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUsers_IdentityUserId_OrganisationId",
                schema: "engine",
                table: "OrganisationUsers",
                columns: new[] { "IdentityUserId", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUsers_OrganisationId",
                schema: "engine",
                table: "OrganisationUsers",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_OrganisationUsers_OrganisationId1",
                schema: "engine",
                table: "OrganisationUsers",
                column: "OrganisationId1");

            migrationBuilder.CreateIndex(
                name: "IX_Resources_ApplicationId_EnvironmentId_ResourceTemplateId",
                schema: "engine",
                table: "Resources",
                columns: new[] { "ApplicationId", "EnvironmentId", "ResourceTemplateId" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplates_Name_OrganisationId",
                schema: "engine",
                table: "ResourceTemplates",
                columns: new[] { "Name", "OrganisationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplates_OrganisationId",
                schema: "engine",
                table: "ResourceTemplates",
                column: "OrganisationId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceTemplateVersion_TemplateId_Version",
                schema: "engine",
                table: "ResourceTemplateVersion",
                columns: new[] { "TemplateId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                schema: "identity",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "identity",
                table: "Roles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserClaims_UserId",
                schema: "identity",
                table: "UserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                schema: "identity",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                schema: "identity",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "identity",
                table: "Users",
                column: "NormalizedUserName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deployments",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "OrganisationServices",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "OrganisationTeamUsers",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "Resources",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "ResourceTemplateVersion",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "RoleClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserClaims",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserLogins",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserRoles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "UserTokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Applications",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "Environments",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "OrganisationTeams",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "OrganisationUsers",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "ResourceTemplates",
                schema: "engine");

            migrationBuilder.DropTable(
                name: "Roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "Organisations",
                schema: "engine");
        }
    }
}
