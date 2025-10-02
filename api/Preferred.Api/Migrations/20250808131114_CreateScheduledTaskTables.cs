using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class CreateScheduledTaskTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_ScheduledTask",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(maxLength: 100, nullable: false),
                    Code = table.Column<string>(maxLength: 100, nullable: false),
                    Cron = table.Column<string>(maxLength: 50, nullable: false),
                    Handler = table.Column<string>(maxLength: 255, nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", nullable: true),
                    Enabled = table.Column<bool>(nullable: false, defaultValue: true),
                    LastRunTime = table.Column<DateTime>(nullable: true),
                    NextRuntime = table.Column<DateTime>(nullable: true),
                    Remark = table.Column<string>(maxLength: 255, nullable: true),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_ScheduledTask", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tb_ScheduledTaskLog",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    TaskId = table.Column<int>(nullable: false),
                    TaskCode = table.Column<string>(maxLength: 100, nullable: false),
                    StartTime = table.Column<DateTime>(nullable: false),
                    EndTime = table.Column<DateTime>(nullable: true),
                    Success = table.Column<bool>(nullable: false, defaultValue: false),
                    Message = table.Column<string>(type: "TEXT", nullable: true),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_ScheduledTaskLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tb_ScheduledTaskLog_Tb_ScheduledTask_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tb_ScheduledTask",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTask_Code",
                table: "Tb_ScheduledTask",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTask_Enabled",
                table: "Tb_ScheduledTask",
                column: "Enabled");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTask_NextRuntime",
                table: "Tb_ScheduledTask",
                column: "NextRuntime");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTaskLog_StartTime",
                table: "Tb_ScheduledTaskLog",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTaskLog_Success",
                table: "Tb_ScheduledTaskLog",
                column: "Success");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTaskLog_TaskCode",
                table: "Tb_ScheduledTaskLog",
                column: "TaskCode");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_ScheduledTaskLog_TaskId",
                table: "Tb_ScheduledTaskLog",
                column: "TaskId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_ScheduledTaskLog");

            migrationBuilder.DropTable(
                name: "Tb_ScheduledTask");
        }
    }
}
