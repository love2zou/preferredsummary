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
        // ====================== ZWAV 录波分析表映射- Start======================
        public DbSet<ZwavAnalysis> ZwavAnalyses { get; set; }
        public DbSet<ZwavFile> ZwavFiles { get; set; }
        public DbSet<ZwavCfg> ZwavCfgs { get; set; }
        public DbSet<ZwavChannel> ZwavChannels { get; set; }
        public DbSet<ZwavHdr> ZwavHdrs { get; set; }
        public DbSet<ZwavData> ZwavDatas { get; set; }
        // ====================== ZWAV 录波分析表映射 - End ======================

        // ====================== 视频分析表映射- Start======================
        public DbSet<VideoAnalysisJob> VideoAnalysisJobs { get; set; }
        public DbSet<VideoAnalysisFile> VideoAnalysisFiles { get; set; }
        public DbSet<VideoAnalysisEvent> VideoAnalysisEvents { get; set; }
        public DbSet<VideoAnalysisSnapshot> VideoAnalysisSnapshots { get; set; }
        // ====================== 视频分析表映射 - End ======================

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

            // ====================== ZWAV 录波分析表映射 ======================
            modelBuilder.Entity<ZwavFile>(entity =>
            {
                entity.ToTable("Tb_ZwavFile");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.OriginalName).IsRequired().HasMaxLength(255);
                entity.Property(e => e.FileSize).IsRequired();
                entity.Property(e => e.StoragePath).IsRequired().HasMaxLength(1024);
                entity.Property(e => e.ExtractPath).IsRequired().HasMaxLength(1024);
                
                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
            });

            modelBuilder.Entity<ZwavAnalysis>(entity =>
            {
                entity.ToTable("Tb_ZwavAnalysis");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.AnalysisGuid).IsRequired().HasMaxLength(36);
                entity.Property(e => e.Status).IsRequired().HasMaxLength(16);
                entity.Property(e => e.Progress).HasDefaultValue(0);
                entity.Property(e => e.ErrorMessage).HasColumnType("TEXT");

                entity.Property(e => e.FileId).IsRequired();

                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();

                entity.HasIndex(e => e.AnalysisGuid).IsUnique();
                entity.HasIndex(e => new { e.Status, e.CrtTime });

                // 可选：外键导航
                entity.HasOne(e => e.File)
                    .WithMany()
                    .HasForeignKey(e => e.FileId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ZwavCfg>(entity =>
            {
                entity.ToTable("Tb_ZwavCfg");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.AnalysisId).IsRequired();
                entity.Property(e => e.FullCfgText).HasColumnType("TEXT"); 
                entity.Property(e => e.StationName).HasMaxLength(128);
                entity.Property(e => e.DeviceId).HasMaxLength(128);
                entity.Property(e => e.Revision).HasMaxLength(64);

                entity.Property(e => e.FrequencyHz).HasColumnType("decimal(10,3)");
                entity.Property(e => e.TimeMul).HasColumnType("decimal(18,8)");

                entity.Property(e => e.StartTimeRaw).HasMaxLength(64);
                entity.Property(e => e.TriggerTimeRaw).HasMaxLength(64);

                entity.Property(e => e.FormatType).HasMaxLength(16);
                entity.Property(e => e.DataType).HasMaxLength(16);

                entity.Property(e => e.SampleRateJson).HasColumnType("JSON");

                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();

                entity.HasIndex(e => e.AnalysisId).IsUnique();
            });

            modelBuilder.Entity<ZwavHdr>(entity =>
            {
                entity.ToTable("Tb_ZwavHdr");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.AnalysisId).IsRequired();
                entity.Property(e => e.FaultStartTime).HasMaxLength(50);
                entity.Property(e => e.FaultKeepingTime).HasMaxLength(50);

                entity.Property(e => e.DeviceInfoJson).HasColumnType("JSON");
                entity.Property(e => e.TripInfoJSON).HasColumnType("JSON");
                entity.Property(e => e.FaultInfoJson).HasColumnType("JSON");
                entity.Property(e => e.DigitalStatusJson).HasColumnType("JSON");
                entity.Property(e => e.DigitalEventJson).HasColumnType("JSON");
                entity.Property(e => e.SettingValueJson).HasColumnType("JSON");
                entity.Property(e => e.RelayEnaValueJSON).HasColumnType("JSON");

                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();

                entity.HasIndex(e => e.AnalysisId).IsUnique();
            });

            modelBuilder.Entity<ZwavChannel>(entity =>
            {
                entity.ToTable("Tb_ZwavChannel");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.AnalysisId).IsRequired();
                entity.Property(e => e.ChannelIndex).IsRequired();
                entity.Property(e => e.ChannelType).IsRequired().HasMaxLength(16);

                entity.Property(e => e.ChannelCode).HasMaxLength(64);
                entity.Property(e => e.ChannelName).HasMaxLength(128);
                entity.Property(e => e.Phase).HasMaxLength(16);
                entity.Property(e => e.Unit).HasMaxLength(32);

                entity.Property(e => e.RatioA).HasColumnType("decimal(18,8)");
                entity.Property(e => e.OffsetB).HasColumnType("decimal(18,8)");
                entity.Property(e => e.Skew).HasColumnType("decimal(18,8)");

                entity.Property(e => e.IsEnable).HasDefaultValue(1);

                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();

                entity.HasIndex(e => new { e.AnalysisId, e.ChannelType, e.ChannelIndex }).IsUnique();
                entity.HasIndex(e => new { e.AnalysisId, e.ChannelType });
            });

            modelBuilder.Entity<ZwavData>(entity =>
            {
                // 指定表名
                entity.ToTable("Tb_ZwavData");

                // 设置主键
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.AnalysisId).IsRequired();
                // 设置 SampleNo 和 TimeRaw 为必填字段
                entity.Property(e => e.SampleNo).IsRequired();
                entity.Property(e => e.TimeRaw).IsRequired();
                entity.Property(e => e.TimeMs).IsRequired();
                
                // 设置模拟量通道（最多70个，示例只显示几个，您可以根据需要继续扩展）
                entity.Property(e => e.Channel1).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel2).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel3).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel4).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel5).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel6).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel7).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel8).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel9).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel10).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel11).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel12).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel13).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel14).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel15).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel16).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel17).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel18).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel19).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel20).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel21).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel22).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel23).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel24).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel25).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel26).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel27).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel28).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel29).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel30).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel31).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel32).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel33).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel34).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel35).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel36).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel37).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel38).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel39).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel40).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel41).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel42).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel43).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel44).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel45).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel46).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel47).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel48).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel49).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel50).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel51).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel52).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel53).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel54).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel55).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel56).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel57).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel58).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel59).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel60).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel61).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel62).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel63).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel64).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel65).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel66).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel67).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel68).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel69).HasColumnType("DOUBLE");
                entity.Property(e => e.Channel70).HasColumnType("DOUBLE");

                entity.Property(x => x.DigitalWords).HasColumnType("varbinary(100)").HasMaxLength(100);

                entity.Property(e => e.SeqNo).HasDefaultValue(0);
                entity.Property(e => e.CrtTime).IsRequired();
                entity.Property(e => e.UpdTime).IsRequired();
            });
            
            // ====================== 视频分析表映射 ======================
            modelBuilder.Entity<VideoAnalysisJob>(entity =>
            {
                entity.ToTable("Tb_VideoAnalysisJob");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.JobNo).IsRequired().HasMaxLength(64)
                    .HasComment("视频分析任务编号（业务唯一）");

                entity.Property(e => e.Status).IsRequired()
                    .HasComment("任务状态：0=待处理，1=处理中，2=完成，3=失败，4=取消");

                entity.Property(e => e.Progress).IsRequired().HasDefaultValue(0)
                    .HasComment("任务执行进度（0-100）");

                entity.Property(e => e.AlgoCode).IsRequired().HasMaxLength(50)
                    .HasComment("算法标识（如 spark_v1）");

                // 表结构是 TEXT，这里用 TEXT 映射（不要用 JSON，以免和你DDL不一致）
                entity.Property(e => e.AlgoParamsJson).HasColumnType("TEXT")
                    .HasComment("算法参数配置(JSON)");

                entity.Property(e => e.TotalVideoCount).IsRequired().HasDefaultValue(0)
                    .HasComment("视频总数量");

                entity.Property(e => e.FinishedVideoCount).IsRequired().HasDefaultValue(0)
                    .HasComment("已完成分析的视频数量");

                entity.Property(e => e.TotalEventCount).IsRequired().HasDefaultValue(0)
                    .HasComment("识别出的事件总数");

                entity.Property(e => e.ErrorMessage).HasColumnType("TEXT")
                    .HasComment("任务失败或异常信息");

                entity.Property(e => e.StartTime)
                    .HasComment("任务开始时间");

                entity.Property(e => e.FinishTime)
                    .HasComment("任务完成时间");

                entity.Property(e => e.SeqNo).IsRequired().HasDefaultValue(0)
                    .HasComment("排序号");

                entity.Property(e => e.CrtTime).IsRequired()
                    .HasComment("创建时间");

                entity.Property(e => e.UpdTime).IsRequired()
                    .HasComment("最后修改时间");
            });

            modelBuilder.Entity<VideoAnalysisFile>(entity =>
            {
                entity.ToTable("Tb_VideoAnalysisFile");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.JobId).IsRequired()
                    .HasComment("视频分析任务ID");

                entity.Property(e => e.FileName).IsRequired().HasMaxLength(255)
                    .HasComment("视频原始文件名");

                entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1024)
                    .HasComment("视频文件存储路径");

                entity.Property(e => e.EventCount)
                    .HasComment("识别事件数");

                entity.Property(e => e.AnalyzeMs)
                    .HasComment("分析耗时(秒)");
                entity.Property(e => e.DurationSec)
                    .HasComment("视频时长（秒）");

                entity.Property(e => e.Width)
                    .HasComment("视频宽度（像素）");

                entity.Property(e => e.Height)
                    .HasComment("视频高度（像素）");

                entity.Property(e => e.Status).IsRequired()
                    .HasComment("视频处理状态：0=待处理，1=处理中，2=完成，3=失败");

                entity.Property(e => e.ErrorMessage).HasColumnType("TEXT")
                    .HasComment("视频处理失败原因");

                entity.Property(e => e.SeqNo).IsRequired().HasDefaultValue(0)
                    .HasComment("视频顺序号");

                entity.Property(e => e.CrtTime).IsRequired()
                    .HasComment("创建时间");

                entity.Property(e => e.UpdTime).IsRequired()
                    .HasComment("最后修改时间");
            });

            modelBuilder.Entity<VideoAnalysisEvent>(entity =>
            {
                entity.ToTable("Tb_VideoAnalysisEvent");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.JobId).IsRequired()
                    .HasComment("视频分析任务ID");

                entity.Property(e => e.VideoFileId).IsRequired()
                    .HasComment("视频文件ID");

                entity.Property(e => e.EventType).IsRequired()
                    .HasComment("事件类型：1=闪光，2=火花");

                entity.Property(e => e.StartTimeSec).IsRequired()
                    .HasComment("事件开始时间点（秒）");

                entity.Property(e => e.EndTimeSec).IsRequired()
                    .HasComment("事件结束时间点（秒）");

                entity.Property(e => e.PeakTimeSec).IsRequired()
                    .HasComment("事件峰值时间点（秒）");

                entity.Property(e => e.FrameIndex).IsRequired()
                    .HasComment("峰值帧序号");

                entity.Property(e => e.Confidence).HasColumnType("decimal(5,4)").IsRequired()
                    .HasComment("识别置信度（0-1）");

                entity.Property(e => e.BBoxJson).HasColumnType("TEXT").IsRequired()
                    .HasComment("事件边界框信息(JSON：x,y,w,h)");

                entity.Property(e => e.SeqNo).IsRequired().HasDefaultValue(0)
                    .HasComment("视频顺序号");

                entity.Property(e => e.CrtTime).IsRequired()
                    .HasComment("创建时间");

                entity.Property(e => e.UpdTime).IsRequired()
                    .HasComment("最后修改时间");
            });

            modelBuilder.Entity<VideoAnalysisSnapshot>(entity =>
            {
                entity.ToTable("Tb_VideoAnalysisSnapshot");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.Property(e => e.EventId).IsRequired()
                    .HasComment("所属识别事件ID");

                entity.Property(e => e.ImagePath).IsRequired().HasMaxLength(1024)
                    .HasComment("已画框截图文件路径");

                entity.Property(e => e.TimeSec).IsRequired()
                    .HasComment("截图对应视频时间点（秒）");

                entity.Property(e => e.FrameIndex).IsRequired()
                    .HasComment("截图对应帧序号");

                entity.Property(e => e.ImageWidth).IsRequired()
                    .HasComment("截图宽度（像素）");

                entity.Property(e => e.ImageHeight).IsRequired()
                    .HasComment("截图高度（像素）");

                entity.Property(e => e.SeqNo).IsRequired().HasDefaultValue(0)
                    .HasComment("视频顺序号");

                entity.Property(e => e.CrtTime).IsRequired()
                    .HasComment("创建时间");

                entity.Property(e => e.UpdTime).IsRequired()
                    .HasComment("最后修改时间");
            });
        }
    }
}