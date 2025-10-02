using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class AddHostNameAndDiskNameToSystemResource : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiskName",
                table: "Tb_SystemResource",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                comment: "系统盘名称");

            migrationBuilder.AddColumn<string>(
                name: "HostName",
                table: "Tb_SystemResource",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                comment: "主机名称");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_SystemResource_HostName",
                table: "Tb_SystemResource",
                column: "HostName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tb_SystemResource_HostName",
                table: "Tb_SystemResource");

            migrationBuilder.DropColumn(
                name: "DiskName",
                table: "Tb_SystemResource");

            migrationBuilder.DropColumn(
                name: "HostName",
                table: "Tb_SystemResource");
        }
    }
}
