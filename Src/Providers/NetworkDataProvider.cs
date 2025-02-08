using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using SoundFlow.Abstracts;
using SoundFlow.Enums;
using SoundFlow.Interfaces;
using SoundFlow.Utils;

namespace SoundFlow.Providers;

/// <summary>
///     Provides audio data from an internet source, supporting both direct audio URLs and HLS (m3u(8)) playlists.
/// </summary>
public sealed class NetworkDataProvider : ISoundDataProvider, IDisposable
{
    private readonly string _url;
    private ISoundDecoder? _decoder;
    private readonly HttpClient _httpClient;
    private Stream? _stream;

    private readonly Queue<float> _audioBuffer = new();
    private int _samplePosition;
    private bool _isEndOfStream;
    private bool _isDisposed;
    private readonly object _lock = new();

    // For HLS
    private bool _isHlsStream;
    private readonly List<HlsSegment> _hlsSegments = [];
    private int _currentSegmentIndex;
    private DateTime _lastPlaylistRefreshTime;
    private bool _isEndList;
    private double _hlsTotalDuration;
    private CancellationTokenSource? _cancellationTokenSource;
    private double _hlsTargetDuration = 5;

    /// <summary>
    ///     Initializes a new instance of the <see cref="NetworkDataProvider" /> class.
    /// </summary>
    /// <param name="url">The URL of the audio stream.</param>
    /// <param name="sampleRate">The sample rate of the audio data.</param>
    public NetworkDataProvider(string url, int? sampleRate = null)
    {
        _url = url ?? throw new ArgumentNullException(nameof(url));
        SampleRate = sampleRate;
        _httpClient = new HttpClient();
        Initialize();
    }

    /// <inheritdoc />
    public int Position
    {
        get
        {
            lock (_lock)
            {
                return _samplePosition;
            }
        }
    }

    /// <inheritdoc />
    public int Length { get; private set; }

    /// <inheritdoc />
    public bool CanSeek { get; private set; }

    /// <inheritdoc />
    public SampleFormat SampleFormat { get; private set; }

    /// <inheritdoc />
    public int? SampleRate { get; set; }

    /// <inheritdoc />
    public event EventHandler<EventArgs>? EndOfStreamReached;

    /// <inheritdoc />
    public event EventHandler<PositionChangedEventArgs>? PositionChanged;

    /// <inheritdoc />
    public int ReadBytes(Span<float> buffer)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        var samplesRead = 0;

