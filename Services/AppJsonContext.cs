using System.Collections.Generic;
using System.Text.Json.Serialization;
using Fluent Scrobbler.Services.Media;

namespace Fluent Scrobbler.Services
{
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(Dictionary<string, ListenBrainzService.DiskCacheEntry>))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
}
