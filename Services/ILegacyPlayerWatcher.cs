using System;
using FluentScrobbler.Models;

namespace FluentScrobbler.Services
{
    public interface ILegacyPlayerWatcher : IDisposable
    {
        bool IsRunning { get; }
        LegacyTrackInfo? CurrentTrack { get; }

        void Start();
        void Stop();
        void SetMonitoringState(bool enabled);

        event EventHandler<LegacyTrackInfo>? TrackChanged;
    }
}
