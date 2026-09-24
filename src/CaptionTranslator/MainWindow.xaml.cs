using System.Speech.Synthesis;
using System.Threading.Channels;
using System.Windows.Threading;
using System.Windows;
using CaptionTranslator.Capture;
using CaptionTranslator.Ocr;
using CaptionTranslator.Pipeline;
using CaptionTranslator.Translation;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;

namespace CaptionTranslator;

public partial class MainWindow : Window
{
    private readonly CaptionStabilizer _stabilizer = new();
    private readonly TranscriptBuffer _sourceTranscript = new();
    private readonly TranscriptBuffer _pendingTranslation = new();
    private readonly TranscriptBuffer _translationTranscript = new();
    private readonly ScreenCaptureService _captureService = new();
    private readonly WindowsOcrService _ocrService = new();
    private readonly OfflinePhraseTranslator _translator = new();
    private readonly ArgosTranslator _argosTranslator = new();
    private readonly SpeechSynthesizer _speechSynthesizer = new();
    private readonly Channel<SpeechRequest> _speechQueue = Channel.CreateUnbounded<SpeechRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true
        });
    private readonly CancellationTokenSource _speechCancellation = new();
    private readonly object _speechStateLock = new();
    private readonly Task _speechWorkerTask;
    private DrawingRectangle? _captureRegion;
    private bool _isRecognizing;
    private DateTime _nextTranslationAttemptUtc = DateTime.MinValue;
    private int _speechEnabled;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureSpeechVoice();
        SpeechEnabledCheckBox.Checked += SpeechEnabledCheckBox_Checked;
        SpeechEnabledCheckBox.Unchecked += SpeechEnabledCheckBox_Unchecked;
        _speechWorkerTask = SpeechWorkerAsync(_speechCancellation.Token);
        _captureService.FrameCaptured += CaptureService_FrameCaptured;
        Closed += MainWindow_Closed;
    }

    private void SpeechEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        Volatile.Write(ref _speechEnabled, 1);
    }

    private void SpeechEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        Volatile.Write(ref _speechEnabled, 0);
        StopSpeech();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        _captureService.Dispose();
        _argosTranslator.Dispose();
        Volatile.Write(ref _speechEnabled, 0);
        StopSpeech();
        _speechQueue.Writer.TryComplete();
        _speechCancellation.Cancel();

        try
        {
            await _speechWorkerTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _speechSynthesizer.Dispose();
            _speechCancellation.Dispose();
        }
    }

    private void ConfigureSpeechVoice()
    {
        try
        {
            var voices = _speechSynthesizer.GetInstalledVoices()
                .Where(voice => voice.Enabled)
                .ToList();
            var portugueseVoice = voices.FirstOrDefault(voice =>
                    string.Equals(voice.VoiceInfo.Culture.Name, "pt-BR", StringComparison.OrdinalIgnoreCase))
                ?? voices.FirstOrDefault(voice =>
                    voice.VoiceInfo.Culture.Name.StartsWith("pt", StringComparison.OrdinalIgnoreCase));

            if (portugueseVoice is not null)
            {
                _speechSynthesizer.SelectVoice(portugueseVoice.VoiceInfo.Name);
            }
        }
        catch (Exception)
        {
        }
    }

    private void SelectRegionButton_Click(object sender, RoutedEventArgs e)
    {
        var selector = new RegionSelectionWindow { Owner = this };
        if (selector.ShowDialog() != true || selector.SelectedRegion is not { } selectedRegion)
        {
            return;
        }

        _captureRegion = selectedRegion;
        RegionText.Text = $"Area selecionada: X={selectedRegion.X}, Y={selectedRegion.Y}, {selectedRegion.Width} x {selectedRegion.Height} px";
        StartButton.IsEnabled = true;
        StatusText.Text = "Pronto para ler as legendas.";
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_captureRegion is not { } region)
        {
            return;
        }

        _stabilizer.Reset();
        _sourceTranscript.Clear();
        _pendingTranslation.Clear();
        _translationTranscript.Clear();
        _nextTranslationAttemptUtc = DateTime.MinValue;
        SourceText.Clear();
        TranslationText.Clear();
        _captureService.Start(region);
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        StatusText.Text = "Lendo legendas localmente...";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _captureService.Stop();
        StartButton.IsEnabled = _captureRegion is not null;
        StopButton.IsEnabled = false;
        StatusText.Text = "Captura pausada.";
    }

    private async void CaptureService_FrameCaptured(object? sender, DrawingBitmap frame)
    {
        if (_isRecognizing)
        {
            frame.Dispose();
            return;
        }

        _isRecognizing = true;
        try
        {
            var recognizedText = await _ocrService.RecognizeAsync(frame);
            var stableText = _stabilizer.Accept(recognizedText);
            if (stableText is null)
            {
                return;
            }

            var newSourceSegment = _sourceTranscript.GetNewSegment(stableText);
            if (newSourceSegment.Length == 0)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                SourceText.Text = _sourceTranscript.Append(stableText);
                SourceText.ScrollToEnd();
            }, DispatcherPriority.Background);

            var pendingText = _pendingTranslation.Append(newSourceSegment);
            if (DateTime.UtcNow < _nextTranslationAttemptUtc)
            {
                return;
            }

            string? onlineTranslation;
            try
            {
                onlineTranslation = await _argosTranslator.TranslateAsync(pendingText);
            }
            catch (Exception)
            {
                onlineTranslation = null;
                _nextTranslationAttemptUtc = DateTime.UtcNow.AddSeconds(3);
            }

            if (!string.IsNullOrWhiteSpace(onlineTranslation))
            {
                _pendingTranslation.Clear();
                await Dispatcher.InvokeAsync(
                    () => UpdateTranslationText(onlineTranslation),
                    DispatcherPriority.Background);
            }
            else
            {
                _nextTranslationAttemptUtc = DateTime.UtcNow.AddSeconds(3);
                var offlineTranslation = _translator.Translate(pendingText);
                if (offlineTranslation.StartsWith(OfflinePhraseTranslator.PartialMessage, StringComparison.Ordinal))
                {
                    offlineTranslation = offlineTranslation[OfflinePhraseTranslator.PartialMessage.Length..].Trim();
                }

                if (!offlineTranslation.StartsWith(OfflinePhraseTranslator.UnavailableMessage, StringComparison.Ordinal)
                    && offlineTranslation.Length > 0)
                {
                    _pendingTranslation.Clear();
                    await Dispatcher.InvokeAsync(
                        () => UpdateTranslationText(offlineTranslation),
                        DispatcherPriority.Background);
                }
            }
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = $"Falha no OCR: {exception.Message}");
        }
        finally
        {
            frame.Dispose();
            _isRecognizing = false;
        }
    }

    private void UpdateTranslationText(string translation)
    {
        var newTranslationSegment = _translationTranscript.GetNewSegment(translation);
        if (newTranslationSegment.Length == 0)
        {
            return;
        }

        TranslationText.Text = _translationTranscript.Append(newTranslationSegment);
        TranslationText.ScrollToEnd();
        SpeakTranslationSegment(newTranslationSegment);
    }

    private void SpeakTranslationSegment(string translationSegment)
    {
        if (!SpeechEnabledCheckBox.IsChecked.GetValueOrDefault())
        {
            return;
        }

        try
        {
            var request = new SpeechRequest(
                translationSegment,
                (int)Math.Clamp(SpeechVolumeSlider.Value, 0, 100),
                (int)Math.Clamp(SpeechRateSlider.Value, -5, 5));
            lock (_speechStateLock)
            {
                if (Volatile.Read(ref _speechEnabled) != 0)
                {
                    _speechQueue.Writer.TryWrite(request);
                }
            }
        }
        catch (Exception)
        {
        }
    }

    private async Task SpeechWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var request in _speechQueue.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<SpeakCompletedEventArgs> completedHandler = (_, _) => completion.TrySetResult(true);

                lock (_speechStateLock)
                {
                    if (Volatile.Read(ref _speechEnabled) == 0)
                    {
                        continue;
                    }

                    _speechSynthesizer.Volume = request.Volume;
                    _speechSynthesizer.Rate = request.Rate;
                    _speechSynthesizer.SpeakCompleted += completedHandler;
                    try
                    {
                        _speechSynthesizer.SpeakAsync(request.Text);
                    }
                    catch (Exception)
                    {
                        _speechSynthesizer.SpeakCompleted -= completedHandler;
                        continue;
                    }
                }

                try
                {
                    await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                finally
                {
                    _speechSynthesizer.SpeakCompleted -= completedHandler;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void StopSpeech()
    {
        lock (_speechStateLock)
        {
            while (_speechQueue.Reader.TryRead(out _))
            {
            }

            try
            {
                _speechSynthesizer.SpeakAsyncCancelAll();
            }
            catch (Exception)
            {
            }
        }
    }

    private sealed record SpeechRequest(string Text, int Volume, int Rate);

}