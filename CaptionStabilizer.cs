namespace CaptionTranslator.Pipeline;

public sealed class CaptionStabilizer
{
    private string? _candidate;
    private int _candidateCount;
    private string? _lastEmitted;

    public string? Accept(string recognizedText)
    {
        if (string.IsNullOrWhiteSpace(recognizedText))
        {
            return null;
        }

        if (string.Equals(_candidate, recognizedText, StringComparison.Ordinal))
        {
            _candidateCount++;
        }
        else
        {
            _candidate = recognizedText;
            _candidateCount = 1;
        }

        if (_candidateCount < 2 || string.Equals(_lastEmitted, recognizedText, StringComparison.Ordinal))
        {
            return null;
        }

        _lastEmitted = recognizedText;
        return recognizedText;
    }

    public void Reset()
    {
        _candidate = null;
        _candidateCount = 0;
        _lastEmitted = null;
    }
}