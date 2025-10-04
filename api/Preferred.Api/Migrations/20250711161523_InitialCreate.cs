using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tb_User",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Username = table.Column<string>(maxLength: 50, nullable: false),
                    Email = table.Column<string>(maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(nullable: false),
                    Salt = table.Column<string>(nullable: false),
                    PhoneNumber = table.Column<string>(maxLength: 20, nullable: true),
                    ProfilePictureUrl = table.Column<string>(maxLength: 500, nullable: true),
                    Bio = table.Column<string>(maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(nullable: false),
                    IsEmailVerified = table.Column<bool>(nullable: false),
                    // 旧命名，便于后续迁移删除/替换
                    CreatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6)"),
                    UpdatedAt = table.Column<DateTime>(nullable: false, defaultValueSql: "CURRENT_TIMESTAMP(6) ON UPDATE CURRENT_TIMESTAMP(6)"),
                    LastLoginAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_User", x => x.Id);
                });
        
            migrationBuilder.CreateIndex(
                name: "IX_Tb_User_Email",
                table: "Tb_User",
                column: "Email",
                unique: true);
        
            migrationBuilder.CreateIndex(
                name: "IX_Tb_User_Username",
                table: "Tb_User",
                column: "Username",
                unique: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tb_User");
        }
    }
}
