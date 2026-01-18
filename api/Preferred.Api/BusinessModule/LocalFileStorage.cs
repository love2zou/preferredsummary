using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Zwav.Application.Parsing;
using Preferred.Api.Models;
using Microsoft.Extensions.Options;

namespace Zwav.Infrastructure.Storage
{
    public interface IFileStorage
    {
        Task<SavedFileResult> SaveAsync(IFormFile file, string analysisGuid, IProgress<int> progress, CancellationToken ct);
        string GetRoot();
    }

    public class LocalFileStorage : IFileStorage
    {
        private readonly string _root;
        private readonly string _VideoRootPath;
        private readonly FileStorageConfig _fileStorageConfig;
        public LocalFileStorage(IOptions<FileStorageConfig> fileStorageOptions)
        {
            _fileStorageConfig = fileStorageOptions.Value;
            _root = _fileStorageConfig.ZwavRootPath;
            _VideoRootPath = _fileStorageConfig.VideoRootPath;
            // 确保上传目录存在
            if (!Directory.Exists(_root))
            {
                Directory.CreateDirectory(_root);
            }
        }

        public string GetRoot() => _root;

        public async Task<SavedFileResult> SaveAsync(
            IFormFile file,
            string guid,
            IProgress<int> progress,
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(file.FileName);
            var targetPath = Path.Combine(_root, guid + ext);

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

            const int bufferSize = 1024 * 1024; // 1MB
            var buffer = new byte[bufferSize];

            long total = file.Length;
            int copied = 0;

            using var input = file.OpenReadStream();
            using var output = new FileStream(
                targetPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize,
                useAsync: true);

            int read;
            int lastReport = -1;

            while ((read = await input.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);

                copied += read;

                if (total > 0 && progress != null)
                {
                    int p = (int)(copied * 100L / total);
                    if (p != lastReport)
                    {
                        lastReport = p;
                        progress.Report(p);
                    }
                }
            }

            await output.FlushAsync(ct);

            return new SavedFileResult
            {
                FullPath = targetPath,
                FileSize = copied
            };
        }
    }
}