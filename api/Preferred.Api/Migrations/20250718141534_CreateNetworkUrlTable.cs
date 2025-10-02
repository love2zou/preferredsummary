using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class CreateNetworkUrlTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_NetWorkURL",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Url = table.Column<string>(maxLength: 100, nullable: false),
                    NAME = table.Column<string>(maxLength: 100, nullable: false),
                    Description = table.Column<string>(maxLength: 200, nullable: true),
                    ImageCode = table.Column<string>(maxLength: 50, nullable: false),
                    IconUrl = table.Column<string>(maxLength: 200, nullable: true),
                    IsAvailable = table.Column<long>(nullable: true),
                    IsMark = table.Column<long>(nullable: false),
                    TagCodeType = table.Column<string>(maxLength: 200, nullable: false),
                    SeqNo = table.Column<int>(nullable: false),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_NetWorkURL", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_NetWorkURL");
        }
    }
}
