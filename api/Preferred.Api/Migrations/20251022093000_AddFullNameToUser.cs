using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class AddFullNameToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 仅当缺失时新增 FullName 列（MySQL）
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'FullName'
), 
    'ALTER TABLE tb_user ADD COLUMN FullName VARCHAR(100) NULL COMMENT ''昵称或姓名'' AFTER UserName', 
    'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 回滚：仅当存在时删除 FullName 列
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'FullName'
), 
    'ALTER TABLE tb_user DROP COLUMN FullName', 
    'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");
        }
    }
}