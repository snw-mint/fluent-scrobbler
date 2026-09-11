using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SQLite;
using FluentScrobbler.Services;

namespace FluentScrobbler.Services.Media
{
    [Table("CoverCache")]
    public class CoverCacheEntry
    {
        [PrimaryKey]
        public string CacheKey { get; set; } = string.Empty;
        public string RemoteUrl { get; set; } = string.Empty;
        public string LocalPath { get; set; } = string.Empty;
        public DateTime LastAccessedUtc { get; set; }
    }

    public class CoverCacheService
    {
        private static CoverCacheService? _inst;
        public static CoverCacheService Instance => _inst ??= new CoverCacheService();

        private readonly string _dir;
        private readonly SQLiteAsyncConnection _db;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        private CoverCacheService()
        {
            _dir = Path.Combine(AppInfoService.AppDataPath, "Cache", "Covers");
            if (!Directory.Exists(_dir))
            {
                Directory.CreateDirectory(_dir);
            }

            var dbDir = Path.Combine(AppInfoService.AppDataPath, "Data");
            if (!Directory.Exists(dbDir))
            {
                Directory.CreateDirectory(dbDir);
            }

            var dbPath = Path.Combine(dbDir, "covers_cache.db");
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<CoverCacheEntry>().Wait();

            _ = Task.Run(CleanExpiredCacheAsync);
        }

        public async Task<(string? LocalUri, string? RemoteUrl)> GetCachedAsync(string key)
        {
            try
            {
                var entry = await _db.Table<CoverCacheEntry>().Where(x => x.CacheKey == key).FirstOrDefaultAsync();
                if (entry != null && File.Exists(entry.LocalPath))
                {
                    var now = DateTime.UtcNow;
                    entry.LastAccessedUtc = now;
                    await _db.UpdateAsync(entry);
                    try { File.SetLastWriteTimeUtc(entry.LocalPath, now); } catch { }
                    return (new Uri(entry.LocalPath).AbsoluteUri, entry.RemoteUrl);
                }
            }
            catch
            {
            }
            return (null, null);
        }

        public async Task<(string? LocalUri, string? RemoteUrl)> SaveAndCacheAsync(string key, string remoteUrl)
        {
            try
            {
                var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(remoteUrl))).ToLowerInvariant();
                var path = Path.Combine(_dir, $"{hash}.jpg");

                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    var bytes = await _http.GetByteArrayAsync(remoteUrl);
                    await File.WriteAllBytesAsync(path, bytes);
                }

                var now = DateTime.UtcNow;
                try { File.SetLastWriteTimeUtc(path, now); } catch { }

                var entry = new CoverCacheEntry
                {
                    CacheKey = key,
                    RemoteUrl = remoteUrl,
                    LocalPath = path,
                    LastAccessedUtc = now
                };
                await _db.InsertOrReplaceAsync(entry);
                return (new Uri(path).AbsoluteUri, remoteUrl);
            }
            catch
            {
            }
            return (null, null);
        }

        public async Task CleanExpiredCacheAsync()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-3);
                var expired = await _db.Table<CoverCacheEntry>().Where(x => x.LastAccessedUtc < cutoff).ToListAsync();

                foreach (var entry in expired)
                {
                    try
                    {
                        if (File.Exists(entry.LocalPath))
                        {
                            File.Delete(entry.LocalPath);
                        }
                    }
                    catch
                    {
                    }
                    await _db.DeleteAsync(entry);
                }

                if (Directory.Exists(_dir))
                {
                    var files = Directory.GetFiles(_dir, "*.jpg");
                    foreach (var f in files)
                    {
                        try
                        {
                            if (File.GetLastWriteTimeUtc(f) < cutoff)
                            {
                                File.Delete(f);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }
    }
}
