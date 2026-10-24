using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SQLite;
using FluentScrobbler.Models;

namespace FluentScrobbler.Services
{
    public class OfflineCacheService
    {
        private static readonly Lazy<OfflineCacheService> _instance = new(() => new OfflineCacheService());
        public static OfflineCacheService Instance => _instance.Value;

        private readonly SQLiteAsyncConnection _db;
        private readonly Task _initTask;
        
        private OfflineCacheService()
        {
            var dbPath = Path.Combine(AppInfoService.AppDataPath, "Data", "offline_cache.db");
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            _db = new SQLiteAsyncConnection(dbPath);
            _initTask = _db.CreateTableAsync<ScrobbleEntry>();
        }

        public async Task AddScrobbleAsync(string track, string artist, string album, long timestamp)
        {
            if (string.IsNullOrWhiteSpace(track) || string.IsNullOrWhiteSpace(artist))
            {
                return;
            }

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (timestamp <= 0 || (now - timestamp) > 1209600 || timestamp > now + 3600)
            {
                return;
            }

            await _initTask;
            var existing = await _db.Table<ScrobbleEntry>()
                                    .Where(s => s.Status == "Pending" && s.Track == track && s.Artist == artist)
                                    .ToListAsync();
            if (existing.Any(e => Math.Abs(e.Timestamp - timestamp) <= 60))
            {
                return;
            }

            var entry = new ScrobbleEntry
            {
                Track = track,
                Artist = artist,
                Album = album,
                Timestamp = timestamp,
                Status = "Pending"
            };
            await _db.InsertAsync(entry);
        }

        public async Task<List<ScrobbleEntry>> GetPendingScrobblesAsync(int limit = 50)
        {
            await _initTask;
            return await _db.Table<ScrobbleEntry>()
                            .Where(s => s.Status == "Pending")
                            .OrderBy(s => s.Timestamp)
                            .Take(limit)
                            .ToListAsync();
        }

        public async Task RemoveScrobbleAsync(int id)
        {
            await _initTask;
            await _db.DeleteAsync<ScrobbleEntry>(id);
        }

        public async Task RemoveScrobblesAsync(IEnumerable<int> ids)
        {
            var idList = ids as List<int> ?? ids.ToList();
            if (idList.Count == 0) return;

            await _initTask;
            await _db.RunInTransactionAsync(conn =>
            {
                foreach (var id in idList)
                {
                    conn.Delete<ScrobbleEntry>(id);
                }
            });
        }

        public async Task<int> PurgeExpiredScrobblesAsync(long cutoffTimestamp)
        {
            await _initTask;
            return await _db.ExecuteAsync("DELETE FROM ScrobbleEntries WHERE Timestamp < ?", cutoffTimestamp);
        }

        public async Task<int> GetPendingCountAsync()
        {
            await _initTask;
            return await _db.Table<ScrobbleEntry>().Where(s => s.Status == "Pending").CountAsync();
        }

        public async Task ClearCacheAsync()
        {
            await _initTask;
            await _db.DeleteAllAsync<ScrobbleEntry>();
        }
    }
}
