using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Preferred.Api.Models;
using Microsoft.Extensions.Options;

namespace Zwav.Infrastructure.Storage
{
    public interface IFileStorage
    {
        Task<(string FullPath, long SizeBytes, string Sha256)> SaveAsync(IFormFile file, string analysisGuid);
        string GetRoot();
    }

    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;
        private readonly FileStorageConfig _fileStorageConfig;
        public LocalFileStorage(IOptions<FileStorageConfig> fileStorageOptions)
        {
            _fileStorageConfig = fileStorageOptions.Value;
            _root = _fileStorageConfig.ZwavRootPath;
            
            // 确保上传目录存在
            if (!Directory.Exists(_root))
            {
                Directory.CreateDirectory(_root);
            }
        }

        public string GetRoot() => _root;

        public async Task<(string FullPath, long SizeBytes, string Sha256)> SaveAsync(IFormFile file, string analysisGuid)
        {
            var dir = Path.Combine(_root, analysisGuid);
            Directory.CreateDirectory(dir);

            var safeName = Path.GetFileName(file.FileName);
            var fullPath = Path.Combine(dir, safeName);

            await using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs);
            }

            var size = new FileInfo(fullPath).Length;
            var sha256 = ComputeSha256(fullPath);
            return (fullPath, size, sha256);
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}