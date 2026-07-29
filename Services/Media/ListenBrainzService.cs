using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FluentScrobbler.Services.Media
{
    public class ListenBrainzService
    {
        private readonly HttpClient _httpClient;

        public ListenBrainzService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "FluentScrobbler/1.0 (contact@seuemail.com)");
        }

        public async Task<string?> GetAlbumCoverUrlAsync(string albumName, string artistName)
        {
            try
            {
                string query = $"release:{Uri.EscapeDataString(albumName)} AND artist:{Uri.EscapeDataString(artistName)}";
                string searchUrl = $"https://musicbrainz.org/ws/2/release/?query={query}&fmt=json&limit=1";

                var response = await _httpClient.GetAsync(searchUrl);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    var releases = doc.RootElement.GetProperty("releases");
                    if (releases.GetArrayLength() > 0)
                    {
                        string mbid = releases[0].GetProperty("id").GetString()!;
                        return $"https://coverartarchive.org/release/{mbid}/front-500";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ListenBrainzService] Erro: {ex.Message}");
            }

            return null;
        }
    }
}