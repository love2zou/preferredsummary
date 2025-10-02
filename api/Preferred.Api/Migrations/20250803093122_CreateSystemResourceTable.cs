using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class CreateSystemResourceTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_SystemResource",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CpuUsage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    MemoryUsage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DiskUsage = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    DiskTotal = table.Column<long>(nullable: false),
                    DiskUsed = table.Column<long>(nullable: false),
                    DiskFree = table.Column<long>(nullable: false),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_SystemResource", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_SystemResource_CrtTime",
                table: "Tb_SystemResource",
                column: "CrtTime");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_SystemResource");
        }
    }
}
