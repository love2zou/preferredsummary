using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Preferred.Api.Services;
using Preferred.Api.Models;

namespace Preferred.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 注册应用程序服务
        /// </summary>
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
            // 注册系统监控服务
            services.AddScoped<ISystemMonitorService, SystemMonitorService>();
            
            
            // 注册后台服务
            services.AddHostedService<ScheduledTaskBackgroundService>();
            
            // 配置SystemMonitorConfig绑定
            services.Configure<SystemMonitorConfig>(configuration.GetSection("SystemMonitor"));
            
            return services;
        }
    }
}