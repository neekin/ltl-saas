using Ltl.Modules.Customers.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Customers.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPermissions : Migration
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
                    { new Guid("00000000-0000-0000-0000-000000000501"), CustomerPermissionCodes.CustomerRead, "查看客户档案", CustomerPermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000502"), CustomerPermissionCodes.CustomerManage, "管理客户（档案/联系人/地址/网点授权）", CustomerPermissionCodes.Module },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000501"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000502"));
        }
    }
}
