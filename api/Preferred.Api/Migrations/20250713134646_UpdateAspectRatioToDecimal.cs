using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class UpdateAspectRatioToDecimal : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_Pictures",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    AppType = table.Column<string>(maxLength: 50, nullable: false),
                    ImageCode = table.Column<string>(maxLength: 50, nullable: false),
                    ImageName = table.Column<string>(maxLength: 200, nullable: false),
                    ImagePath = table.Column<string>(maxLength: 500, nullable: false),
                    AspectRatio = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Width = table.Column<int>(nullable: true),
                    Height = table.Column<int>(nullable: true),
                    SeqNo = table.Column<int>(nullable: false, defaultValue: 0),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_Pictures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_Pictures_AppType_ImageCode",
                table: "Tb_Pictures",
                columns: new[] { "AppType", "ImageCode" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_Pictures");
        }
    }
}
