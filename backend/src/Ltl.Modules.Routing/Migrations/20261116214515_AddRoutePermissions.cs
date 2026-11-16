using Ltl.Modules.Routing.Domain;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ltl.Modules.Routing.Migrations
{
    /// <inheritdoc />
    public partial class AddRoutePermissions : Migration
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
                    { new Guid("00000000-0000-0000-0000-000000000601"), RoutePermissionCodes.RouteRead, "查看线路/运输方案", RoutePermissionCodes.Module },
                    { new Guid("00000000-0000-0000-0000-000000000602"), RoutePermissionCodes.RouteManage, "管理线路/站点/运输方案", RoutePermissionCodes.Module },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000601"));
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyColumnType: "uuid",
                keyValue: new Guid("00000000-0000-0000-0000-000000000602"));
        }
    }
}
