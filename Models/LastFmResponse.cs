using System.Text.Json.Serialization;

namespace Fluent Scrobbler.Models
{
    public class LastFmAuthTokenResponse
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    public class LastFmSessionResponse
    {
        [JsonPropertyName("session")]
        public LastFmSession? Session { get; set; }
    }

    public class LastFmSession
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;
    }
}
