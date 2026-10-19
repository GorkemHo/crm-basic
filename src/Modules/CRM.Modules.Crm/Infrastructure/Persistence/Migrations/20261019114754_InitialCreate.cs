using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CRM.Modules.Crm.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "crm");

            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    user_id = table.Column<int>(type: "integer", nullable: true),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "companies",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tax_number = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_companies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "crm",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    company_id = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<int>(type: "integer", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<int>(type: "integer", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                    table.ForeignKey(
                        name: "fk_customers_companies_company_id",
                        column: x => x.company_id,
                        principalSchema: "crm",
                        principalTable: "companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity_name_entity_id_occurred_at",
                schema: "crm",
                table: "audit_logs",
                columns: new[] { "entity_name", "entity_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_companies_company_name",
                schema: "crm",
                table: "companies",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "ix_companies_tax_number",
                schema: "crm",
                table: "companies",
                column: "tax_number",
                unique: true,
                filter: "tax_number IS NOT NULL AND is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_customers_company_id",
                schema: "crm",
                table: "customers",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_customers_email",
                schema: "crm",
                table: "customers",
                column: "email",
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "companies",
                schema: "crm");
        }
    }
}
