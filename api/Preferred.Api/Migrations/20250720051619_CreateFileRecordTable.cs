using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class CreateFileRecordTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 移除这行，因为 IconUrl 字段可能不存在
            // migrationBuilder.DropColumn(
            //     name: "IconUrl",
            //     table: "Tb_NetWorkURL");

            migrationBuilder.CreateTable(
                name: "Tb_FileRecord",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    FileName = table.Column<string>(maxLength: 128, nullable: false),
                    FileType = table.Column<string>(maxLength: 10, nullable: false),
                    FilePath = table.Column<string>(maxLength: 500, nullable: false),
                    FileSize = table.Column<string>(maxLength: 100, nullable: false),
                    Description = table.Column<string>(maxLength: 500, nullable: false),
                    UploadUserId = table.Column<int>(nullable: false, defaultValue: 0),
                    AppType = table.Column<string>(maxLength: 20, nullable: false),
                    SeqNo = table.Column<int>(nullable: false, defaultValue: 0),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_FileRecord", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_FileRecord_FileName",
                table: "Tb_FileRecord",
                column: "FileName");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_FileRecord");

            // 同样移除这行
            // migrationBuilder.AddColumn<string>(
            //     name: "IconUrl",
            //     table: "Tb_NetWorkURL",
            //     type: "varchar(200) CHARACTER SET utf8mb4",
            //     maxLength: 200,
            //     nullable: true);
        }
    }
}
