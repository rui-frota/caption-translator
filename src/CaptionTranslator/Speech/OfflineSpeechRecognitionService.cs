using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using CaptionTranslator.Capture;
using Whisper.net;

namespace CaptionTranslator.Speech;

public sealed class OfflineSpeechRecognitionService : IDisposable
{
    private const int WindowSampleCount = 16000 * 4;
    private readonly AudioLoopbackCaptureService _captureService;
    private readonly string _modelPath;
    private readonly Channel<AudioChunk> _chunks = Channel.CreateBounded<AudioChunk>(
        new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private WhisperProcessor? _processor;
    private WhisperFactory? _factory;
    private int _isRunning;

    public OfflineSpeechRecognitionService(
        AudioLoopbackCaptureService captureService,
        string modelPath)
    {
        _captureService = captureService;
        _modelPath = modelPath;
        _captureService.AudioCaptured += CaptureService_AudioCaptured;
        _captureService.RecordingFailed += CaptureService_RecordingFailed;
    }

    public event EventHandler<string>? TextRecognized;
    public event EventHandler<Exception>? RecognitionFailed;

    public void Start()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
        {
            return;
        }

        if (!File.Exists(_modelPath))
        {
            Interlocked.Exchange(ref _isRunning, 0);
            throw new FileNotFoundException(
                $"Modelo Whisper nao encontrado: {_modelPath}",
                _modelPath);
        }

        try
        {
            _factory = WhisperFactory.FromPath(_modelPath);
            _processor = _factory.CreateBuilder()
                .WithLanguage("en")
                .Build();
            _cancellation = new CancellationTokenSource();
            _worker = ProcessAudioAsync(_cancellation.Token);
            _captureService.Start();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _isRunning, 0) == 0)
        {
            return;
        }

        _captureService.Stop();
        _cancellation?.Cancel();
        _worker?.GetAwaiter().GetResult();
        _worker = null;
        _cancellation?.Dispose();
        _cancellation = null;
        _processor?.Dispose();
        _processor = null;
        _factory?.Dispose();
        _factory = null;
        while (_chunks.Reader.TryRead(out _))
        {
        }
    }

    public void Dispose()
    {
        _captureService.AudioCaptured -= CaptureService_AudioCaptured;
        _captureService.RecordingFailed -= CaptureService_RecordingFailed;
        Stop();
    }

    private void CaptureService_AudioCaptured(object? sender, AudioChunk chunk)
    {
        if (Volatile.Read(ref _isRunning) != 0)
        {
            _chunks.Writer.TryWrite(chunk);
        }
    }

    private void CaptureService_RecordingFailed(object? sender, Exception exception)
    {
        RecognitionFailed?.Invoke(this, exception);
    }

    private async Task ProcessAudioAsync(CancellationToken cancellationToken)
    {
        var samples = new List<float>(WindowSampleCount);

        try
        {
            await foreach (var chunk in _chunks.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                AppendSamples(samples, chunk);
                if (samples.Count < WindowSampleCount)
                {
                    continue;
                }

                var window = samples.ToArray();
                samples.Clear();
                await TranscribeAsync(window, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            RecognitionFailed?.Invoke(this, exception);
        }
    }

    private async Task TranscribeAsync(float[] samples, CancellationToken cancellationToken)
    {
        if (_processor is null)
        {
            return;
        }

        await foreach (var result in _processor.ProcessAsync(samples, cancellationToken).ConfigureAwait(false))
        {
            var text = result.Text.Trim();
            if (text.Length > 0)
            {
                TextRecognized?.Invoke(this, text);
            }
        }
    }

    private static void AppendSamples(List<float> destination, AudioChunk chunk)
    {
        if (chunk.Channels <= 0 || chunk.SampleRate <= 0 || chunk.Data.Length < sizeof(float))
        {
            return;
        }

        var floatCount = chunk.Data.Length / sizeof(float);
        var interleaved = MemoryMarshal.Cast<byte, float>(chunk.Data.AsSpan(0, floatCount * sizeof(float)));
        var monoSamples = new float[interleaved.Length / chunk.Channels];
        for (var index = 0; index < interleaved.Length; index += chunk.Channels)
        {
            var sample = 0f;
            var channelCount = Math.Min(chunk.Channels, interleaved.Length - index);
            for (var channel = 0; channel < channelCount; channel++)
            {
                sample += interleaved[index + channel];
            }

            monoSamples[index / chunk.Channels] = sample / channelCount;
        }

        if (chunk.SampleRate == 16000)
        {
            destination.AddRange(monoSamples);
            return;
        }

        var outputCount = (int)Math.Floor(monoSamples.Length * 16000d / chunk.SampleRate);
        for (var outputIndex = 0; outputIndex < outputCount; outputIndex++)
        {
            var sourcePosition = outputIndex * chunk.SampleRate / 16000d;
            var sourceIndex = (int)sourcePosition;
            var nextIndex = Math.Min(sourceIndex + 1, monoSamples.Length - 1);
            var fraction = (float)(sourcePosition - sourceIndex);
            destination.Add(monoSamples[sourceIndex] + (monoSamples[nextIndex] - monoSamples[sourceIndex]) * fraction);
        }
    }
}
