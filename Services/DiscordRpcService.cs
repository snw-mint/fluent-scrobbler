using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FluentScrobbler.Services
{
    public class DiscordRpcService
    {
        private const string AppId = "1547589924251902073";
        private static DiscordRpcService? _inst;
        public static DiscordRpcService Instance => _inst ??= new DiscordRpcService();

        private NamedPipeClientStream? _pipe;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private bool _isReady;
        private DateTime _lastAttempt = DateTime.MinValue;

        public bool IsConnected => _pipe?.IsConnected == true && _isReady;

        private async Task<bool> ConnectAsync()
        {
            if (IsConnected) return true;

            if (DateTime.UtcNow - _lastAttempt < TimeSpan.FromSeconds(5))
            {
                LogService.LogInfo("[Discord RPC] Connect skipped due to throttle");
                return false;
            }
            _lastAttempt = DateTime.UtcNow;

            ClosePipe();

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    LogService.LogInfo($"[Discord RPC] Probing discord-ipc-{i}...");
                    var p = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut, PipeOptions.Asynchronous);
                    using var cts = new CancellationTokenSource(1000);
                    await p.ConnectAsync(cts.Token);

                    LogService.LogInfo($"[Discord RPC] Connected to pipe discord-ipc-{i}, sending handshake...");
                    _pipe = p;
                    if (await HandshakeAsync())
                    {
                        _isReady = true;
                        LogService.LogInfo($"[Discord RPC] Handshake successful on discord-ipc-{i}");
                        _ = ReadLoopAsync(_pipe);
                        return true;
                    }
                    LogService.LogWarning($"[Discord RPC] Handshake failed on discord-ipc-{i}");
                    ClosePipe();
                }
                catch (OperationCanceledException)
                {
                    ClosePipe();
                }
                catch (Exception ex)
                {
                    LogService.LogInfo($"[Discord RPC] discord-ipc-{i} not available: {ex.Message}");
                    ClosePipe();
                }
            }

            LogService.LogWarning("[Discord RPC] Could not connect to any Discord IPC pipe");
            return false;
        }

        private async Task<bool> HandshakeAsync()
        {
            if (_pipe == null || !_pipe.IsConnected) return false;

            string json = $"{{\"v\":1,\"client_id\":\"{AppId}\"}}";
            LogService.LogInfo($"[Discord RPC] Handshake payload: {json}");
            await WriteFrameAsync(0, json);

            using var cts = new CancellationTokenSource(3000);
            var (op, resp) = await ReadFrameAsync(_pipe, cts.Token);
            LogService.LogInfo($"[Discord RPC] Handshake response: op={op}, payload={resp}");
            return op == 1 && resp.Contains("\"READY\"");
        }

        private async Task WriteFrameAsync(int op, string json)
        {
            var p = _pipe;
            if (p == null || !p.IsConnected)
            {
                LogService.LogWarning("[Discord RPC] WriteFrame skipped, pipe not connected");
                return;
            }

            byte[] b = Encoding.UTF8.GetBytes(json);
            byte[] buf = new byte[8 + b.Length];

            BitConverter.GetBytes(op).CopyTo(buf, 0);
            BitConverter.GetBytes(b.Length).CopyTo(buf, 4);
            Buffer.BlockCopy(b, 0, buf, 8, b.Length);

            await _writeLock.WaitAsync();
            try
            {
                if (p.IsConnected)
                {
                    await p.WriteAsync(buf, 0, buf.Length);
                    await p.FlushAsync();
                    LogService.LogInfo($"[Discord RPC] Frame written: op={op}, len={b.Length}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("[Discord RPC] Failed writing frame to pipe", ex);
                ClosePipe();
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private async Task<(int op, string payload)> ReadFrameAsync(Stream s, CancellationToken ct = default)
        {
            byte[] h = new byte[8];
            int read = 0;
            while (read < 8)
            {
                int r = await s.ReadAsync(h.AsMemory(read, 8 - read), ct);
                if (r == 0) return (-1, string.Empty);
                read += r;
            }

            int op = BitConverter.ToInt32(h, 0);
            int len = BitConverter.ToInt32(h, 4);

            if (len <= 0 || len > 1048576) return (op, string.Empty);

            byte[] b = new byte[len];
            read = 0;
            while (read < len)
            {
                int r = await s.ReadAsync(b.AsMemory(read, len - read), ct);
                if (r == 0) return (op, string.Empty);
                read += r;
            }

            return (op, Encoding.UTF8.GetString(b));
        }

        private async Task ReadLoopAsync(NamedPipeClientStream p)
        {
            try
            {
                while (p.IsConnected && _isReady)
                {
                    var (op, json) = await ReadFrameAsync(p);
                    if (op == -1 || op == 2)
                    {
                        LogService.LogInfo($"[Discord RPC] Connection closed by Discord: op={op}");
                        break;
                    }
                    LogService.LogInfo($"[Discord RPC] Received from Discord: op={op}, len={json.Length}, payload={json}");
                    if (op == 3)
                    {
                        LogService.LogInfo("[Discord RPC] Received Ping, sending Pong");
                        await WriteFrameAsync(4, json);
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.LogInfo($"[Discord RPC] ReadLoop ended: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_pipe, p))
                {
                    ClosePipe();
                }
            }
        }

        public async Task UpdateActivityAsync(string title, string artist, string? album, string? imgUrl, long? startSec = null, long? endSec = null)
        {
            LogService.LogInfo($"[Discord RPC] UpdateActivity requested: title='{title}', artist='{artist}', album='{album}', img='{imgUrl}', start={startSec}, end={endSec}");
            await _lock.WaitAsync();
            try
            {
                if (!await ConnectAsync())
                {
                    LogService.LogWarning("[Discord RPC] UpdateActivity aborted: not connected");
                    return;
                }

                int pid = Environment.ProcessId;
                long start = startSec ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                string t = title.Length < 2 ? title + " " : (title.Length > 128 ? title[..128] : title);
                string a = artist.Length < 2 ? artist + " " : (artist.Length > 128 ? artist[..128] : artist);

                bool hasImg = !string.IsNullOrWhiteSpace(imgUrl) && (imgUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || imgUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
                if (hasImg && imgUrl != null)
                {
                    imgUrl = imgUrl.Replace("lastfm-img.freetls.fastly.net", "lastfm.freetls.fastly.net");
                }

                using var ms = new MemoryStream();
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("cmd", "SET_ACTIVITY");
                    w.WriteStartObject("args");
                    w.WriteNumber("pid", pid);
                    w.WriteStartObject("activity");
                    w.WriteNumber("type", 2);
                    w.WriteString("details", t);
                    w.WriteString("state", a);

                    w.WriteStartObject("timestamps");
                    w.WriteNumber("start", start);
                    if (endSec.HasValue && endSec.Value > start)
                    {
                        w.WriteNumber("end", endSec.Value);
                    }
                    w.WriteEndObject();

                    if (hasImg)
                    {
                        w.WriteStartObject("assets");
                        w.WriteString("large_image", imgUrl);
                        if (!string.IsNullOrWhiteSpace(album))
                        {
                            string al = album.Length > 128 ? album[..128] : album;
                            w.WriteString("large_text", al);
                        }
                        w.WriteEndObject();
                    }

                    w.WriteEndObject();
                    w.WriteEndObject();
                    w.WriteString("nonce", Guid.NewGuid().ToString("N"));
                    w.WriteEndObject();
                }

                string pld = Encoding.UTF8.GetString(ms.ToArray());
                LogService.LogInfo($"[Discord RPC] Sending SET_ACTIVITY: {pld}");
                await WriteFrameAsync(1, pld);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Discord RPC] Error during UpdateActivity", ex);
                ClosePipe();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ClearActivityAsync()
        {
            LogService.LogInfo("[Discord RPC] ClearActivity requested");
            await _lock.WaitAsync();
            try
            {
                if (!IsConnected)
                {
                    LogService.LogInfo("[Discord RPC] ClearActivity skipped: not connected");
                    return;
                }

                int pid = Environment.ProcessId;
                using var ms = new MemoryStream();
                using (var w = new Utf8JsonWriter(ms))
                {
                    w.WriteStartObject();
                    w.WriteString("cmd", "SET_ACTIVITY");
                    w.WriteStartObject("args");
                    w.WriteNumber("pid", pid);
                    w.WriteNull("activity");
                    w.WriteEndObject();
                    w.WriteString("nonce", Guid.NewGuid().ToString("N"));
                    w.WriteEndObject();
                }

                string pld = Encoding.UTF8.GetString(ms.ToArray());
                LogService.LogInfo($"[Discord RPC] Sending CLEAR: {pld}");
                await WriteFrameAsync(1, pld);
            }
            catch (Exception ex)
            {
                LogService.LogError("[Discord RPC] Error during ClearActivity", ex);
                ClosePipe();
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Disconnect()
        {
            _lock.Wait();
            try
            {
                LogService.LogInfo("[Discord RPC] Disconnect requested");
                ClosePipe();
            }
            finally
            {
                _lock.Release();
            }
        }

        private void ClosePipe()
        {
            _isReady = false;
            try
            {
                _pipe?.Dispose();
            }
            catch
            {
            }
            _pipe = null;
        }
    }
}
