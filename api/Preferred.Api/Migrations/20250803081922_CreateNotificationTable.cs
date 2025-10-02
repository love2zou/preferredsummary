using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class CreateNotificationTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_Notification",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    IsRead = table.Column<int>(nullable: false, defaultValue: 0),
                    Name = table.Column<string>(maxLength: 36, nullable: false),
                    Content = table.Column<string>(maxLength: 400, nullable: false),
                    NotifyType = table.Column<string>(maxLength: 20, nullable: false),
                    NotifyStatus = table.Column<int>(nullable: false, defaultValue: 0),
                    SendTime = table.Column<DateTime>(nullable: false),
                    SendUser = table.Column<string>(maxLength: 100, nullable: false),
                    Receiver = table.Column<string>(maxLength: 100, nullable: false),
                    Remark = table.Column<string>(maxLength: 200, nullable: true),
                    SeqNo = table.Column<int>(nullable: false, defaultValue: 0),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_Notification", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_Notification_IsRead",
                table: "Tb_Notification",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_Notification_NotifyType",
                table: "Tb_Notification",
                column: "NotifyType");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_Notification_Receiver",
                table: "Tb_Notification",
                column: "Receiver");

            migrationBuilder.CreateIndex(
                name: "IX_Tb_Notification_SendTime",
                table: "Tb_Notification",
                column: "SendTime");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_Notification");
        }
    }
}
