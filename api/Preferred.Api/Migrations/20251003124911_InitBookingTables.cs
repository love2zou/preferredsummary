using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Preferred.Api.Migrations
{
    public partial class InitBookingTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 仅当旧列存在时再删除
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'CreatedAt'
), 'ALTER TABLE tb_user DROP COLUMN CreatedAt', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := (SELECT IF(EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'LastLoginAt'
), 'ALTER TABLE tb_user DROP COLUMN LastLoginAt', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
SET @sql := (SELECT IF(EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'UpdatedAt'
), 'ALTER TABLE tb_user DROP COLUMN UpdatedAt', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");

            // 仅当需要时重命名列 Username -> UserName
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(
    EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'Username')
    AND NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'UserName'),
    'ALTER TABLE tb_user RENAME COLUMN Username TO UserName',
    'SELECT 1'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");

            // 仅当需要时重命名索引
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(
    EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND INDEX_NAME = 'IX_tb_user_Username')
    AND NOT EXISTS(SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND INDEX_NAME = 'IX_tb_user_UserName'),
    'ALTER TABLE tb_user RENAME INDEX IX_tb_user_Username TO IX_tb_user_UserName',
    'SELECT 1'
));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");

            // 仅当缺失时新增列（为非空时间列提供安全默认值）
            migrationBuilder.Sql(@"
SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'CrtTime'
), 'ALTER TABLE tb_user ADD COLUMN CrtTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'LastLoginTime'
), 'ALTER TABLE tb_user ADD COLUMN LastLoginTime datetime NULL', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'SeqNo'
), 'ALTER TABLE tb_user ADD COLUMN SeqNo int NOT NULL DEFAULT 0', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'UpdTime'
), 'ALTER TABLE tb_user ADD COLUMN UpdTime datetime NOT NULL DEFAULT CURRENT_TIMESTAMP', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'UserToSystemCode'
), 'ALTER TABLE tb_user ADD COLUMN UserToSystemCode varchar(50) NULL', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql := (SELECT IF(NOT EXISTS(
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'tb_user' AND COLUMN_NAME = 'UserTypeCode'
), 'ALTER TABLE tb_user ADD COLUMN UserTypeCode varchar(20) NULL', 'SELECT 1'));
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;
");

            // 预定相关表不变
            migrationBuilder.CreateTable(
                name: "Tb_BookTask",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoachId = table.Column<int>(nullable: false),
                    MemberId = table.Column<int>(nullable: false),
                    BookDate = table.Column<DateTime>(nullable: false),
                    BookTimeSlot = table.Column<string>(maxLength: 20, nullable: false),
                    SeqNo = table.Column<int>(nullable: false, defaultValue: 0),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_BookTask", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tb_CoachMemberRelation",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CoachId = table.Column<int>(nullable: false),
                    MemberId = table.Column<int>(nullable: false),
                    SeqNo = table.Column<int>(nullable: false, defaultValue: 0),
                    CrtTime = table.Column<DateTime>(nullable: false),
                    UpdTime = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tb_CoachMemberRelation", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tb_BookTask_CoachId_BookDate_BookTimeSlot",
                table: "Tb_BookTask",
                columns: new[] { "CoachId", "BookDate", "BookTimeSlot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tb_CoachMemberRelation_MemberId_CoachId",
                table: "Tb_CoachMemberRelation",
                columns: new[] { "MemberId", "CoachId" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 保留回滚逻辑（不会在正向迁移执行）
            migrationBuilder.DropTable(
                name: "Tb_BookTask");
        
            migrationBuilder.DropTable(
                name: "Tb_CoachMemberRelation");
        
            migrationBuilder.DropPrimaryKey(
                name: "PK_tb_user",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "CrtTime",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "LastLoginTime",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "SeqNo",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "UpdTime",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "UserToSystemCode",
                table: "tb_user");
        
            migrationBuilder.DropColumn(
                name: "UserTypeCode",
                table: "tb_user");
        
            migrationBuilder.RenameTable(
                name: "tb_user",
                newName: "Users");
        
            migrationBuilder.RenameColumn(
                name: "UserName",
                table: "Users",
                newName: "Username");
        
            // 恢复旧时间列
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "tb_user",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginAt",
                table: "tb_user",
                type: "datetime(6)",
                nullable: true);
        
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "tb_user",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                table: "Users",
                column: "Id");
        }
    }
}
