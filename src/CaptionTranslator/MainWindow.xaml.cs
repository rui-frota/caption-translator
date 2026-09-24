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
    private DrawingRectangle? _captureRegion;
    private bool _isRecognizing;
    private DateTime _nextTranslationAttemptUtc = DateTime.MinValue;

    public MainWindow()
    {
        InitializeComponent();
        _captureService.FrameCaptured += CaptureService_FrameCaptured;
        Closed += (_, _) =>
        {
            _captureService.Dispose();
            _argosTranslator.Dispose();
        };
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
    }

}