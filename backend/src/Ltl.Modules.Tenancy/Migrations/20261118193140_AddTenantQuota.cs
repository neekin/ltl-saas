using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Tenancy.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantQuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "max_storage_bytes",
                table: "tenants",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_users",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "max_waybills",
                table: "tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "plan_name",
                table: "tenants",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_storage_bytes",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_users",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "max_waybills",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "plan_name",
                table: "tenants");
        }
    }
}
