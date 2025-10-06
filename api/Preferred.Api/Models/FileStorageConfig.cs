using System.IO;

namespace Preferred.Api.Models
{
    /// <summary>
    /// 文件存储配置
    /// </summary>
    public class FileStorageConfig
    {
        /// <summary>
        /// 服务器主机地址
        /// </summary>
        public string ServerHost { get; set; } = "localhost";
        
        /// <summary>
        /// 服务器端口
        /// </summary>
        public string ServerPort { get; set; } = "8080";
        
        /// <summary>
        /// 图片基础路径
        /// </summary>
        public string ImageBasePath { get; set; } = "/upload/images";
        
        /// <summary>
        /// 本地上传路径
        /// </summary>
        public string LocalUploadPath { get; set; } = "wwwroot/upload/images";
        
        /// <summary>
        /// 是否使用远程访问
        /// </summary>
        public bool UseRemoteAccess { get; set; } = true;
        
        /// <summary>
        /// 获取完整的服务器图片URL
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>完整URL</returns>
        public string GetImageUrl(string fileName)
        {
            if (UseRemoteAccess)
            {
                return $"http://{ServerHost}:{ServerPort}{ImageBasePath}/{fileName}";
            }
            return $"{ImageBasePath}/{fileName}";
        }
        
        /// <summary>
        /// 获取本地存储路径（容器内的绝对路径）
        /// </summary>
        /// <returns>本地路径</returns>
        public string GetLocalUploadPath()
        {
            // 在容器环境中，使用绝对路径确保与docker-compose.yml中的挂载路径一致
            if (Path.IsPathRooted(LocalUploadPath))
            {
                return LocalUploadPath;
            }
            
            // 容器内的工作目录是 /app，所以拼接完整路径
            return Path.Combine("/app", LocalUploadPath);
        }
    }
}