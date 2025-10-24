using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Preferred.Api.Models;

namespace Preferred.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<Picture> Pictures { get; set; }
        public DbSet<NetworkUrl> NetworkUrls { get; set; }
        public DbSet<FileRecord> FileRecords { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<SystemResource> SystemResources { get; set; }
        public DbSet<ScheduledTask> ScheduledTasks { get; set; }
        public DbSet<ScheduledTaskLog> ScheduledTaskLogs { get; set; }
        public DbSet<CoachMemberRelation> CoachMemberRelations { get; set; }
        public DbSet<BookTask> BookTasks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 用户表配置：直接使用属性名映射到 tb_user
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("tb_user");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
                // 新增：FullName 字段映射
                entity.Property(e => e.FullName).HasMaxLength(100)
                      .HasComment("昵称或姓名");
                entity.Property(e => e.Email).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CrtTime);
                entity.Property(e => e.UpdTime);
                entity.Property(e => e.LastLoginTime);

                entity.Property(e => e.UserTypeCode).HasMaxLength(20);
                entity.Property(e => e.UserToSystemCode).HasMaxLength(50);
                entity.Property(e => e.SeqNo).HasDefaultValue(0);

                entity.HasIndex(e => e.UserName).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // 标签表配置
            modelBuilder.Entity<Tag>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ParName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.TagCode).IsRequired().HasMaxLength(20);
                entity.Property(e => e.TagName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.HexColor).IsRequired().HasMaxLength(10);
                entity.Property(e => e.RgbColor).IsRequired().HasMaxLength(50);
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                
                // 创建复合唯一索引
                entity.HasIndex(e => new { e.ParName, e.TagCode }).IsUnique();
            });

            // 图片表配置
            modelBuilder.Entity<Picture>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.AppType).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ImageCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.ImageName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.ImagePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.AspectRatio).HasColumnType("decimal(10,4)");
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                
                // 创建复合唯一索引
                entity.HasIndex(e => new { e.AppType, e.ImageCode }).IsUnique();
            });

            modelBuilder.Entity<FileRecord>(entity =>
            {
                entity.ToTable("Tb_FileRecord");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.FileName).HasMaxLength(128).IsRequired();
                entity.Property(e => e.FileType).HasMaxLength(10).IsRequired();
                entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
                entity.Property(e => e.FileSize).HasMaxLength(100).IsRequired();
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.UploadUserId).HasDefaultValue(0);
                entity.Property(e => e.AppType).HasMaxLength(20).IsRequired();
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                
                // 创建索引
                entity.HasIndex(e => e.FileName);
            });

            // 分类表配置
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Tb_CategoryMenu");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CategoryCode).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CategoryName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CategoryIcon).HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                
                // 创建唯一索引
                entity.HasIndex(e => e.CategoryCode).IsUnique();
            });

            // 通知表配置
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Content).IsRequired().HasMaxLength(400);
                entity.Property(e => e.NotifyType).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SendUser).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Receiver).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Remark).HasMaxLength(200);
                entity.Property(e => e.IsRead).HasDefaultValue(0);
                entity.Property(e => e.NotifyStatus).HasDefaultValue(0);
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                
                // 创建索引 - 修正为正确的字段名
                entity.HasIndex(e => e.Receiver);
                entity.HasIndex(e => e.SendTime);
                entity.HasIndex(e => e.IsRead);
                entity.HasIndex(e => e.NotifyType);
            });

            // 在OnModelCreating方法中添加SystemResource配置
            // 系统资源表配置
            modelBuilder.Entity<SystemResource>(entity =>
            {
                entity.ToTable("Tb_SystemResource");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                
                // 新增字段配置
                entity.Property(e => e.HostName).HasMaxLength(100).IsRequired()
                    .HasComment("主机名称");
                
                entity.Property(e => e.CpuUsage).HasColumnType("decimal(5,2)").IsRequired();
                entity.Property(e => e.MemoryUsage).HasColumnType("decimal(5,2)").IsRequired();
                
                // 新增字段配置
                entity.Property(e => e.DiskName).HasMaxLength(100).IsRequired()
                    .HasComment("系统盘名称");
                
                entity.Property(e => e.DiskUsage).HasColumnType("decimal(5,2)").IsRequired();
                entity.Property(e => e.DiskTotal).IsRequired();
                entity.Property(e => e.DiskUsed).IsRequired();
                entity.Property(e => e.DiskFree).IsRequired();
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
                
                // 创建索引以提高查询性能
                entity.HasIndex(e => e.CrtTime);
                entity.HasIndex(e => e.HostName); // 新增主机名索引
            });
            
            // 定时任务表配置
            modelBuilder.Entity<ScheduledTask>(entity =>
            {
                entity.ToTable("Tb_ScheduledTask");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Cron).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Handler).IsRequired().HasMaxLength(255);
                entity.Property(e => e.Parameters).HasColumnType("TEXT");
                entity.Property(e => e.Enabled).IsRequired().HasDefaultValue(true);
                entity.Property(e => e.Remark).HasMaxLength(255);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
                
                // 创建唯一索引
                entity.HasIndex(e => e.Code).IsUnique();
                entity.HasIndex(e => e.Enabled);
                entity.HasIndex(e => e.NextRuntime);
            });
            
            // 定时任务日志表配置
            modelBuilder.Entity<ScheduledTaskLog>(entity =>
            {
                entity.ToTable("Tb_ScheduledTaskLog");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.TaskId).IsRequired();
                entity.Property(e => e.TaskCode).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StartTime).IsRequired();
                entity.Property(e => e.Success).IsRequired().HasDefaultValue(false);
                entity.Property(e => e.Message).HasColumnType("TEXT");
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
                
                // 创建外键关系
                entity.HasOne(e => e.ScheduledTask)
                      .WithMany()
                      .HasForeignKey(e => e.TaskId)
                      .OnDelete(DeleteBehavior.Cascade);
                
                // 创建索引
                entity.HasIndex(e => e.TaskId);
                entity.HasIndex(e => e.TaskCode);
                entity.HasIndex(e => e.StartTime);
                entity.HasIndex(e => e.Success);
            });
            modelBuilder.Entity<CoachMemberRelation>(entity =>
            {
                entity.ToTable("Tb_CoachMemberRelation");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CoachId).IsRequired();
                entity.Property(e => e.MemberId).IsRequired();
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
                entity.HasIndex(e => new { e.MemberId, e.CoachId }).IsUnique();
            });
            
            modelBuilder.Entity<BookTask>(entity =>
            {
                entity.ToTable("Tb_BookTask");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.CoachId).IsRequired();
                entity.Property(e => e.MemberId).IsRequired();
                entity.Property(e => e.BookDate).IsRequired();
                entity.Property(e => e.BookTimeSlot).IsRequired().HasMaxLength(20);
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
                entity.HasIndex(e => new { e.CoachId, e.BookDate, e.BookTimeSlot }).IsUnique();
            });
        }
    }
}