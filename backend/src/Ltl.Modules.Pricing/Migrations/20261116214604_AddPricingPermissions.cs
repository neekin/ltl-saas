using Ltl.Modules.Pricing.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Pricing.Migrations
{
    /// <inheritdoc />
    public partial class AddPricingPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "code", "name", "module" },
                columnTypes: new[] { "uuid", "character varying(100)", "character varying(200)", "character varying(64)" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000701"), PricingPermissionCodes.PricingRead, "查看计价规则/试算", PricingPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000702"), PricingPermissionCodes.PricingManage, "管理计价规则（版本/发布）", PricingPermissionCodes.Module },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000701"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000702"));
        }
    }
}
