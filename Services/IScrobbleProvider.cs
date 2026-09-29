using System.Threading.Tasks;

namespace FluentScrobbler.Services
{
    public interface IScrobbleProvider
    {
        string Name { get; }
        
        bool IsLoggedIn();
        
        Task<bool> UpdateNowPlayingAsync(string track, string artist, string album = "");
        
        Task<bool> ScrobbleTrackAsync(string track, string artist, string album = "", long? timestamp = null);
    }
}
