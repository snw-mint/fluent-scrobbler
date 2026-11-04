using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FluentScrobbler.Models;

namespace FluentScrobbler.Services
{
    public sealed class BlockedRulesService
    {
        private static readonly Lazy<BlockedRulesService> _lazy = new(() => new BlockedRulesService());
        public static BlockedRulesService Instance => _lazy.Value;

        private static readonly string FilePath = Path.Combine(AppInfoService.AppDataPath, "blocked_rules.json");
        private readonly object _lock = new();
        private readonly List<BlockedRule> _rules = new();
        private readonly HashSet<string> _blockedTracks = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _blockedArtists = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler? BlockedRulesChanged;

        private BlockedRulesService()
        {
            Load();
        }

        public bool IsTrackBlocked(string? track)
        {
            if (string.IsNullOrWhiteSpace(track)) return false;
            lock (_lock)
            {
                return _blockedTracks.Contains(track.Trim());
            }
        }

        private static readonly string[] ArtistSeparators = new[]
        {
            ",", ";", "/", "\\", "|", "&",
            " feat. ", " feat ", " ft. ", " ft ", " featuring ",
            " with ", " and ", " e ", " y ", " x ", " vs. ", " vs "
        };

        public bool IsArtistBlocked(string? artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return false;
            string clean = artist.Trim();

            lock (_lock)
            {
                if (_blockedArtists.Contains(clean)) return true;

                string primary = WindowsMediaService.FormatPrimaryArtist(clean);
                if (!string.IsNullOrWhiteSpace(primary) && _blockedArtists.Contains(primary))
                {
                    return true;
                }

                var tokens = clean.Split(ArtistSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (tokens.Length > 1)
                {
                    foreach (var token in tokens)
                    {
                        if (_blockedArtists.Contains(token))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        public bool IsBlocked(string? track, string? artist)
        {
            return IsTrackBlocked(track) || IsArtistBlocked(artist);
        }

        public void BlockTrack(string track)
        {
            if (string.IsNullOrWhiteSpace(track)) return;
            string clean = track.Trim();
            bool changed = false;

            lock (_lock)
            {
                if (!_blockedTracks.Contains(clean))
                {
                    _blockedTracks.Add(clean);
                    _rules.Add(new BlockedRule
                    {
                        Target = BlockedRuleTarget.Track,
                        Pattern = clean
                    });
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed)
            {
                BlockedRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void BlockArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return;
            string clean = artist.Trim();
            bool changed = false;

            lock (_lock)
            {
                if (!_blockedArtists.Contains(clean))
                {
                    _blockedArtists.Add(clean);
                    _rules.Add(new BlockedRule
                    {
                        Target = BlockedRuleTarget.Artist,
                        Pattern = clean
                    });
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed)
            {
                BlockedRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UnblockTrack(string track)
        {
            if (string.IsNullOrWhiteSpace(track)) return;
            string clean = track.Trim();
            bool changed = false;

            lock (_lock)
            {
                if (_blockedTracks.Remove(clean))
                {
                    _rules.RemoveAll(r => r.Target == BlockedRuleTarget.Track && string.Equals(r.Pattern, clean, StringComparison.OrdinalIgnoreCase));
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed)
            {
                BlockedRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void UnblockArtist(string artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return;
            string clean = artist.Trim();
            bool changed = false;

            lock (_lock)
            {
                if (_blockedArtists.Remove(clean))
                {
                    _rules.RemoveAll(r => r.Target == BlockedRuleTarget.Artist && string.Equals(r.Pattern, clean, StringComparison.OrdinalIgnoreCase));
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed)
            {
                BlockedRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void RemoveRule(string id)
        {
            bool changed = false;
            lock (_lock)
            {
                int index = _rules.FindIndex(r => r.Id == id);
                if (index >= 0)
                {
                    var rule = _rules[index];
                    _rules.RemoveAt(index);
                    if (rule.Target == BlockedRuleTarget.Track)
                    {
                        _blockedTracks.Remove(rule.Pattern);
                    }
                    else if (rule.Target == BlockedRuleTarget.Artist)
                    {
                        _blockedArtists.Remove(rule.Pattern);
                    }
                    changed = true;
                    SaveLocked();
                }
            }

            if (changed)
            {
                BlockedRulesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public IReadOnlyList<BlockedRule> GetRules()
        {
            lock (_lock)
            {
                return _rules.ToArray();
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                _rules.Clear();
                _blockedTracks.Clear();
                _blockedArtists.Clear();

                if (!File.Exists(FilePath)) return;

                try
                {
                    string json = File.ReadAllText(FilePath);
                    var items = JsonSerializer.Deserialize<List<BlockedRule>>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (string.IsNullOrWhiteSpace(item.Pattern)) continue;
                            string clean = item.Pattern.Trim();
                            item.Pattern = clean;
                            _rules.Add(item);

                            if (item.Target == BlockedRuleTarget.Track)
                            {
                                _blockedTracks.Add(clean);
                            }
                            else if (item.Target == BlockedRuleTarget.Artist)
                            {
                                _blockedArtists.Add(clean);
                            }
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private void SaveLocked()
        {
            try
            {
                Directory.CreateDirectory(AppInfoService.AppDataPath);
                string json = JsonSerializer.Serialize(_rules, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(FilePath, json);
            }
            catch
            {
            }
        }
    }
}

