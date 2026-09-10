using Avalonia.Platform;
using NAudio.Wave;

namespace Dynastia.App.Audio;

/// <summary>
/// Sequential four-track background playlist.
/// Missing assets are skipped; if no track exists, the game continues silently.
/// </summary>
public sealed class BackgroundMusicService :
    IDisposable
{
    private static readonly string[] Tracks =
    [
        "Dynasty 1.mp3",
        "Dynasty 2.mp3",
        "Dynasty 3.mp3",
        "Dynasty 4.mp3"
    ];

    private readonly object _gate =
        new();

    private int _nextTrackIndex;

    private WaveOutEvent? _output;
    private Mp3FileReader? _reader;
    private Stream? _stream;

    private bool _disposed;
    private bool _advancing;

    public float Volume { get; set; } =
        0.28f;

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed
                || _output is not null)
            {
                return;
            }

            PlayNextAvailableTrack();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed =
                true;

            ReleaseCurrentTrack();
        }
    }

    private void PlayNextAvailableTrack()
    {
        if (_disposed
            || _advancing)
        {
            return;
        }

        _advancing =
            true;

        try
        {
            ReleaseCurrentTrack();

            for (var attempt = 0;
                attempt < Tracks.Length;
                attempt++)
            {
                var index =
                    _nextTrackIndex
                    % Tracks.Length;

                _nextTrackIndex =
                    (index + 1)
                    % Tracks.Length;

                var uri =
                    BuildAssetUri(
                        Tracks[index]);

                if (!AssetLoader.Exists(
                    uri))
                {
                    continue;
                }

                using var assetStream =
                    AssetLoader.Open(
                        uri);

                var memoryStream =
                    new MemoryStream();

                assetStream.CopyTo(
                    memoryStream);

                memoryStream.Position =
                    0;

                _stream =
                    memoryStream;

                _reader =
                    new Mp3FileReader(
                        _stream);

                _output =
                    new WaveOutEvent
                    {
                        Volume =
                            Volume
                    };

                _output.PlaybackStopped +=
                    OnPlaybackStopped;

                _output.Init(
                    _reader);

                _output.Play();

                Console.WriteLine(
                    $"[music] Playing {Tracks[index]}");

                return;
            }

            Console.WriteLine(
                "[music] No Dynasty 1-4 MP3 assets found. " +
                "Background music remains disabled.");
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"[music] Could not start background track: " +
                $"{exception.Message}");

            ReleaseCurrentTrack();
        }
        finally
        {
            _advancing =
                false;
        }
    }

    private void OnPlaybackStopped(
        object? sender,
        StoppedEventArgs e)
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (e.Exception is not null)
            {
                Console.Error.WriteLine(
                    $"[music] Playback error: " +
                    $"{e.Exception.Message}");
            }

            // PlaybackStopped may be raised by Dispose/Stop while we are
            // intentionally changing tracks.
            if (_advancing)
                return;

            PlayNextAvailableTrack();
        }
    }

    private void ReleaseCurrentTrack()
    {
        if (_output is not null)
        {
            _output.PlaybackStopped -=
                OnPlaybackStopped;

            try
            {
                _output.Stop();
            }
            catch
            {
                // Best effort during shutdown/track transition.
            }

            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;

        _stream?.Dispose();
        _stream = null;
    }

    private static Uri BuildAssetUri(
        string fileName)
    {
        var encoded =
            Uri.EscapeDataString(
                fileName);

        return new Uri(
            $"avares://Dynastia.App/Assets/Audio/{encoded}");
    }
}
