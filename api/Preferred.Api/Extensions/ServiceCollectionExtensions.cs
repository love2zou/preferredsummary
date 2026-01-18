using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Preferred.Api.Services;
using Zwav.Infrastructure.Storage;
using Zwav.Application.Workers;
using Zwav.Application.Processing;
using Video.Application.Processing;
using Preferred.Api.Models;

namespace Preferred.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            // 注册所有业务服务
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITagService, TagService>();
            services.AddScoped<IPictureService, PictureService>();
            services.AddScoped<INetworkUrlService, NetworkUrlService>();
            services.AddScoped<IFileService, FileService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<ISystemResourceService, SystemResourceService>();
            services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
            services.AddScoped<IBookingService, BookingService>();
            // 注册系统监控服务
            services.AddScoped<ISystemMonitorService, SystemMonitorService>();
            //录波文件服务
            services.AddScoped<IFileStorage, LocalFileStorage>();
            services.AddSingleton<IAnalysisQueue, AnalysisQueue>();
            services.AddScoped<IZwavAnalysisAppService, ZwavAnalysisAppService>();
            services.AddHostedService<AnalysisWorker>();
            //录波文件服务
            //视频分析服务
            services.AddScoped<IVideoAnalyticsService,VideoAnalyticsService>();
            services.AddSingleton<IVideoAnalysisQueue, VideoAnalysisQueue>();
            services.AddScoped<SparkDetectionService>();
            services.AddHostedService<VideoAnalysisWorker>();
            //视频分析服务

            // 注册后台服务
            services.AddHostedService<ScheduledTaskBackgroundService>();
            
            // 配置SystemMonitorConfig绑定
            services.Configure<SystemMonitorConfig>(configuration.GetSection("SystemMonitor"));
            
            return services;
        }
    }
}
