using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentScrobbler.Models;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Services
{
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, ListenBrainzService.DiskCacheEntry>))]
    [JsonSerializable(typeof(List<BlockedRule>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
