using System.Collections.Generic;
using System.Text.Json.Serialization;
using FluentScrobbler.Services.Media;

namespace FluentScrobbler.Services
{
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, ListenBrainzService.DiskCacheEntry>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
