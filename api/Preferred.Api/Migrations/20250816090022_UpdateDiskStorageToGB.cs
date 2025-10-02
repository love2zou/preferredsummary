using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class UpdateDiskStorageToGB : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "DiskUsed",
                table: "Tb_SystemResource",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiskTotal",
                table: "Tb_SystemResource",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiskFree",
                table: "Tb_SystemResource",
                type: "decimal(10,2)",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "DiskUsed",
                table: "Tb_SystemResource",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<long>(
                name: "DiskTotal",
                table: "Tb_SystemResource",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");

            migrationBuilder.AlterColumn<long>(
                name: "DiskFree",
                table: "Tb_SystemResource",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(10,2)");
        }
    }
}
