using Axpense.Infrastructure.Intertfaces;
using Microsoft.Extensions.Configuration;

namespace Axpense.Infrastructure.Storage
{
    /// <summary>
    /// Stores files on the local disk (a Docker volume in production) under
    /// {root}/{organizationId}/{folder}/{guid}{ext}. Keys are validated on every read/delete
    /// so a key can never escape the storage root.
    /// </summary>
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly string _root;

        public LocalFileStorage(IConfiguration configuration)
        {
            var configured = configuration["FileStorage:RootPath"];
            _root = Path.GetFullPath(string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(AppContext.BaseDirectory, "storage")
                : configured);
            Directory.CreateDirectory(_root);
        }

        public async Task<string> SaveAsync(Guid organizationId, string folder, Stream content, string extension, CancellationToken cancellationToken = default)
        {
            var safeFolder = new string(folder.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());
            var safeExt = new string(extension.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            var key = $"{organizationId:N}/{safeFolder}/{Guid.NewGuid():N}.{safeExt}";

            var fullPath = Resolve(key);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, cancellationToken);
            return key;
        }

        public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken = default)
        {
            var fullPath = Resolve(key);
            Stream? stream = File.Exists(fullPath)
                ? new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            var fullPath = Resolve(key);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            return Task.CompletedTask;
        }

        public Task<long> UsageBytesAsync(Guid organizationId, CancellationToken cancellationToken = default)
        {
            var dir = Path.Combine(_root, organizationId.ToString("N"));
            if (!Directory.Exists(dir)) return Task.FromResult(0L);
            var total = new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            return Task.FromResult(total);
        }

        private string Resolve(string key)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_root, key));
            if (!fullPath.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid storage key.");
            return fullPath;
        }
    }
}
