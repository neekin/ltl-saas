using Ltl.Modules.Waybills.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Waybills.Migrations
{
    /// <inheritdoc />
    public partial class AddWaybillPermission : Migration
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
                    { new Guid("00000000-0000-0000-0000-000000000301"), WaybillPermissionCodes.WaybillRead, "查看运单（含打印数据）", WaybillPermissionCodes.Module },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000301"));
        }
    }
}