        lock (_lock)
        {
            while (samplesRead < buffer.Length)
            {
                if (_audioBuffer.Count == 0)
                {
                    if (_isEndOfStream)
                    {
                        if (samplesRead == 0)
                            EndOfStreamReached?.Invoke(this, EventArgs.Empty);

                        break;
                    }

                    Monitor.Wait(_lock, TimeSpan.FromMilliseconds(100));
                    continue;
                }

                buffer[samplesRead++] = _audioBuffer.Dequeue();
            }

            _samplePosition += samplesRead;
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(_samplePosition));
            return samplesRead;
        }
    }

    /// <inheritdoc />
    public void Seek(int sampleOffset)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        if (!CanSeek)
            throw new NotSupportedException("Seeking is not supported for this stream.");

        lock (_lock)
        {
            if (_isHlsStream)
                SeekInHlsStream(sampleOffset);
            else
                SeekInDirectStream(sampleOffset);
        }
    }

    private async void Initialize()
    {
        _isHlsStream = await IsHlsUrlAsync(_url);
        if (_isHlsStream)
            InitializeHlsStream();
        else
            InitializeDirectStream();
    }

    private async Task<bool> IsHlsUrlAsync(string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, url);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response is { IsSuccessStatusCode: true, Content.Headers.ContentType: not null })
            {
                var contentType = response.Content.Headers.ContentType.MediaType!;
                if (contentType.Equals("application/vnd.apple.mpegurl", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("application/x-mpegURL", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("audio/x-mpegURL", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Equals("audio/mpegurl", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (url.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".m3u", StringComparison.OrdinalIgnoreCase))
                return true;

            var content = await DownloadPartialContentAsync(url, 1024);
            if (content != null)
            {
                if (content.Contains("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            // Ignore exceptions and default to false
        }

        return false;
    }

    private async Task<string?> DownloadPartialContentAsync(string url, int byteCount)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(0, byteCount - 1);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[byteCount];
            var bytesRead = await stream.ReadAsync(buffer);
            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
        catch
        {
            return null;
        }
    }

    private void InitializeDirectStream()
    {
        Task.Run(async () =>
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _url);
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                CanSeek = response.Headers.AcceptRanges.Contains("bytes");
                _stream = await response.Content.ReadAsStreamAsync();
                _decoder = AudioEngine.Instance.CreateDecoder(_stream);
                SampleFormat = _decoder.SampleFormat;
                Length = _decoder.Length;

                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => BufferDirectStreamAsync(_cancellationTokenSource.Token));
            }
            catch
            {
                lock (_lock)
                {
                    _isEndOfStream = true;
                    Monitor.PulseAll(_lock);
                }
            }
        });
    }

    private void InitializeHlsStream()
    {
        _cancellationTokenSource = new CancellationTokenSource();
        Task.Run(async () =>
        {
            try
            {
                await DownloadAndParsePlaylistAsync(_url, _cancellationTokenSource.Token);
                if (_hlsSegments.Count == 0)
                {
                    throw new InvalidOperationException("No segments found in HLS playlist.");
                }

                SampleFormat = SampleFormat.F32;
                Length = _isEndList ? (int)(_hlsTotalDuration * (SampleRate ?? 44100)) : -1;
                CanSeek = _isEndList;
                await BufferHlsStreamAsync(_cancellationTokenSource.Token);
            }
            catch
            {
                lock (_lock)
                {
                    _isEndOfStream = true;
                    Monitor.PulseAll(_lock);
                }
            }
        });
    }

    private void BufferDirectStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            var buffer = ArrayPool<float>.Shared.Rent(8192);

            try
            {
                while (!_isDisposed && !cancellationToken.IsCancellationRequested)
                {
                    var samplesRead = _decoder!.Decode(buffer);

                    if (samplesRead > 0)
                    {
                        lock (_lock)
                        {
                            for (var i = 0; i < samplesRead; i++)
                            {
                                _audioBuffer.Enqueue(buffer[i]);
                            }

                            Monitor.PulseAll(_lock);
                        }
                    }
                    else
                    {
                        lock (_lock)
                        {
                            _isEndOfStream = true;
                            Monitor.PulseAll(_lock);
                        }

                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(buffer);
            }
        }
        catch
        {
            lock (_lock)
            {
                _isEndOfStream = true;
                Monitor.PulseAll(_lock);
            }
        }
        finally
        {
            DisposeResources();
        }
    }

    private async Task DownloadAndParsePlaylistAsync(string url, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        ParseHlsPlaylist(content, url);
    }

    private void ParseHlsPlaylist(string playlistContent, string baseUrl)
    {
        var lines = playlistContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        double segmentDuration = 0;
        _hlsSegments.Clear();
        _hlsTotalDuration = 0;
        _isEndList = false;
        _hlsTargetDuration = 5; // Reset to default

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("#EXT-X-TARGETDURATION", StringComparison.OrdinalIgnoreCase))
            {
                var durationStr = trimmedLine["#EXT-X-TARGETDURATION:".Length..];
                if (double.TryParse(durationStr, out var duration))
                    _hlsTargetDuration = duration;
            }
            else if (trimmedLine.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                var durationStr = trimmedLine["#EXTINF:".Length..].Split(',')[0];
                if (double.TryParse(durationStr, out var duration))
                    segmentDuration = duration;
                else
                    segmentDuration = 0;
            }
            else if (trimmedLine.StartsWith("#EXT-X-ENDLIST", StringComparison.OrdinalIgnoreCase))
            {
                _isEndList = true;
            }
            else if (!trimmedLine.StartsWith("#"))
            {
                var segmentUri = CombineUri(baseUrl, trimmedLine);
                _hlsSegments.Add(new HlsSegment
                {
                    Uri = segmentUri,
                    Duration = segmentDuration
                });
                _hlsTotalDuration += segmentDuration;
                segmentDuration = 0;
            }
        }
    }

    private static string CombineUri(string baseUri, string relativeUri)
    {
        if (!Uri.TryCreate(baseUri, UriKind.Absolute, out var baseUriObj)) return relativeUri;
        return Uri.TryCreate(baseUriObj, relativeUri, out var newUri) ? newUri.ToString() : relativeUri;
    }

    private async Task BufferHlsStreamAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!_isDisposed && !cancellationToken.IsCancellationRequested)
            {
                if (!_isEndList && ShouldRefreshPlaylist())
                {
                    _lastPlaylistRefreshTime = DateTime.UtcNow;
                    await DownloadAndParsePlaylistAsync(_url, cancellationToken);
                }

                if (_currentSegmentIndex < _hlsSegments.Count)
                {
                    var segment = _hlsSegments[_currentSegmentIndex];
                    await DownloadAndBufferSegmentAsync(segment, cancellationToken);
                    _currentSegmentIndex++;
                }
                else if (_isEndList)
                {
                    lock (_lock)
                    {
                        _isEndOfStream = true;
                        Monitor.PulseAll(_lock);
                    }

                    EndOfStreamReached?.Invoke(this, EventArgs.Empty);
                    break;
                }
                else
                {
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch
        {
            lock (_lock)
            {
                _isEndOfStream = true;
                Monitor.PulseAll(_lock);
            }
        }
        finally
        {
            DisposeResources();
        }
    }

    private bool ShouldRefreshPlaylist()
    {
        if (_isEndList)
            return false;

        var elapsed = DateTime.UtcNow - _lastPlaylistRefreshTime;
        // Refresh the playlist a bit before the target duration to be safe (e.g., 80% of target duration)
        var refreshInterval = TimeSpan.FromSeconds(_hlsTargetDuration * 0.8);
        return elapsed >= refreshInterval;
    }

    private async Task DownloadAndBufferSegmentAsync(HlsSegment segment, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(segment.Uri, HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var segmentStream = await response.Content.ReadAsStreamAsync(cancellationToken);

            if (_decoder == null)
            {
                _decoder = AudioEngine.Instance.CreateDecoder(segmentStream);
                SampleFormat = _decoder.SampleFormat;
                SampleRate ??= AudioEngine.Instance.SampleRate;
            }

            var buffer = ArrayPool<float>.Shared.Rent(8192);

            try
            {
                while (!_isDisposed && !cancellationToken.IsCancellationRequested)
                {
                    var samplesRead = _decoder.Decode(buffer);

                    if (samplesRead > 0)
                    {
                        lock (_lock)
                        {
                            for (var i = 0; i < samplesRead; i++)
                            {
                                _audioBuffer.Enqueue(buffer[i]);
                            }

                            Monitor.PulseAll(_lock);
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(buffer);
            }
        }
        catch
        {
            // ignored
        }
    }

    private void SeekInDirectStream(int sampleOffset)
    {
        long byteOffset = sampleOffset * SampleFormat.GetBytesPerSample();

        Task.Run(async () =>
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, _url);
                request.Headers.Range = new RangeHeaderValue(byteOffset, null);

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                response.EnsureSuccessStatusCode();

                if (_stream != null)
                    await _stream.DisposeAsync();

                _stream = await response.Content.ReadAsStreamAsync();

                _decoder?.Dispose();
                _decoder = AudioEngine.Instance.CreateDecoder(_stream);

                lock (_lock)
                {
                    _samplePosition = sampleOffset;
                    PositionChanged?.Invoke(this, new PositionChangedEventArgs(_samplePosition));
                    _audioBuffer.Clear();
                }

                await _cancellationTokenSource?.CancelAsync()!;
                _cancellationTokenSource = new CancellationTokenSource();
                _ = Task.Run(() => BufferDirectStreamAsync(_cancellationTokenSource.Token));
            }
            catch
            {
                lock (_lock)
                {
                    _isEndOfStream = true;
                    Monitor.PulseAll(_lock);
                }
            }
        });
    }

    private void SeekInHlsStream(int sampleOffset)
    {
        var targetTime = sampleOffset / (double)(SampleRate ?? 44100);

        double cumulativeTime = 0;
        var index = 0;
        foreach (var segment in _hlsSegments)
        {
            cumulativeTime += segment.Duration;
            if (cumulativeTime >= targetTime)
                break;

            index++;
        }

        if (index >= _hlsSegments.Count)
            index = _hlsSegments.Count - 1;

        _currentSegmentIndex = index;

        lock (_lock)
        {
            _decoder?.Dispose();
            _audioBuffer.Clear();
            _samplePosition = sampleOffset;
            PositionChanged?.Invoke(this, new PositionChangedEventArgs(_samplePosition));
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource = new CancellationTokenSource();
        Task.Run(async () => { await BufferHlsStreamAsync(_cancellationTokenSource.Token); });
    }

    private void DisposeResources()
    {
        _decoder?.Dispose();
        _stream?.Dispose();
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _httpClient.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_isDisposed)
            return;

        lock (_lock)
        {
            _isDisposed = true;
            DisposeResources();
            _audioBuffer.Clear();
        }
    }

    private class HlsSegment
    {
        public string Uri { get; init; } = string.Empty;
        public double Duration { get; init; }
    }
}