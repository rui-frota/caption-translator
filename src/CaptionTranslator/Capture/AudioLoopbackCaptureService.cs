using NAudio.Wave;

namespace CaptionTranslator.Capture;

public sealed class AudioLoopbackCaptureService : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private int _isRunning;

    public event EventHandler<AudioChunk>? AudioCaptured;

    public void Start()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
        {
            return;
        }

        try
        {
            var capture = new WasapiLoopbackCapture();
            capture.DataAvailable += Capture_DataAvailable;
            capture.RecordingStopped += Capture_RecordingStopped;
            _capture = capture;
            capture.StartRecording();
        }
        catch
        {
            Interlocked.Exchange(ref _isRunning, 0);
            _capture?.Dispose();
            _capture = null;
            throw;
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
            return;
        }

        var capture = Interlocked.Exchange(ref _capture, null);
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.StopRecording();
        }
        finally
        {
            capture.Dispose();
        }
    }

    public void Dispose() => Stop();

    private void Capture_DataAvailable(object? sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref _isRunning) == 0 || sender is not WasapiLoopbackCapture capture)
        {
            return;
        }

        var data = new byte[args.BytesRecorded];
        Buffer.BlockCopy(args.Buffer, 0, data, 0, args.BytesRecorded);
        AudioCaptured?.Invoke(this, new AudioChunk(
            data,
            capture.WaveFormat.SampleRate,
            capture.WaveFormat.Channels));
    }

    private void Capture_RecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (args.Exception is not null)
        {
            RecordingFailed?.Invoke(this, args.Exception);
        }
    }

    public event EventHandler<Exception>? RecordingFailed;
}
