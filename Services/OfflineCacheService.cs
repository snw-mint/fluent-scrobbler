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
        private static OfflineCacheService? _instance;
        public static OfflineCacheService Instance => _instance ??= new OfflineCacheService();

        private readonly SQLiteAsyncConnection _db;
        
        private OfflineCacheService()
        {
            var dbPath = Path.Combine(AppInfoService.AppDataPath, "Data", "offline_cache.db");
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            _db = new SQLiteAsyncConnection(dbPath);
            _db.CreateTableAsync<ScrobbleEntry>().Wait();
        }

        public async Task AddScrobbleAsync(string track, string artist, string album, long timestamp)
        {
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
            return await _db.Table<ScrobbleEntry>()
                            .Where(s => s.Status == "Pending")
                            .OrderBy(s => s.Timestamp)
                            .Take(limit)
                            .ToListAsync();
        }

        public async Task RemoveScrobbleAsync(int id)
        {
            await _db.DeleteAsync<ScrobbleEntry>(id);
        }

        public async Task RemoveScrobblesAsync(IEnumerable<int> ids)
        {
            foreach (var id in ids)
            {
                await _db.DeleteAsync<ScrobbleEntry>(id);
            }
        }

        public async Task<int> GetPendingCountAsync()
        {
            return await _db.Table<ScrobbleEntry>().Where(s => s.Status == "Pending").CountAsync();
        }

        public async Task ClearCacheAsync()
        {
            await _db.DeleteAllAsync<ScrobbleEntry>();
        }
    }
}
